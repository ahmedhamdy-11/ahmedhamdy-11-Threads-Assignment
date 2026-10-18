using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customerRepo;
    private readonly ProductRepository _productRepo;
    private readonly CouponRepository _couponRepo;

    public OrderValidator(CustomerRepository customerRepo, ProductRepository productRepo, CouponRepository couponRepo)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _couponRepo = couponRepo;
    }

    public async Task ValidateOrderAsync(Order order, CancellationToken ct = default)
    {
        // 1. Customer exists
        var customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
        if (customer == null)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = $"Customer #{order.CustomerId} not found";
            return;
        }

        // 2. Customer is active
        if (!customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = "Customer is inactive";
            return;
        }

        // 3 & 4. Every product exists, StockQuantity >= Quantity (checked, not reserved)
        foreach (var item in order.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
            if (product == null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Product #{item.ProductId} not found";
                return;
            }

            if (product.StockQuantity < item.Quantity)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{product.Name}'";
                return;
            }
        }

        // 5 & 6. Coupon validation (if not null)
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(order.CouponCode, ct);
            if (coupon == null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Coupon '{order.CouponCode}' does not exist";
                return;
            }

            if (coupon.ExpiryDate < DateTime.Today)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Coupon '{order.CouponCode}' has expired";
                return;
            }
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;
    }
}
