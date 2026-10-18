using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly OrderRepository _orderRepo;
    private readonly IEmailService _emailService;
    private readonly ProductRepository _productRepo;

    public OrderProcessor(
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        OrderRepository orderRepo,
        IEmailService emailService,
        ProductRepository productRepo)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepo = orderRepo;
        _emailService = emailService;
        _productRepo = productRepo;
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct = default)
    {
        // Check if run cancellation was requested before starting
        if (ct.IsCancellationRequested)
        {
            await HandleCancellationAsync(order, false, null, ct);
            return false;
        }

        Customer? customer = null;
        bool stockReserved = false;
        string? capturedTxnId = null;

        try
        {
            // --- Step 1: Customer Verification ---
            var step1Watch = Stopwatch.StartNew();
            try
            {
                customer = await _customerService.GetCustomerAsync(order.CustomerId, ct);
                if (!customer.IsActive)
                {
                    step1Watch.Stop();
                    Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... INACTIVE ({step1Watch.ElapsedMilliseconds} ms)");
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = "Customer is inactive";
                    await _orderRepo.SaveAsync(order, ct);
                    return false;
                }
                step1Watch.Stop();
                Log.Info($"Order #{order.Id} -> Step 1/6 Customer ... OK ({customer.Name}) in {step1Watch.ElapsedMilliseconds} ms");
            }
            catch (CustomerNotFoundException ex)
            {
                step1Watch.Stop();
                Log.Error($"Order #{order.Id} -> Step 1/6 Customer ... NOT FOUND ({step1Watch.ElapsedMilliseconds} ms)");
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (ct.IsCancellationRequested)
            {
                await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
                return false;
            }

            // --- Step 2: Reserve Stock ---
            var step2Watch = Stopwatch.StartNew();
            bool reserved = await _inventoryService.ReserveStockAsync(order, ct);
            step2Watch.Stop();

            if (!reserved)
            {
                // Find which item failed for the exact error message
                string failedProductName = "Unknown";
                foreach (var item in order.Items)
                {
                    var p = await _productRepo.GetByIdAsync(item.ProductId, ct);
                    if (p == null || p.StockQuantity < item.Quantity)
                    {
                        failedProductName = p?.Name ?? $"#{item.ProductId}";
                        break;
                    }
                }
                Log.Error($"Order #{order.Id} -> Step 2/6 Stock ... OUT OF STOCK ({step2Watch.ElapsedMilliseconds} ms)");
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{failedProductName}'";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            stockReserved = true;
            Log.Info($"Order #{order.Id} -> Step 2/6 Stock ... RESERVED in {step2Watch.ElapsedMilliseconds} ms");

            if (ct.IsCancellationRequested)
            {
                await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
                return false;
            }

            // --- Step 3: Calculate Pricing ---
            var step3Watch = Stopwatch.StartNew();
            try
            {
                await _pricingService.CalculateTotalAsync(order, ct);
                step3Watch.Stop();
                Log.Info($"Order #{order.Id} -> Step 3/6 Pricing ... OK (Total: ${order.TotalAmount:F2}) in {step3Watch.ElapsedMilliseconds} ms");
            }
            catch (InvalidCouponException ex)
            {
                step3Watch.Stop();
                Log.Error($"Order #{order.Id} -> Step 3/6 Pricing ... INVALID COUPON ({ex.Message}) in {step3Watch.ElapsedMilliseconds} ms");
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (ct.IsCancellationRequested)
            {
                await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
                return false;
            }

            // --- Step 4: Process Payment (3s timeout or run cancellation) ---
            var step4Watch = Stopwatch.StartNew();
            PaymentResult paymentResult;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(order.Id, order.TotalAmount, timeoutCts.Token);
                step4Watch.Stop();
            }
            catch (OperationCanceledException)
            {
                step4Watch.Stop();
                if (ct.IsCancellationRequested)
                {
                    // Run cancellation has higher priority
                    await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
                    return false;
                }

                // 3s Payment gateway timeout
                Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... TIMEOUT after {step4Watch.ElapsedMilliseconds} ms, released");
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (!paymentResult.Success)
            {
                Log.Error($"Order #{order.Id} -> Step 4/6 Payment ... DECLINED ({paymentResult.Message}) in {step4Watch.ElapsedMilliseconds} ms");
                await _inventoryService.ReleaseStockAsync(order, ct);
                stockReserved = false;

                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            capturedTxnId = paymentResult.TransactionId;
            Log.Success($"Order #{order.Id} -> Step 4/6 Payment ... OK ({capturedTxnId}) in {step4Watch.ElapsedMilliseconds} ms");

            if (ct.IsCancellationRequested)
            {
                await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
                return false;
            }

            // --- Step 5: Save Completed Order with Compensation ---
            var step5Watch = Stopwatch.StartNew();
            order.TransactionId = capturedTxnId;
            order.Status = OrderStatus.Completed;

            try
            {
                await _orderRepo.SaveAsync(order, ct);
                step5Watch.Stop();
                Log.Info($"Order #{order.Id} -> Step 5/6 DB Save ... SAVED in {step5Watch.ElapsedMilliseconds} ms");
            }
            catch (DatabaseWriteException)
            {
                step5Watch.Stop();
                Log.Error($"Order #{order.Id} -> Step 5/6 DB Save ... FAILED! Compensating...");

                if (capturedTxnId != null)
                {
                    await _paymentGateway.RefundAsync(capturedTxnId, ct);
                }
                if (stockReserved)
                {
                    await _inventoryService.ReleaseStockAsync(order, ct);
                    stockReserved = false;
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = $"Database save failed after payment; payment {capturedTxnId} refunded";

                // Save failed status using direct store update to avoid failing repository wrapper
                var store = new JsonFileStore();
                await store.UpdateListAsync<Order>(DataFiles.Orders, list =>
                {
                    int idx = list.FindIndex(o => o.Id == order.Id);
                    if (idx >= 0) list[idx] = order; else list.Add(order);
                }, ct);

                return false;
            }

            if (ct.IsCancellationRequested)
            {
                await HandleCancellationAsync(order, false, capturedTxnId, ct);
                return false;
            }

            // --- Step 6: Send Email with Retry ---
            var step6Watch = Stopwatch.StartNew();
            if (customer != null)
            {
                await _emailService.SendConfirmationWithRetryAsync(order, customer, ct);
            }
            step6Watch.Stop();
            Log.Info($"Order #{order.Id} -> Step 6/6 Email ... Finished in {step6Watch.ElapsedMilliseconds} ms");

            return order.Status == OrderStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            await HandleCancellationAsync(order, stockReserved, capturedTxnId, ct);
            return false;
        }
    }

    private async Task HandleCancellationAsync(Order order, bool stockReserved, string? capturedTxnId, CancellationToken ct)
    {
        Log.Info($"Order #{order.Id} -> Cancelling in-progress order...");

        if (capturedTxnId != null)
        {
            try { await _paymentGateway.RefundAsync(capturedTxnId, CancellationToken.None); } catch { }
        }

        if (stockReserved)
        {
            try { await _inventoryService.ReleaseStockAsync(order, CancellationToken.None); } catch { }
        }

        order.Status = OrderStatus.Pending;
        order.FailureReason = "Cancelled";

        var store = new JsonFileStore();
        try
        {
            await store.UpdateListAsync<Order>(DataFiles.Orders, list =>
            {
                int idx = list.FindIndex(o => o.Id == order.Id);
                if (idx >= 0) list[idx] = order; else list.Add(order);
            }, CancellationToken.None);
        }
        catch { }
    }
}
