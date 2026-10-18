using System.Diagnostics;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public static class Phase1Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Starting Phase 1 ===");
        var store = new JsonFileStore();

        // 1. Reset and run synchronous validation
        await DataSeeder.ResetAsync(store, ct);
        var incomingRepo = new IncomingOrderRepository(store);
        var orders = await incomingRepo.GetAllAsync(ct);

        var syncCustomerRepo = new SyncCustomerRepository(store);
        var syncProductRepo = new SyncProductRepository(store);
        var syncCouponRepo = new SyncCouponRepository(store);
        var syncValidator = new SyncOrderValidator(syncCustomerRepo, syncProductRepo, syncCouponRepo);

        Console.WriteLine("\n--- Starting Synchronous Sequential Validation (Results not saved) ---");
        var syncWatch = Stopwatch.StartNew();
        foreach (var order in orders)
        {
            syncValidator.ValidateOrder(order);
        }
        syncWatch.Stop();
        double syncSeconds = syncWatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Synchronous Validation completed in {syncSeconds:F1} seconds.");

        // 2. Reset again and run asynchronous sequential validation
        await DataSeeder.ResetAsync(store, ct);
        orders = await incomingRepo.GetAllAsync(ct);

        var customerRepo = new CustomerRepository(store);
        var productRepo = new ProductRepository(store);
        var couponRepo = new CouponRepository(store);
        var orderRepo = new OrderRepository(store);
        var asyncValidator = new OrderValidator(customerRepo, productRepo, couponRepo);

        Console.WriteLine("\n--- Starting Asynchronous Sequential Validation (Saving to orders.json) ---");
        var asyncWatch = Stopwatch.StartNew();
        foreach (var order in orders)
        {
            await asyncValidator.ValidateOrderAsync(order, ct);
            await orderRepo.SaveAsync(order, ct);
        }
        asyncWatch.Stop();
        double asyncSeconds = asyncWatch.Elapsed.TotalSeconds;
        Console.WriteLine($"Asynchronous Validation completed in {asyncSeconds:F1} seconds.");

        // 3. Print validation summary read back from orders.json
        var validatedOrders = await orderRepo.GetByStatusAsync(OrderStatus.Validated, ct);
        var rejectedOrders = await orderRepo.GetByStatusAsync(OrderStatus.Rejected, ct);
        var allOrders = await orderRepo.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {allOrders.Count}");
        Console.WriteLine($"Validated:    {validatedOrders.Count}");
        Console.WriteLine($"Rejected:     {rejectedOrders.Count}");

        foreach (var rej in rejectedOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine($" - Order #{rej.Id}: {rej.FailureReason}");
        }

        Console.WriteLine($"Sync Time:  {syncSeconds:F1} seconds");
        Console.WriteLine($"Async Time: {asyncSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {allOrders.Count} records.");
    }
}
