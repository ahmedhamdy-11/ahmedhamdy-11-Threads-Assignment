using OrderProcessing.Shared.Models;

namespace OrderProcessing.Shared.Storage;

public static class DataSeeder
{
    public static async Task ResetAsync(JsonFileStore? store = null, CancellationToken ct = default)
    {
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(dataDir))
        {
            try
            {
                Directory.Delete(dataDir, recursive: true);
            }
            catch
            {
                // If files are briefly in use, clear items
                foreach (var file in Directory.GetFiles(dataDir))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, DataFiles.InvoicesDir));
        Directory.CreateDirectory(Path.Combine(dataDir, DataFiles.ReportsDir));

        store ??= new JsonFileStore(dataDir);

        var customers = GetSeedCustomers();
        await store.WriteListAsync(DataFiles.Customers, customers, ct);
        Console.WriteLine($"[Seeder] {DataFiles.Customers} -> {customers.Count} records");

        var products = GetSeedProducts();
        await store.WriteListAsync(DataFiles.Products, products, ct);
        Console.WriteLine($"[Seeder] {DataFiles.Products} -> {products.Count} records");

        var coupons = GetSeedCoupons();
        await store.WriteListAsync(DataFiles.Coupons, coupons, ct);
        Console.WriteLine($"[Seeder] {DataFiles.Coupons} -> {coupons.Count} records");

        var incomingOrders = GetSeedIncomingOrders();
        await store.WriteListAsync(DataFiles.IncomingOrders, incomingOrders, ct);
        Console.WriteLine($"[Seeder] {DataFiles.IncomingOrders} -> {incomingOrders.Count} records");

        await store.WriteListAsync<Order>(DataFiles.Orders, new(), ct);
        Console.WriteLine($"[Seeder] {DataFiles.Orders} -> 0 records");

        await store.WriteListAsync<PaymentTransaction>(DataFiles.Payments, new(), ct);
        Console.WriteLine($"[Seeder] {DataFiles.Payments} -> 0 records");

        await store.WriteListAsync<EmailMessage>(DataFiles.Emails, new(), ct);
        Console.WriteLine($"[Seeder] {DataFiles.Emails} -> 0 records");

