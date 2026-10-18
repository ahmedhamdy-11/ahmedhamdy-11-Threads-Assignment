using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public static class Phase3Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Starting Phase 3 ===");
        var store = new JsonFileStore();

        // --- Task 9.6: Sync vs Async Comparison ---
        Console.WriteLine("\n--- Task 9.6: Sync vs Async Comparison (Orders #1 - #10, Failures Disabled) ---");
        await RunComparisonAsync(store, ct);

        // --- Task 9.7: Fire-and-Forget Investigation ---
        await FireAndForgetDemo.RunDemoAsync(store, ct);

        // --- Task 9.3: Full Checkout Workflow for All 20 Orders ---
        Console.WriteLine("\n--- Task 9.3: Full Checkout Workflow (20 Orders with Configured Failures) ---");
        await DataSeeder.ResetAsync(store, ct);

        var incomingRepo = new IncomingOrderRepository(store);
        var orders = await incomingRepo.GetAllAsync(ct);

        var custRepo = new CustomerRepository(store);
        var prodRepo = new ProductRepository(store);
        var coupRepo = new CouponRepository(store);
        var orderRepo = new OrderRepository(store, failSaveForOrderIds: new[] { 13 });

        var custService = new CustomerService(custRepo);
        var invService = new InventoryService(prodRepo);
        var pricingService = new PricingService(store, custRepo, coupRepo);
        var paymentGateway = new PaymentGateway(store, timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 });
        var emailService = new EmailService(store, new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue });

        var processor = new OrderProcessor(
            custService,
            invService,
            pricingService,
            paymentGateway,
            orderRepo,
            emailService,
            prodRepo);

        var tasks = new List<Task<bool>>();
        var totalWatch = Stopwatch.StartNew();

        foreach (var order in orders)
        {
            tasks.Add(processor.ProcessOrderAsync(order, ct));
        }

        // Await each task one by one in accordance with Part 00 rule
        foreach (var t in tasks)
        {
            await t;
        }
        totalWatch.Stop();

        // Read back orders and print final table
        var finalOrders = (await orderRepo.GetAllAsync(ct)).OrderBy(o => o.Id).ToList();

        Console.WriteLine();
        Console.WriteLine($"Full checkout completed in {totalWatch.Elapsed.TotalSeconds:F2} seconds.");
        Console.WriteLine("---------------------------------------------------------------------------------------------------------");
        Console.WriteLine(string.Format("{0,-8} | {1,-15} | {2,-10} | {3}", "Order", "Status", "Total", "Reason"));
        Console.WriteLine("---------------------------------------------------------------------------------------------------------");
        foreach (var ord in finalOrders)
        {
            Console.WriteLine(string.Format("{0,-8} | {1,-15} | ${2,-9:F2} | {3}", 
                $"#{ord.Id}", ord.Status, ord.TotalAmount, ord.FailureReason ?? "-"));
        }
        Console.WriteLine("---------------------------------------------------------------------------------------------------------");

        // Verification of Task 9.4 expected end state
        var payments = await store.ReadListAsync<PaymentTransaction>(DataFiles.Payments, ct);
        var emails = await store.ReadListAsync<EmailMessage>(DataFiles.Emails, ct);
        var retries = await store.ReadListAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        var products = await store.ReadListAsync<Product>(DataFiles.Products, ct);

        Console.WriteLine("\n===== Task 9.4 Verification =====");
        Console.WriteLine($"orders.json:      {finalOrders.Count} records (Completed: {finalOrders.Count(o => o.Status == OrderStatus.Completed)}, " +
                          $"Rejected: {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}, " +
                          $"PaymentFailed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}, " +
                          $"Failed: {finalOrders.Count(o => o.Status == OrderStatus.Failed)})");
        Console.WriteLine($"payments.json:    {payments.Count} records (Captured: {payments.Count(p => p.Status == PaymentStatus.Captured)}, " +
                          $"Declined: {payments.Count(p => p.Status == PaymentStatus.Declined)}, " +
                          $"Refunded: {payments.Count(p => p.Status == PaymentStatus.Refunded)})");
        Console.WriteLine($"emails.json:      {emails.Count} records (Order #3 attempts: {emails.FirstOrDefault(e => e.OrderId == 3)?.Attempts})");
        Console.WriteLine($"email_retry.json: {retries.Count} records (Order #17 attempts: {retries.FirstOrDefault(e => e.OrderId == 17)?.Attempts})");
        Console.WriteLine($"products.json:    Keyboard stock = {products.First(p => p.Id == 4).StockQuantity}");
    }

    private static async Task RunComparisonAsync(JsonFileStore store, CancellationToken ct)
    {
        var incomingRepo = new IncomingOrderRepository(store);

        // --- Synchronous Run (10 Threads) ---
        await DataSeeder.ResetAsync(store, ct);
        var syncOrders = (await incomingRepo.GetAllAsync(ct)).Take(10).ToList();
        var syncProcessor = new SyncOrderProcessor(store);

        var syncThreads = new List<Thread>();
        var syncThreadIds = new System.Collections.Concurrent.ConcurrentBag<int>();

        int syncThreadPoolBefore = ThreadPool.ThreadCount;
        var syncWatch = Stopwatch.StartNew();

        for (int i = 0; i < syncOrders.Count; i++)
        {
            var ord = syncOrders[i];
            var th = new Thread(() =>
            {
                syncThreadIds.Add(Environment.CurrentManagedThreadId);
                syncProcessor.ProcessOrder(ord);
            });
            syncThreads.Add(th);
            th.Start();
        }

        foreach (var th in syncThreads)
            th.Join();

        syncWatch.Stop();
        int syncThreadPoolAfter = ThreadPool.ThreadCount;

        Console.WriteLine($"Sync Run (10 orders on 10 dedicated threads):");
        Console.WriteLine($"  Time: {syncWatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Distinct Threads Used: {syncThreadIds.Distinct().Count()}");
        Console.WriteLine($"  ThreadPool Count: {syncThreadPoolBefore} -> {syncThreadPoolAfter}");

        // --- Asynchronous Run (10 Tasks) ---
        await DataSeeder.ResetAsync(store, ct);
        var asyncOrders = (await incomingRepo.GetAllAsync(ct)).Take(10).ToList();

        var custRepo = new CustomerRepository(store);
        var prodRepo = new ProductRepository(store);
        var coupRepo = new CouponRepository(store);
        var orderRepo = new OrderRepository(store); // No failures injected

        var custService = new CustomerService(custRepo);
        var invService = new InventoryService(prodRepo);
        var pricingService = new PricingService(store, custRepo, coupRepo);
        var paymentGateway = new PaymentGateway(store); // No timeouts or declines
        var emailService = new EmailService(store); // No email failures

        var asyncProcessor = new OrderProcessor(
            custService,
            invService,
            pricingService,
            paymentGateway,
            orderRepo,
            emailService,
            prodRepo);

        var asyncThreadIds = new System.Collections.Concurrent.ConcurrentBag<int>();
        int asyncThreadPoolBefore = ThreadPool.ThreadCount;
        var asyncWatch = Stopwatch.StartNew();

        var asyncTasks = new List<Task>();
        foreach (var ord in asyncOrders)
        {
            asyncTasks.Add(Task.Run(async () =>
            {
                asyncThreadIds.Add(Environment.CurrentManagedThreadId);
                await asyncProcessor.ProcessOrderAsync(ord, ct);
            }, ct));
        }

        foreach (var t in asyncTasks)
            await t;

        asyncWatch.Stop();
        int asyncThreadPoolAfter = ThreadPool.ThreadCount;

        Console.WriteLine($"Async Run (10 orders on asynchronous tasks):");
        Console.WriteLine($"  Time: {asyncWatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Distinct Threads Used: {asyncThreadIds.Distinct().Count()}");
        Console.WriteLine($"  ThreadPool Count: {asyncThreadPoolBefore} -> {asyncThreadPoolAfter}");
    }
}
