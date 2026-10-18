using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class CustomerRepository
{
    private readonly JsonFileStore _store;

    public CustomerRepository(JsonFileStore store) => _store = store;

    public virtual async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        Log.Info($"CustomerRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"CustomerRepository -> Executing query for Customer #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var list = await _store.ReadListAsync<Customer>(DataFiles.Customers, ct);
        var customer = list.FirstOrDefault(c => c.Id == id);
        if (customer != null)
            Log.Info($"CustomerRepository -> Customer #{id} found ({customer.Name})");
        else
            Log.Info($"CustomerRepository -> Customer #{id} not found");

        return customer;
    }
}

public class ProductRepository
{
    private readonly JsonFileStore _store;
    private static readonly SemaphoreSlim _stockLock = new(1, 1);

    public ProductRepository(JsonFileStore store) => _store = store;

    public virtual async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        Log.Info($"ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for Product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var list = await _store.ReadListAsync<Product>(DataFiles.Products, ct);
        return list.FirstOrDefault(p => p.Id == id);
    }

    public virtual async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
    {
        Log.Info($"ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for all products...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadListAsync<Product>(DataFiles.Products, ct);
    }

    public virtual async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct = default)
    {
        Log.Info($"ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Begin transaction for reserving product #{productId}...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock for product #{productId} (-{qty})...");
        await Task.Delay(400, ct);

        bool success = false;
        await _stockLock.WaitAsync(ct);
        try
        {
            await _store.UpdateListAsync<Product>(DataFiles.Products, products =>
            {
                var prod = products.FirstOrDefault(p => p.Id == productId);
                if (prod != null && prod.StockQuantity >= qty)
                {
                    prod.StockQuantity -= qty;
                    success = true;
                }
            }, ct);
        }
        finally
        {
            _stockLock.Release();
        }

        Log.Info($"ProductRepository -> Commit transaction (Result: {success})...");
        await Task.Delay(200, ct);

        return success;
    }

    public virtual async Task ReleaseStockAsync(int productId, int qty, CancellationToken ct = default)
    {
        Log.Info($"ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Begin transaction for releasing product #{productId}...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock for product #{productId} (+{qty})...");
        await Task.Delay(400, ct);

        await _stockLock.WaitAsync(ct);
        try
        {
            await _store.UpdateListAsync<Product>(DataFiles.Products, products =>
            {
                var prod = products.FirstOrDefault(p => p.Id == productId);
                if (prod != null)
                {
                    prod.StockQuantity += qty;
                }
            }, ct);
        }
        finally
        {
            _stockLock.Release();
        }

        Log.Info($"ProductRepository -> Commit transaction...");
        await Task.Delay(200, ct);
    }

    public virtual async Task<bool> ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct = default)
    {
        Log.Info($"ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        var list = await _store.ReadListAsync<Product>(DataFiles.Products, ct);
        var product = list.FirstOrDefault(p => p.Id == productId);

        Log.Info($"ProductRepository -> Begin transaction [UNSAFE] for product #{productId}...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock [UNSAFE] for product #{productId}...");
        await Task.Delay(400, ct);

        if (product == null || product.StockQuantity < qty)
            return false;

        product.StockQuantity -= qty;
        await _store.WriteListAsync(DataFiles.Products, list, ct);

        Log.Info($"ProductRepository -> Commit transaction [UNSAFE]...");
        await Task.Delay(200, ct);

        return true;
    }
}

public class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store) => _store = store;

    public virtual async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        Log.Info($"CouponRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"CouponRepository -> Executing query for coupon '{code}'...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var list = await _store.ReadListAsync<Coupon>(DataFiles.Coupons, ct);
        return list.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

public class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(JsonFileStore store, IEnumerable<int>? failSaveForOrderIds = null)
    {
        _store = store;
        _failSaveForOrderIds = failSaveForOrderIds != null ? new HashSet<int>(failSaveForOrderIds) : new HashSet<int>();
    }

    public virtual async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"OrderRepository -> Opening connection for Order #{order.Id}...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Begin transaction for Order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info($"OrderRepository -> Insert/Update Order #{order.Id} (Status: {order.Status})...");
        await Task.Delay(400, ct);

        if (_failSaveForOrderIds.Contains(order.Id) && order.Status == OrderStatus.Completed)
        {
            throw new DatabaseWriteException("Connection lost during commit");
        }

        await _store.UpdateListAsync<Order>(DataFiles.Orders, orders =>
        {
            int index = orders.FindIndex(o => o.Id == order.Id);
            if (index >= 0)
                orders[index] = order;
            else
                orders.Add(order);
        }, ct);

        Log.Info($"OrderRepository -> Commit transaction for Order #{order.Id}...");
        await Task.Delay(200, ct);
    }

    public virtual async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        Log.Info($"OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadListAsync<Order>(DataFiles.Orders, ct);
    }

    public virtual async Task<List<Order>> GetByStatusAsync(OrderStatus s, CancellationToken ct = default)
    {
        Log.Info($"OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for orders with status {s}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var list = await _store.ReadListAsync<Order>(DataFiles.Orders, ct);
        return list.Where(o => o.Status == s).ToList();
    }
}

public class IncomingOrderRepository
{
    private readonly JsonFileStore _store;

    public IncomingOrderRepository(JsonFileStore store) => _store = store;

    public virtual async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        Log.Info($"IncomingOrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"IncomingOrderRepository -> Executing query for incoming orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        return await _store.ReadListAsync<Order>(DataFiles.IncomingOrders, ct);
    }
}