        await store.WriteListAsync<EmailMessage>(DataFiles.EmailRetry, new(), ct);
        Console.WriteLine($"[Seeder] {DataFiles.EmailRetry} -> 0 records");
    }

    public static void Reset(JsonFileStore? store = null)
    {
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (Directory.Exists(dataDir))
        {
            try
            {
                Directory.Delete(dataDir, recursive: true);
            }
            catch
            {
                foreach (var file in Directory.GetFiles(dataDir))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, DataFiles.InvoicesDir));
        Directory.CreateDirectory(Path.Combine(dataDir, DataFiles.ReportsDir));

        store ??= new JsonFileStore(dataDir);

        var customers = GetSeedCustomers();
        store.WriteList(DataFiles.Customers, customers);
        Console.WriteLine($"[Seeder] {DataFiles.Customers} -> {customers.Count} records");

        var products = GetSeedProducts();
        store.WriteList(DataFiles.Products, products);
        Console.WriteLine($"[Seeder] {DataFiles.Products} -> {products.Count} records");

        var coupons = GetSeedCoupons();
        store.WriteList(DataFiles.Coupons, coupons);
        Console.WriteLine($"[Seeder] {DataFiles.Coupons} -> {coupons.Count} records");

        var incomingOrders = GetSeedIncomingOrders();
        store.WriteList(DataFiles.IncomingOrders, incomingOrders);
        Console.WriteLine($"[Seeder] {DataFiles.IncomingOrders} -> {incomingOrders.Count} records");

        store.WriteList<Order>(DataFiles.Orders, new());
        Console.WriteLine($"[Seeder] {DataFiles.Orders} -> 0 records");

        store.WriteList<PaymentTransaction>(DataFiles.Payments, new());
        Console.WriteLine($"[Seeder] {DataFiles.Payments} -> 0 records");

        store.WriteList<EmailMessage>(DataFiles.Emails, new());
        Console.WriteLine($"[Seeder] {DataFiles.Emails} -> 0 records");

        store.WriteList<EmailMessage>(DataFiles.EmailRetry, new());
        Console.WriteLine($"[Seeder] {DataFiles.EmailRetry} -> 0 records");
    }

    public static List<Customer> GetSeedCustomers() =>
    [
        new() { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 2, Name = "Sara Mohamed", Email = "sara.mohamed@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 3, Name = "Omar Hassan", Email = "omar.hassan@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 4, Name = "Mona Ibrahim", Email = "mona.ibrahim@example.com", IsActive = false, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 5, Name = "Youssef Adel", Email = "youssef.adel@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 6, Name = "Nour Khaled", Email = "nour.khaled@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 7, Name = "Karim Samir", Email = "karim.samir@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
        new() { Id = 8, Name = "Laila Mahmoud", Email = "laila.mahmoud@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
        new() { Id = 9, Name = "Hassan Tarek", Email = "hassan.tarek@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
        new() { Id = 10, Name = "Dina Fathy", Email = "dina.fathy@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
    ];

    public static List<Product> GetSeedProducts() =>
    [
        new() { Id = 1, Name = "Laptop", Price = 1200.00m, StockQuantity = 10, WeightKg = 2.2m },
        new() { Id = 2, Name = "Smartphone", Price = 800.00m, StockQuantity = 15, WeightKg = 0.4m },
        new() { Id = 3, Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 2, WeightKg = 0.1m },
        new() { Id = 4, Name = "Mechanical Keyboard", Price = 90.00m, StockQuantity = 1, WeightKg = 0.9m },
        new() { Id = 5, Name = "USB-C Hub", Price = 45.00m, StockQuantity = 30, WeightKg = 0.2m },
        new() { Id = 6, Name = "Headphones", Price = 150.00m, StockQuantity = 12, WeightKg = 0.3m },
        new() { Id = 7, Name = "27\" Monitor", Price = 350.00m, StockQuantity = 6, WeightKg = 5.0m },
        new() { Id = 8, Name = "Webcam", Price = 70.00m, StockQuantity = 20, WeightKg = 0.2m },
    ];

    public static List<Coupon> GetSeedCoupons() =>
    [
        new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = DateTime.Today.AddDays(180) },
        new() { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = DateTime.Today.AddDays(90) },
        new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(7) },
        new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(-30) },
    ];

    public static List<Order> GetSeedIncomingOrders()
    {
        DateTime now = DateTime.Now;
        return
        [
            new() { Id = 1, CustomerId = 1, Items = [new() { ProductId = 1, Quantity = 1 }, new() { ProductId = 3, Quantity = 1 }], CouponCode = "SAVE10", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 2, CustomerId = 2, Items = [new() { ProductId = 2, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 3, CustomerId = 3, Items = [new() { ProductId = 5, Quantity = 2 }, new() { ProductId = 8, Quantity = 1 }], CouponCode = "WELCOME15", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 4, CustomerId = 4, Items = [new() { ProductId = 6, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 5, CustomerId = 5, Items = [new() { ProductId = 7, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 6, CustomerId = 6, Items = [new() { ProductId = 5, Quantity = 1 }], CouponCode = "FLASH20", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 7, CustomerId = 7, Items = [new() { ProductId = 6, Quantity = 1 }, new() { ProductId = 8, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 8, CustomerId = 8, Items = [new() { ProductId = 1, Quantity = 1 }], CouponCode = "SAVE10", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 9, CustomerId = 9, Items = [new() { ProductId = 3, Quantity = 5 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 10, CustomerId = 10, Items = [new() { ProductId = 4, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 11, CustomerId = 1, Items = [new() { ProductId = 4, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 12, CustomerId = 2, Items = [new() { ProductId = 2, Quantity = 1 }], CouponCode = "OLD20", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 13, CustomerId = 3, Items = [new() { ProductId = 6, Quantity = 2 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 14, CustomerId = 5, Items = [new() { ProductId = 8, Quantity = 2 }, new() { ProductId = 5, Quantity = 1 }], CouponCode = "WELCOME15", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 15, CustomerId = 99, Items = [new() { ProductId = 2, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 16, CustomerId = 6, Items = [new() { ProductId = 7, Quantity = 1 }, new() { ProductId = 5, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 17, CustomerId = 7, Items = [new() { ProductId = 2, Quantity = 1 }], CouponCode = "SAVE10", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 18, CustomerId = 8, Items = [new() { ProductId = 6, Quantity = 1 }], CouponCode = "FAKE50", Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 19, CustomerId = 9, Items = [new() { ProductId = 8, Quantity = 1 }], CouponCode = null, Status = OrderStatus.Pending, CreatedAt = now },
            new() { Id = 20, CustomerId = 10, Items = [new() { ProductId = 1, Quantity = 1 }, new() { ProductId = 6, Quantity = 1 }], CouponCode = "FLASH20", Status = OrderStatus.Pending, CreatedAt = now },
        ];
    }
}
