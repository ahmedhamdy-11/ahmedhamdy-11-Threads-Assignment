using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct = default);
    Task RefundAsync(string transactionId, CancellationToken ct = default);
}

public interface IEmailService
{
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default);
    Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct = default);
}

public class CustomerService
{
    private readonly CustomerRepository _customerRepo;

    public CustomerService(CustomerRepository customerRepo) => _customerRepo = customerRepo;

    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct = default)
    {
        var customer = await _customerRepo.GetByIdAsync(id, ct);
        if (customer == null)
            throw new CustomerNotFoundException($"Customer #{id} not found");

        return customer;
    }
}

public class InventoryService
{
    private readonly ProductRepository _productRepo;

    public InventoryService(ProductRepository productRepo) => _productRepo = productRepo;

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct = default)
    {
        var reserved = new List<OrderItem>();
        foreach (var item in order.Items)
        {
            bool ok = await _productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!ok)
            {
                foreach (var prev in reserved)
                {
                    await _productRepo.ReleaseStockAsync(prev.ProductId, prev.Quantity, ct);
                }
                return false;
            }
            reserved.Add(item);
        }
        return true;
    }

    public async Task ReleaseStockAsync(Order order, CancellationToken ct = default)
    {
        foreach (var item in order.Items)
        {
            await _productRepo.ReleaseStockAsync(item.ProductId, item.Quantity, ct);
        }
    }
}

public class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    public PaymentGateway(
        JsonFileStore store,
        IEnumerable<int>? timeoutOrderIds = null,
        IEnumerable<int>? declineOrderIds = null)
    {
        _store = store;
        _timeoutOrderIds = timeoutOrderIds != null ? new HashSet<int>(timeoutOrderIds) : new HashSet<int>();
        _declineOrderIds = declineOrderIds != null ? new HashSet<int>(declineOrderIds) : new HashSet<int>();
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct = default)
    {
        Log.Info($"PaymentGateway -> Authorizing (800ms) for Order #{orderId}...");
        await Task.Delay(800, ct);

        Log.Info($"PaymentGateway -> Fraud check (500ms) for Order #{orderId}...");
        await Task.Delay(500, ct);

        if (_declineOrderIds.Contains(orderId))
        {
            string decTxn = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
            var decTransaction = new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Declined,
                TransactionId = decTxn,
                CreatedAt = DateTime.Now
            };

            await _store.UpdateListAsync<PaymentTransaction>(DataFiles.Payments, list => list.Add(decTransaction), ct);

            return new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Declined,
                TransactionId = decTxn,
                Message = "Card declined by issuer"
            };
        }

        int captureDelay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;
        Log.Info($"PaymentGateway -> Capturing payment ({captureDelay}ms) for Order #{orderId}...");
        await Task.Delay(captureDelay, ct);

        string txnId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        var transaction = new PaymentTransaction
        {
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Captured,
            TransactionId = txnId,
            CreatedAt = DateTime.Now
        };

        await _store.UpdateListAsync<PaymentTransaction>(DataFiles.Payments, list => list.Add(transaction), ct);

        return new PaymentResult
        {
            Success = true,
            TransactionId = txnId,
            Status = PaymentStatus.Captured,
            Message = "Payment captured successfully"
        };
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct = default)
    {
        Log.Info($"PaymentGateway -> External API refund DNS/handshake (150ms) for {transactionId}...");
        await Task.Delay(150, ct);

        Log.Info($"PaymentGateway -> Sending refund request (300ms) for {transactionId}...");
        await Task.Delay(300, ct);

        Log.Info($"PaymentGateway -> Waiting for refund confirmation (500ms) for {transactionId}...");
        await Task.Delay(500, ct);

        await _store.UpdateListAsync<PaymentTransaction>(DataFiles.Payments, list =>
        {
            var txn = list.FirstOrDefault(t => t.TransactionId == transactionId);
            if (txn != null)
            {
                txn.Status = PaymentStatus.Refunded;
            }
        }, ct);

        Log.Info($"PaymentGateway -> Payment {transactionId} successfully Refunded.");
    }
}

public class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly Dictionary<int, int> _failAttempts;
    private readonly Dictionary<int, int> _currentAttemptCount = new();
    private readonly object _lock = new();

    public EmailService(JsonFileStore store, Dictionary<int, int>? failAttempts = null)
    {
        _store = store;
        _failAttempts = failAttempts != null ? new Dictionary<int, int>(failAttempts) : new Dictionary<int, int>();
    }

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        Log.Info($"EmailService -> Rendering template (200ms) for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"EmailService -> Connecting to SMTP server (300ms) for Order #{order.Id}...");
        await Task.Delay(300, ct);

        int currentAttempt;
        lock (_lock)
        {
            _currentAttemptCount.TryGetValue(order.Id, out int attempts);
            attempts++;
            _currentAttemptCount[order.Id] = attempts;
            currentAttempt = attempts;
        }

        if (_failAttempts.TryGetValue(order.Id, out int configuredFailures) && currentAttempt <= configuredFailures)
        {
            Log.Error($"EmailService -> SMTP connection error on attempt {currentAttempt} for Order #{order.Id}!");
            throw new SmtpUnavailableException($"SMTP server unavailable on attempt {currentAttempt}");
        }

        Log.Info($"EmailService -> Sending email message (500ms) for Order #{order.Id}...");
        await Task.Delay(500, ct);

        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Thank you for your order! Total amount: ${order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = currentAttempt,
            LastError = null
        };

        await _store.UpdateListAsync<EmailMessage>(DataFiles.Emails, list => list.Add(email), ct);
        Log.Success($"EmailService -> Confirmation email sent for Order #{order.Id} (Attempts: {currentAttempt})");
    }

    public async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        int maxRetries = 3; // 1 initial + up to 3 retries = 4 total attempts
        int[] delays = [500, 1000, 2000];
        string? lastError = null;

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            try
            {
                await SendConfirmationAsync(order, customer, ct);
                return; // Succeeded
            }
            catch (SmtpUnavailableException ex)
            {
                lastError = ex.Message;
                Log.Error($"EmailService -> Attempt {attempt}/4 failed for Order #{order.Id}: {ex.Message}");

                if (attempt <= maxRetries)
                {
                    int delay = delays[attempt - 1];
                    Log.Info($"EmailService -> Retrying in {delay}ms for Order #{order.Id}...");
                    await Task.Delay(delay, ct);
                }
            }
        }

        // If attempt 4 fails, append email to email_retry.json and return normally
        var failedEmail = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Thank you for your order! Total amount: ${order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = 4,
            LastError = lastError
        };

        await _store.UpdateListAsync<EmailMessage>(DataFiles.EmailRetry, list => list.Add(failedEmail), ct);
        Log.Error($"Email for Order #{order.Id} queued for retry later");
    }
}
