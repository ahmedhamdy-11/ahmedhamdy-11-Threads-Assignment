using System.Text.Json;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class ReportGenerator
{
    private readonly OrderRepository _orderRepo;
    private readonly ProductRepository _productRepo;
    private readonly string _reportsDir;

    public ReportGenerator(OrderRepository orderRepo, ProductRepository productRepo, string? dataDir = null)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
        string dir = dataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        _reportsDir = Path.Combine(dir, DataFiles.ReportsDir);
        Directory.CreateDirectory(_reportsDir);
    }

    public async Task<Report> GenerateAsync(CancellationToken ct = default)
    {
        var orders = await _orderRepo.GetAllAsync(ct);
        var products = await _productRepo.GetAllAsync(ct);

        int totalOrders = orders.Count;
        int completedCount = orders.Count(o => o.Status == OrderStatus.Completed);
        int rejectedCount = orders.Count(o => o.Status == OrderStatus.Rejected);
        int failedCount = orders.Count(o => o.Status == OrderStatus.Failed);
        int paymentFailedCount = orders.Count(o => o.Status == OrderStatus.PaymentFailed);

        decimal totalRevenue = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .Sum(o => o.TotalAmount);

        decimal averageOrderValue = completedCount > 0
            ? Math.Round(totalRevenue / completedCount, 2, MidpointRounding.AwayFromZero)
            : 0.00m;

        // TopSellingProduct = highest sum Quantity across Completed orders, ties -> lowest ProductId
        var completedOrders = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var productQuantities = products.Select(p => new
        {
            Product = p,
            TotalQty = completedOrders
                .SelectMany(o => o.Items)
                .Where(i => i.ProductId == p.Id)
                .Sum(i => i.Quantity)
        })
        .OrderByDescending(x => x.TotalQty)
        .ThenBy(x => x.Product.Id)
        .FirstOrDefault();

        string topProduct = productQuantities?.Product.Name ?? "None";
        int topQty = productQuantities?.TotalQty ?? 0;

        var report = new Report
        {
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            CompletedCount = completedCount,
            RejectedCount = rejectedCount,
            FailedCount = failedCount,
            PaymentFailedCount = paymentFailedCount,
            TopSellingProduct = topProduct,
            TopSellingQuantity = topQty,
            AverageOrderValue = averageOrderValue,
            GeneratedAt = DateTime.Now
        };

        string fileName = $"report_{report.GeneratedAt:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(_reportsDir, fileName);

        string json = JsonSerializer.Serialize(report, JsonFileStore.JsonOptions);
        await File.WriteAllTextAsync(filePath, json, ct);
        Log.Info($"ReportGenerator -> Saved report to {filePath}");

        return report;
    }
}
