using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    private readonly string _invoicesDir;

    public InvoiceGenerator(string? dataDir = null)
    {
        string dir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        _invoicesDir = Path.Combine(dir, DataFiles.InvoicesDir);
        Directory.CreateDirectory(_invoicesDir);
    }

    public async Task GenerateAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"InvoiceGenerator -> Rendering invoice PDF (CPU-bound) for Order #{order.Id}...");

        // Task 8.5: Use Task.Run() for the CPU-bound PDF rendering loop
        await Task.Run(() =>
        {
            long hash = 0;
            for (int i = 0; i < 20_000_000; i++)
            {
                if (ct.IsCancellationRequested)
                    ct.ThrowIfCancellationRequested();

                hash = (hash ^ i) + 1;
            }
        }, ct);

        string filePath = Path.Combine(_invoicesDir, $"invoice_{order.Id}.txt");
        string content =
            $"""
            ========================================
                       SWIFTBITE INVOICE
            ========================================
            Order ID:       {order.Id}
            Customer ID:    {order.CustomerId}
            Date:           {order.CreatedAt:yyyy-MM-dd HH:mm:ss}
            Status:         {order.Status}
            ----------------------------------------
            SubTotal:       ${order.SubTotal:F2}
            Discount:      -${order.Discount:F2}
            Shipping:       ${order.ShippingCost:F2}
            Total Amount:   ${order.TotalAmount:F2}
            ========================================
            """;

        await File.WriteAllTextAsync(filePath, content, ct);
        Log.Info($"InvoiceGenerator -> Wrote {filePath}");
    }
}
