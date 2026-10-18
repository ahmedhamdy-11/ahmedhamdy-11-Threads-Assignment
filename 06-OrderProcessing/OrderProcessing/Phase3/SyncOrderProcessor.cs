using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class SyncOrderProcessor
{
    private readonly JsonFileStore _store;

    public SyncOrderProcessor(JsonFileStore store) => _store = store;

    public bool ProcessOrder(Order order)
    {
        // 1. Customer
        Thread.Sleep(200);
        Thread.Sleep(400);
        var customers = _store.ReadList<Customer>(DataFiles.Customers);
        var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);
        if (customer == null || !customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = customer == null ? $"Customer #{order.CustomerId} not found" : "Customer is inactive";
            _store.UpdateList<Order>(DataFiles.Orders, list => { int idx = list.FindIndex(o => o.Id == order.Id); if (idx >= 0) list[idx] = order; else list.Add(order); });
            return false;
        }

        // 2. Stock check & reserve
        Thread.Sleep(200);
        Thread.Sleep(100);
        Thread.Sleep(400);
        bool stockOk = true;
        string failedProduct = "";
        _store.UpdateList<Product>(DataFiles.Products, products =>
        {
            foreach (var item in order.Items)
            {
                var p = products.FirstOrDefault(prod => prod.Id == item.ProductId);
                if (p == null || p.StockQuantity < item.Quantity)
                {
                    stockOk = false;
                    failedProduct = p?.Name ?? $"#{item.ProductId}";
                    return;
                }
            }
            if (stockOk)
            {
                foreach (var item in order.Items)
                {
                    var p = products.First(prod => prod.Id == item.ProductId);
                    p.StockQuantity -= item.Quantity;
                }
            }
        });
        Thread.Sleep(200);

        if (!stockOk)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = $"Insufficient stock for '{failedProduct}'";
            _store.UpdateList<Order>(DataFiles.Orders, list => { int idx = list.FindIndex(o => o.Id == order.Id); if (idx >= 0) list[idx] = order; else list.Add(order); });
            return false;
        }

        // 3. Pricing
        Thread.Sleep(200 + 400); // SubTotal
        var productsList = _store.ReadList<Product>(DataFiles.Products);
        order.SubTotal = order.Items.Sum(i => productsList.First(p => p.Id == i.ProductId).Price * i.Quantity);

        Thread.Sleep(150 + 300 + 600); // Discount
        int discountPct = customer.LoyaltyTier == LoyaltyTier.Gold ? 10 : (customer.LoyaltyTier == LoyaltyTier.Silver ? 5 : 0);
        if (!string.IsNullOrEmpty(order.CouponCode))
        {
            var coupons = _store.ReadList<Coupon>(DataFiles.Coupons);
            var coup = coupons.FirstOrDefault(c => c.Code == order.CouponCode);
            if (coup != null && coup.ExpiryDate >= DateTime.Today)
                discountPct += coup.DiscountPercent;
        }
        discountPct = Math.Min(30, discountPct);
        order.Discount = Math.Round(order.SubTotal * discountPct / 100m, 2, MidpointRounding.AwayFromZero);

        Thread.Sleep(150 + 300 + 600); // Shipping
        order.ShippingCost = order.SubTotal >= 1000m ? 0 : Math.Round(5.00m + 1.50m * order.Items.Sum(i => i.Quantity) + 2.00m * order.Items.Sum(i => productsList.First(p => p.Id == i.ProductId).WeightKg * i.Quantity), 2, MidpointRounding.AwayFromZero);
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);

        // 4. Payment
        Thread.Sleep(800 + 500 + 700);
        string txn = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        _store.UpdateList<PaymentTransaction>(DataFiles.Payments, list => list.Add(new()
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Captured,
            TransactionId = txn,
            CreatedAt = DateTime.Now
        }));

        // 5. DB Save
        Thread.Sleep(200 + 100 + 400 + 200);
        order.TransactionId = txn;
        order.Status = OrderStatus.Completed;
        _store.UpdateList<Order>(DataFiles.Orders, list => { int idx = list.FindIndex(o => o.Id == order.Id); if (idx >= 0) list[idx] = order; else list.Add(order); });

        // 6. Email
        Thread.Sleep(200 + 300 + 500);
        _store.UpdateList<EmailMessage>(DataFiles.Emails, list => list.Add(new()
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Total: ${order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = 1
        }));

        return true;
    }
}
