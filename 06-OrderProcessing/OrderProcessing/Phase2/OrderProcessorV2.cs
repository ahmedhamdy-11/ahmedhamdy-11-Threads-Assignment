using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public class OrderProcessorV2
{
    private readonly ProductRepository _productRepo;
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly OrderRepository _orderRepo;

    public OrderProcessorV2(
        ProductRepository productRepo,
        OrderPricer orderPricer,
        InvoiceGenerator invoiceGenerator,
        OrderRepository orderRepo)
    {
        _productRepo = productRepo;
        _orderPricer = orderPricer;
        _invoiceGenerator = invoiceGenerator;
        _orderRepo = orderRepo;
    }

    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct = default)
    {
        var reservedItems = new List<OrderItem>();

        // 1. Reserve stock for each item in item order
        foreach (var item in order.Items)
        {
            bool reserved = await _productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!reserved)
            {
                // Roll back any already reserved items for this order
                foreach (var prev in reservedItems)
                {
                    await _productRepo.ReleaseStockAsync(prev.ProductId, prev.Quantity, ct);
                }

                var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
                string prodName = product?.Name ?? $"#{item.ProductId}";

                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{prodName}'";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }
            reservedItems.Add(item);
        }

        // 2. Price order (does not catch exceptions)
        try
        {
            await _orderPricer.PriceOrderAsync(order, ct);
        }
        catch
        {
            // If pricing throws, release reserved stock before bubbling up
            foreach (var item in reservedItems)
            {
                await _productRepo.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
            }
            throw;
        }

        // 3. Generate invoice
        await _invoiceGenerator.GenerateAsync(order, ct);

        // 4. Set Completed, save, return true
        order.Status = OrderStatus.Completed;
        await _orderRepo.SaveAsync(order, ct);
        return true;
    }
}
