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

// --- Task 10.2: Customer Dashboard ---
public class CustomerDashboardService
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(JsonFileStore store, IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _store = store;
        _failPaymentsForCustomerIds = failPaymentsForCustomerIds != null ? new HashSet<int>(failPaymentsForCustomerIds) : new HashSet<int>();
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct = default)
    {
        var loadCustomerTask = LoadCustomerAsync(customerId, ct);
        var loadOrdersTask = LoadOrdersAsync(customerId, ct);
        var loadPaymentsTask = LoadPaymentsAsync(customerId, ct);
        var loadEmailsTask = LoadEmailsAsync(customerId, ct);

        // R2: The four loads run at the same time, and the method waits for all of them with a single await
        var allTask = Task.WhenAll(loadCustomerTask, loadOrdersTask, loadPaymentsTask, loadEmailsTask);

        try
        {
            await allTask;
        }
        catch (Exception)
        {
            // R4: If more than one load fails, the method reports EVERY failure, not only the first one
            if (allTask.Exception != null && allTask.Exception.InnerExceptions.Count > 0)
            {
                var errors = allTask.Exception.InnerExceptions.Select(e => e.Message).ToList();
                throw new AggregateException(allTask.Exception.InnerExceptions);
            }
            throw;
        }

        return new CustomerDashboard
        {
            Customer = await loadCustomerTask,
            Orders = await loadOrdersTask,
            Payments = await loadPaymentsTask,
            Emails = await loadEmailsTask
        };
    }

    private async Task<Customer> LoadCustomerAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var customers = await _store.ReadListAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == customerId);
        if (customer == null)
            throw new CustomerNotFoundException($"Customer #{customerId} not found");

        return customer;
    }

    private async Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await _store.ReadListAsync<Order>(DataFiles.Orders, ct);
        return orders.Where(o => o.CustomerId == customerId).ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException("Payments store timeout");
        }

        var orders = await _store.ReadListAsync<Order>(DataFiles.Orders, ct);
        var custOrderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();

        var payments = await _store.ReadListAsync<PaymentTransaction>(DataFiles.Payments, ct);
        return payments.Where(p => custOrderIds.Contains(p.OrderId)).ToList();
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await _store.ReadListAsync<Order>(DataFiles.Orders, ct);
        var custOrderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();

        var emails = await _store.ReadListAsync<EmailMessage>(DataFiles.Emails, ct);
        return emails.Where(e => custOrderIds.Contains(e.OrderId)).ToList();
    }
}

// --- Task 10.3: Fastest Shipping Quote ---
public interface IShippingCarrier
{
    string Name { get; }
    Task<decimal> GetQuoteAsync(Order order, CancellationToken ct);
}

public class BaseShippingCarrier : IShippingCarrier
{
    public string Name { get; }
    private readonly int _minDelay;
    private readonly int _maxDelay;
    private readonly decimal _priceFactor;
    private readonly HashSet<int> _failForOrderIds;

    public BaseShippingCarrier(string name, int minDelay, int maxDelay, decimal priceFactor, IEnumerable<int>? failForOrderIds = null)
    {
        Name = name;
        _minDelay = minDelay;
        _maxDelay = maxDelay;
        _priceFactor = priceFactor;
        _failForOrderIds = failForOrderIds != null ? new HashSet<int>(failForOrderIds) : new HashSet<int>();
    }

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct)
    {
        await Task.Delay(150, ct); // Handshake

        if (_failForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException($"[{Name}] Carrier unavailable for order #{order.Id}");
        }

        int delay = Random.Shared.Next(_minDelay, _maxDelay + 1);
        await Task.Delay(delay, ct);

        // Base shipping formula from 8.1
        decimal baseShipping = 5.00m + (1.50m * order.Items.Sum(i => i.Quantity)) + (2.00m * 1.5m); // approximate weight if unpriced
        if (order.SubTotal >= 1000m) baseShipping = 0m;

        decimal quote = Math.Round(baseShipping * _priceFactor, 2, MidpointRounding.AwayFromZero);
        return quote;
    }
}

public class ShippingQuoteService
{
    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(List<IShippingCarrier> carriers) => _carriers = carriers;

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromMilliseconds(1200)); // Time budget 1.2s

        var carrierTasks = new Dictionary<Task<decimal>, IShippingCarrier>();
        var cancelledCarriers = new List<string>();

        foreach (var carrier in _carriers)
        {
            carrierTasks[carrier.GetQuoteAsync(order, linkedCts.Token)] = carrier;
        }

        var pendingTasks = new HashSet<Task<decimal>>(carrierTasks.Keys);
        var exceptions = new List<Exception>();
        var sw = Stopwatch.StartNew();

        try
        {
            while (pendingTasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(pendingTasks);
                pendingTasks.Remove(finishedTask);
                var carrier = carrierTasks[finishedTask];

                if (finishedTask.IsCompletedSuccessfully)
                {
                    sw.Stop();
                    decimal price = finishedTask.Result;

                    // Cancel every carrier still running
                    linkedCts.Cancel();

                    foreach (var remaining in pendingTasks)
                    {
                        var remCarrier = carrierTasks[remaining];
                        cancelledCarriers.Add(remCarrier.Name);
                        Log.Info($"[{remCarrier.Name}] cancelled");
                    }

                    return new ShippingQuote
                    {
                        Carrier = carrier.Name,
                        Price = price,
                        ElapsedMs = sw.ElapsedMilliseconds
                    };
                }
                else if (finishedTask.IsFaulted)
                {
                    exceptions.Add(finishedTask.Exception?.InnerException ?? new Exception("Carrier faulted"));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Time budget expired
        }

        sw.Stop();

        // If all carriers failed explicitly
        if (exceptions.Count == _carriers.Count && pendingTasks.Count == 0)
        {
            throw new ShippingProviderUnavailableException("All carriers failed");
        }

        // Budget exceeded fallback
        foreach (var remaining in pendingTasks)
        {
            var remCarrier = carrierTasks[remaining];
            cancelledCarriers.Add(remCarrier.Name);
            Log.Info($"[{remCarrier.Name}] cancelled");
        }

        return new ShippingQuote
        {
            Carrier = "Flat rate",
            Price = 25.00m,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }
}
