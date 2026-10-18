using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customerRepo;
    private readonly CouponRepository _couponRepo;
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(
        JsonFileStore store,
        CustomerRepository customerRepo,
        CouponRepository couponRepo,
        IEnumerable<int>? failShippingForOrderIds = null)
    {
        _store = store;
        _customerRepo = customerRepo;
        _couponRepo = couponRepo;
        _failShippingForOrderIds = failShippingForOrderIds != null ? new HashSet<int>(failShippingForOrderIds) : new HashSet<int>();
    }

    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"PricingService -> Calculating SubTotal for Order #{order.Id} (DB query: open 200ms)...");
        await Task.Delay(200, ct);

        Log.Info($"PricingService -> Executing query for product prices for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var products = await _store.ReadListAsync<Product>(DataFiles.Products, ct);
        decimal subTotal = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId)
                ?? throw new InvalidOperationException($"Product #{item.ProductId} not found");

            item.UnitPrice = product.Price;
            subTotal += item.UnitPrice * item.Quantity;
        }

        order.SubTotal = Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
        return order.SubTotal;
    }

    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"PricingService -> Discount API DNS/handshake (150ms) for Order #{order.Id}...");
        await Task.Delay(150, ct);

        Log.Info($"PricingService -> Sending discount request (300ms) for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"PricingService -> Waiting for discount response (500-1500ms) for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
        int loyaltyPercent = customer?.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0,
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        int couponPercent = 0;
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(order.CouponCode, ct);
            if (coupon == null || coupon.ExpiryDate < DateTime.Today)
            {
                throw new InvalidCouponException(coupon == null 
                    ? $"Coupon '{order.CouponCode}' does not exist" 
                    : $"Coupon '{order.CouponCode}' has expired");
            }
            couponPercent = coupon.DiscountPercent;
        }

        int totalDiscountPercent = Math.Min(30, loyaltyPercent + couponPercent);
        decimal discount = order.SubTotal * totalDiscountPercent / 100m;
        order.Discount = Math.Round(discount, 2, MidpointRounding.AwayFromZero);
        return order.Discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"PricingService -> Shipping API DNS/handshake (150ms) for Order #{order.Id}...");
        await Task.Delay(150, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            Log.Error($"PricingService -> Shipping provider failed for Order #{order.Id}!");
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");
        }

        Log.Info($"PricingService -> Sending shipping request (300ms) for Order #{order.Id}...");
        await Task.Delay(300, ct);

        Log.Info($"PricingService -> Waiting for shipping response (500-1500ms) for Order #{order.Id}...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        if (order.SubTotal >= 1000m)
        {
            order.ShippingCost = 0.00m;
            return 0.00m;
        }

        var products = await _store.ReadListAsync<Product>(DataFiles.Products, ct);
        int totalQuantity = order.Items.Sum(i => i.Quantity);
        decimal totalWeight = order.Items.Sum(i =>
        {
            var p = products.FirstOrDefault(prod => prod.Id == i.ProductId);
            return (p?.WeightKg ?? 0m) * i.Quantity;
        });

        decimal shipping = 5.00m + (1.50m * totalQuantity) + (2.00m * totalWeight);
        order.ShippingCost = Math.Round(shipping, 2, MidpointRounding.AwayFromZero);
        return order.ShippingCost;
    }

    // Phase 3 method that calculates all money fields (8.1)
    public async Task<decimal> CalculateTotalAsync(Order order, CancellationToken ct = default)
    {
        await CalculateSubTotalAsync(order, ct);

        var discountTask = GetDiscountAsync(order, ct);
        var shippingTask = CalculateShippingCostAsync(order, ct);

        await discountTask;
        await shippingTask;

        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
        return order.TotalAmount;
    }
}

public class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(PricingService pricingService) => _pricingService = pricingService;

    public async Task PriceOrderAsync(Order order, CancellationToken ct = default)
    {
        await _pricingService.CalculateSubTotalAsync(order, ct);

        // Run discount and shipping calls at the same time: start both, await both one-by-one
        var discountTask = _pricingService.GetDiscountAsync(order, ct);
        var shippingTask = _pricingService.CalculateShippingCostAsync(order, ct);

        var discount = await discountTask;
        var shipping = await shippingTask;

        order.Discount = discount;
        order.ShippingCost = shipping;
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
    }

    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct = default)
    {
        await _pricingService.CalculateSubTotalAsync(order, ct);
        var discount = await _pricingService.GetDiscountAsync(order, ct);
        var shipping = await _pricingService.CalculateShippingCostAsync(order, ct);

        order.Discount = discount;
        order.ShippingCost = shipping;
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
    }
}
