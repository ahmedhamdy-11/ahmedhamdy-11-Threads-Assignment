using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store) => _store = store;

    public Customer? GetById(int id)
    {
        Log.Info($"SyncCustomerRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncCustomerRepository -> Executing query for Customer #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var list = _store.ReadList<Customer>(DataFiles.Customers);
        return list.FirstOrDefault(c => c.Id == id);
    }
}

public class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store) => _store = store;

    public Product? GetById(int id)
    {
        Log.Info($"SyncProductRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncProductRepository -> Executing query for Product #{id}...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var list = _store.ReadList<Product>(DataFiles.Products);
        return list.FirstOrDefault(p => p.Id == id);
    }
}

public class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store) => _store = store;

    public Coupon? GetByCode(string code)
    {
        Log.Info($"SyncCouponRepository -> Opening connection...");
        Thread.Sleep(200);

        Log.Info($"SyncCouponRepository -> Executing query for coupon '{code}'...");
        Thread.Sleep(Random.Shared.Next(300, 601));

        var list = _store.ReadList<Coupon>(DataFiles.Coupons);
        return list.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

public class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customerRepo;
    private readonly SyncProductRepository _productRepo;
    private readonly SyncCouponRepository _couponRepo;

    public SyncOrderValidator(SyncCustomerRepository customerRepo, SyncProductRepository productRepo, SyncCouponRepository couponRepo)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _couponRepo = couponRepo;
    }

    public void ValidateOrder(Order order)
    {
        var customer = _customerRepo.GetById(order.CustomerId);
        if (customer == null)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = $"Customer #{order.CustomerId} not found";
            return;
        }

        if (!customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = "Customer is inactive";
            return;
        }

        foreach (var item in order.Items)
        {
            var product = _productRepo.GetById(item.ProductId);
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

        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = _couponRepo.GetByCode(order.CouponCode);
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
