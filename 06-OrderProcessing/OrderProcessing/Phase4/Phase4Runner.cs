using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public static class Phase4Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Starting Phase 4 ===");
        var store = new JsonFileStore();

        // --- Task 10.1: Live Checkout Progress with Task.WhenEach ---
        await RunTask101Async(store, ct);

        // --- Task 10.2: Customer Dashboard ---
        await RunTask102Async(store, ct);

        // --- Task 10.3: Fastest Shipping Quote ---
        await RunTask103Async(store, ct);

        // --- Task 10.4: Fraud Scoring & Email Re-send ---
        await RunTask104Async(store, ct);
    }

    public static async Task RunTask101Async(JsonFileStore store, CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.1: Live Checkout Progress (Real-Time using Task.WhenEach) =====");
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

        var orderTasks = new List<Task<(Order Order, double ElapsedSeconds)>>();
        var sw = Stopwatch.StartNew();

        foreach (var ord in orders)
        {
            orderTasks.Add(Task.Run(async () =>
            {
                var orderSw = Stopwatch.StartNew();
                await processor.ProcessOrderAsync(ord, ct);
                orderSw.Stop();
                return (ord, orderSw.Elapsed.TotalSeconds);
            }, ct));
        }

        // R2 & R4: Use Task.WhenEach to stream tasks the moment they finish with O(1) efficiency
        int counter = 0;
        await foreach (var completedTask in Task.WhenEach(orderTasks))
        {
            counter++;
            var result = await completedTask;
            string reasonPart = !string.IsNullOrEmpty(result.Order.FailureReason) 
                ? $" ({result.Order.FailureReason})" 
                : "";
            Console.WriteLine($"[ {counter,2}/20] Order #{result.Order.Id,2} -> {result.Order.Status}{reasonPart} after {result.ElapsedSeconds:F1} s");
        }

        sw.Stop();
        Console.WriteLine($"\nAll 20 orders completed in {sw.Elapsed.TotalSeconds:F2} s.");

        var finalOrders = await orderRepo.GetAllAsync(ct);
        Console.WriteLine("\nStatus breakdown (must match Task 9.4):");
        Console.WriteLine($"  Completed:     {finalOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine($"  Rejected:      {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine($"  PaymentFailed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine($"  Failed:        {finalOrders.Count(o => o.Status == OrderStatus.Failed)}");
    }

    private static async Task RunTask102Async(JsonFileStore store, CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.2: Customer Dashboard (Parallel Loads with Task.WhenAll) =====");
        var dashboardService = new CustomerDashboardService(store, failPaymentsForCustomerIds: new[] { 99 });

        // Run for Customer #3
        Console.WriteLine("\n--- Customer #3 Dashboard ---");
        var sw3 = Stopwatch.StartNew();
        try
        {
            var dash3 = await dashboardService.LoadAsync(3, ct);
            sw3.Stop();
            Console.WriteLine($"Loaded Customer #3 dashboard in {sw3.ElapsedMilliseconds} ms (Single slowest load time):");
            Console.WriteLine($"  Customer: {dash3.Customer.Name} ({dash3.Customer.Email})");
            Console.WriteLine($"  Orders:   {dash3.Orders.Count} orders ({string.Join(", ", dash3.Orders.Select(o => $"#{o.Id} {o.Status}"))})");
            Console.WriteLine($"  Payments: {dash3.Payments.Count} payments ({string.Join(", ", dash3.Payments.Select(p => $"#{p.OrderId} {p.Status}"))})");
            Console.WriteLine($"  Emails:   {dash3.Emails.Count} email(s) ({string.Join(", ", dash3.Emails.Select(e => $"#{e.OrderId}"))})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Customer #3: {ex.Message}");
        }

        // Run for Customer #99 (Multiple Failures)
        Console.WriteLine("\n--- Customer #99 Dashboard (Expected Multiple Failures) ---");
        var sw99 = Stopwatch.StartNew();
        try
        {
            await dashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException ex)
        {
            sw99.Stop();
            Console.WriteLine($"Two failures printed for Customer #99 (in {sw99.ElapsedMilliseconds} ms):");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"  - {inner.Message}");
            }
        }
    }

    private static async Task RunTask103Async(JsonFileStore store, CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.3: Fastest Shipping Quote =====");

        var carriers = new List<IShippingCarrier>
        {
            new BaseShippingCarrier("FastShip", 500, 1500, 1.00m),
            new BaseShippingCarrier("CairoExpress", 300, 2000, 0.95m, failForOrderIds: new[] { 2, 4, 6 }),
            new BaseShippingCarrier("NileCourier", 800, 1200, 1.10m)
        };

        var quoteService = new ShippingQuoteService(carriers);
        var incomingRepo = new IncomingOrderRepository(store);
        var orders = (await incomingRepo.GetAllAsync(ct)).Take(5).ToList();

        Console.WriteLine("\n--- Quotes for Orders #1 - #5 (Max Budget 1.2s) ---");
        foreach (var order in orders)
        {
            try
            {
                var quote = await quoteService.GetFastestQuoteAsync(order, ct);
                Console.WriteLine($"Order #{order.Id}: Winning Carrier = {quote.Carrier,-12} | Price = ${quote.Price,6:F2} | Time = {quote.ElapsedMs} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Order #{order.Id}: Failed -> {ex.Message}");
            }
        }

        Console.WriteLine("\n--- All Carriers Fail Demo (Order #6 with all carriers failing) ---");
        var failingCarriers = new List<IShippingCarrier>
        {
            new BaseShippingCarrier("FastShip", 200, 400, 1.00m, failForOrderIds: new[] { 6 }),
            new BaseShippingCarrier("CairoExpress", 200, 400, 0.95m, failForOrderIds: new[] { 6 }),
            new BaseShippingCarrier("NileCourier", 200, 400, 1.10m, failForOrderIds: new[] { 6 })
        };
        var allFailService = new ShippingQuoteService(failingCarriers);
        var order6 = new Order { Id = 6, Items = [new() { ProductId = 5, Quantity = 1 }] };

        try
        {
            await allFailService.GetFastestQuoteAsync(order6, ct);
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Console.WriteLine($"Order #6 successfully caught expected exception: {ex.Message}");
        }
    }

    private static async Task RunTask104Async(JsonFileStore store, CancellationToken ct)
    {
        Console.WriteLine("\n===== Task 10.4: Part A — Fraud Scoring (CPU-Bound) =====");
        const int totalOrders = 10_000;

        // R1: Score all 10,000 sequentially
        var seqWatch = Stopwatch.StartNew();
        var seqFlagged = new List<int>();
        for (int i = 1; i <= totalOrders; i++)
        {
            if (Score(i) >= 90)
                seqFlagged.Add(i);
        }
        seqWatch.Stop();

        // R2: Score across all CPU cores
        var parWatch = Stopwatch.StartNew();
        var parFlaggedBag = new ConcurrentBag<int>();
        var distinctThreads = new ConcurrentDictionary<int, byte>();

        Parallel.For(1, totalOrders + 1, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        }, i =>
        {
            distinctThreads.TryAdd(Environment.CurrentManagedThreadId, 0);
            if (Score(i) >= 90)
            {
                parFlaggedBag.Add(i);
            }
        });
        parWatch.Stop();

        var parFlagged = parFlaggedBag.OrderBy(id => id).ToList();

        double speedUp = seqWatch.Elapsed.TotalMilliseconds / Math.Max(1, parWatch.Elapsed.TotalMilliseconds);
        Console.WriteLine($"Sequential Time:       {seqWatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Parallel Time:         {parWatch.ElapsedMilliseconds} ms (Speed-up: {speedUp:F1}x)");
        Console.WriteLine($"Flagged Orders Count:  {parFlagged.Count} (Sequential: {seqFlagged.Count})");
        Console.WriteLine($"First 5 Flagged IDs:   {string.Join(", ", parFlagged.Take(5))}");
        Console.WriteLine($"Distinct Threads Used: {distinctThreads.Count}");
        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");

        Console.WriteLine("\n===== Task 10.4: Part B — Email Re-Send (I/O-Bound with Max 4 Concurrency) =====");
        var testEmails = Enumerable.Range(1001, 30).Select(id => new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = id,
            To = $"customer{id}@example.com",
            Subject = $"Re-sent order {id}",
            Body = "Your order update",
            SentAt = DateTime.Now,
            Attempts = 1
        }).ToList();

        int currentSends = 0;
        int maxSimultaneousSends = 0;
        var emailService = new EmailService(store); // No failures injected

        var emailWatch = Stopwatch.StartNew();

        // R1 & R2: Send all 30 with at most 4 sends running at the same time using Parallel.ForEachAsync
        await Parallel.ForEachAsync(testEmails, new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        }, async (email, token) =>
        {
            int running = Interlocked.Increment(ref currentSends);
            // Track highest concurrent sends atomically
            int initialMax;
            do
            {
                initialMax = maxSimultaneousSends;
                if (running <= initialMax) break;
            } while (Interlocked.CompareExchange(ref maxSimultaneousSends, running, initialMax) != initialMax);

            try
            {
                var dummyOrder = new Order { Id = email.OrderId, TotalAmount = 50m };
                var dummyCustomer = new Customer { Email = email.To };
                await emailService.SendConfirmationAsync(dummyOrder, dummyCustomer, token);
            }
            finally
            {
                Interlocked.Decrement(ref currentSends);
            }
        });

        emailWatch.Stop();

        Console.WriteLine($"Total Time for 30 emails: {emailWatch.Elapsed.TotalSeconds:F2} s (Expected ~8 s)");
        Console.WriteLine($"Highest Simultaneous Sends: {maxSimultaneousSends} (Must be exactly 4)");
        var emailsInFile = await store.ReadListAsync<EmailMessage>(DataFiles.Emails, ct);
        int testCountInFile = emailsInFile.Count(e => e.OrderId >= 1001 && e.OrderId <= 1030);
        Console.WriteLine($"Test Emails in emails.json: {testCountInFile}/30");
    }

    private static int Score(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
            h = (h * 31 + i) % 1_000_000_007;
        return (int)(h % 100);
    }
}
