using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public static class Phase2Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("=== Starting Phase 2 ===");
        var store = new JsonFileStore();

        // 1. ResetAsync() -> Race demo -> ResetAsync() again -> Pricing comparison on order #1
        await DataSeeder.ResetAsync(store, ct);
        await RaceConditionDemo.RunAsync(store, ct);

        await DataSeeder.ResetAsync(store, ct);

        // Pricing comparison on Order #1
        var incomingRepo = new IncomingOrderRepository(store);
        var orders = await incomingRepo.GetAllAsync(ct);
        var order1 = orders.First(o => o.Id == 1);

        var custRepo = new CustomerRepository(store);
        var prodRepo = new ProductRepository(store);
        var coupRepo = new CouponRepository(store);
        var pricingService = new PricingService(store, custRepo, coupRepo, failShippingForOrderIds: new[] { 7 });
        var orderPricer = new OrderPricer(pricingService);

        Console.WriteLine("\n--- Task 8.2: Pricing Order #1 Comparison (Concurrent vs Sequential) ---");
        var cloneForSeq = new Order
        {
            Id = order1.Id,
            CustomerId = order1.CustomerId,
            Items = order1.Items.Select(i => new OrderItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
            CouponCode = order1.CouponCode
        };

        var seqWatch = Stopwatch.StartNew();
        await orderPricer.PriceOrderSequentialAsync(cloneForSeq, ct);
        seqWatch.Stop();
        Console.WriteLine($"Sequential Pricing on Order #1: {seqWatch.Elapsed.TotalSeconds:F2}s " +
                          $"(SubTotal: {cloneForSeq.SubTotal}, Discount: {cloneForSeq.Discount}, Shipping: {cloneForSeq.ShippingCost}, Total: {cloneForSeq.TotalAmount})");

        var cloneForConc = new Order
        {
            Id = order1.Id,
            CustomerId = order1.CustomerId,
            Items = order1.Items.Select(i => new OrderItem { ProductId = i.ProductId, Quantity = i.Quantity }).ToList(),
            CouponCode = order1.CouponCode
        };

        var concWatch = Stopwatch.StartNew();
        await orderPricer.PriceOrderAsync(cloneForConc, ct);
        concWatch.Stop();
        Console.WriteLine($"Concurrent Pricing on Order #1: {concWatch.Elapsed.TotalSeconds:F2}s " +
                          $"(SubTotal: {cloneForConc.SubTotal}, Discount: {cloneForConc.Discount}, Shipping: {cloneForConc.ShippingCost}, Total: {cloneForConc.TotalAmount})");

        // 2. Validate all 20 orders concurrently and save each one
        Console.WriteLine("\n--- Step 2: Validate all 20 orders concurrently ---");
        var validator = new OrderValidator(custRepo, prodRepo, coupRepo);
        var orderRepo = new OrderRepository(store);

        var validationTasks = new List<Task>();
        foreach (var order in orders)
        {
            validationTasks.Add(Task.Run(async () =>
            {
                await validator.ValidateOrderAsync(order, ct);
                await orderRepo.SaveAsync(order, ct);
            }, ct));
        }

        // Rule: await tasks one by one
        foreach (var t in validationTasks)
            await t;

        // 3. Start ProcessValidatedOrderAsync for every Validated order into a List<Task<bool>>
        Console.WriteLine("\n--- Step 3: Process all validated orders concurrently ---");
        var invoiceGen = new InvoiceGenerator();
        var processor = new OrderProcessorV2(prodRepo, orderPricer, invoiceGen, orderRepo);

        var savedOrders = await orderRepo.GetAllAsync(ct);
        var validatedOrders = savedOrders.Where(o => o.Status == OrderStatus.Validated).OrderBy(o => o.Id).ToList();

        var processTasks = new List<Task<bool>>();
        var taskOrderMap = new List<(Task<bool> Task, Order Order)>();

        var totalProcessWatch = Stopwatch.StartNew();

        foreach (var valOrder in validatedOrders)
        {
            var task = processor.ProcessValidatedOrderAsync(valOrder, ct);
            processTasks.Add(task);
            taskOrderMap.Add((task, valOrder));
        }

        // Print Status of task[0] immediately (after creation)
        if (processTasks.Count > 0)
        {
            Console.WriteLine($"Status of task[0] immediately after creation: {processTasks[0].Status}");
        }

        // 4. await Task.Delay(1000) and print the Status of every task (while running)
        await Task.Delay(1000, ct);
        Console.WriteLine("\nStatus of every task while running (after 1000ms):");
        for (int i = 0; i < taskOrderMap.Count; i++)
        {
            Console.WriteLine($"Task for Order #{taskOrderMap[i].Order.Id}: {taskOrderMap[i].Task.Status}");
        }

        // 5. Await each task in a foreach. A failing order must not stop the others
        Console.WriteLine("\n--- Awaiting all processing tasks one by one ---");
        for (int i = 0; i < taskOrderMap.Count; i++)
        {
            var entry = taskOrderMap[i];
            try
            {
                await entry.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Order #{entry.Order.Id} failed with task status {entry.Task.Status}: {ex.Message}");
                entry.Order.Status = OrderStatus.Failed;
                entry.Order.FailureReason = ex.Message;

                // Release stock for failed order
                foreach (var item in entry.Order.Items)
                {
                    await prodRepo.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
                }

                await orderRepo.SaveAsync(entry.Order, ct);
            }
        }
        totalProcessWatch.Stop();

        // 6. Print final Status of every task and total time
        Console.WriteLine($"\nAll tasks completed. Total processing time: {totalProcessWatch.Elapsed.TotalSeconds:F2} seconds.");
        for (int i = 0; i < taskOrderMap.Count; i++)
        {
            Console.WriteLine($"Order #{taskOrderMap[i].Order.Id}: Final Task Status = {taskOrderMap[i].Task.Status}");
        }

        // 7. Read orders.json and confirm it has exactly 20 records
        var finalOrders = await orderRepo.GetAllAsync(ct);
        int completed = finalOrders.Count(o => o.Status == OrderStatus.Completed);
        int rejected = finalOrders.Count(o => o.Status == OrderStatus.Rejected);
        int failed = finalOrders.Count(o => o.Status == OrderStatus.Failed);

        Console.WriteLine("\n===== Phase 2 Final Status Breakdown =====");
        Console.WriteLine($"Total Records in orders.json: {finalOrders.Count} (Expected 20)");
        Console.WriteLine($"Completed: {completed} (Expected 13)");
        Console.WriteLine($"Rejected:  {rejected} (Expected 6)");
        Console.WriteLine($"Failed:    {failed} (Expected 1, #7)");

        // 8. Task 8.6 - Report generation
        Console.WriteLine("\n--- Task 8.6: Report Generation ---");
        var reportGen = new ReportGenerator(orderRepo, prodRepo);
        var reportTask = reportGen.GenerateAsync(ct);
        Console.WriteLine("Generating report...");
        var report = await reportTask;

        Console.WriteLine("\n===== Generated Report =====");
        Console.WriteLine($"Total Orders:        {report.TotalOrders}");
        Console.WriteLine($"Completed Count:     {report.CompletedCount}");
        Console.WriteLine($"Rejected Count:      {report.RejectedCount}");
        Console.WriteLine($"Failed Count:        {report.FailedCount}");
        Console.WriteLine($"Total Revenue:       ${report.TotalRevenue:F2}");
        Console.WriteLine($"Average Order Value: ${report.AverageOrderValue:F2}");
        Console.WriteLine($"Top Selling Product: {report.TopSellingProduct} ({report.TopSellingQuantity} units)");
    }
}
