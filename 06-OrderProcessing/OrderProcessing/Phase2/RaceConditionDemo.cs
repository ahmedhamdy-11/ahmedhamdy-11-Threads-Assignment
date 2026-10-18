using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public static class RaceConditionDemo
{
    public static async Task RunAsync(JsonFileStore store, CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Task 8.3: Race Condition Demo on Mechanical Keyboard =====");
        var productRepo = new ProductRepository(store);

        // --- Step 1: Unsafe Reservation ---
        Console.WriteLine("\n--- Part A: Unsafe Reservation (No synchronization) ---");
        // Ensure P4 stock is exactly 1
        await store.UpdateListAsync<Product>(DataFiles.Products, products =>
        {
            var p4 = products.First(p => p.Id == 4);
            p4.StockQuantity = 1;
        }, ct);

        Log.Info("Mechanical Keyboard stock set to 1. Launching Orders #10 and #11 concurrently (Unsafe)...");
        var unsafeTask1 = productRepo.ReserveStockUnsafeAsync(4, 1, ct);
        var unsafeTask2 = productRepo.ReserveStockUnsafeAsync(4, 1, ct);

        bool result10Unsafe = await unsafeTask1;
        bool result11Unsafe = await unsafeTask2;

        var prodsAfterUnsafe = await store.ReadListAsync<Product>(DataFiles.Products, ct);
        int stockAfterUnsafe = prodsAfterUnsafe.First(p => p.Id == 4).StockQuantity;

        Console.WriteLine($"[UNSAFE RESULT] Order #10 reservation: {result10Unsafe}");
        Console.WriteLine($"[UNSAFE RESULT] Order #11 reservation: {result11Unsafe}");
        Console.WriteLine($"[UNSAFE RESULT] Stock remaining in products.json: {stockAfterUnsafe}");
        if (result10Unsafe && result11Unsafe)
        {
            Log.Error("OVERSELL DETECTED! Both orders secured the single keyboard.");
        }

        // --- Step 2: Safe Reservation ---
        Console.WriteLine("\n--- Part B: Safe Reservation (With SemaphoreSlim / Atomic Update) ---");
        // Reset P4 stock to 1 again
        await store.UpdateListAsync<Product>(DataFiles.Products, products =>
        {
            var p4 = products.First(p => p.Id == 4);
            p4.StockQuantity = 1;
        }, ct);

        Log.Info("Mechanical Keyboard stock reset to 1. Launching Orders #10 and #11 concurrently (Safe)...");
        var safeTask1 = productRepo.ReserveStockAsync(4, 1, ct);
        var safeTask2 = productRepo.ReserveStockAsync(4, 1, ct);

        bool result10Safe = await safeTask1;
        bool result11Safe = await safeTask2;

        var prodsAfterSafe = await store.ReadListAsync<Product>(DataFiles.Products, ct);
        int stockAfterSafe = prodsAfterSafe.First(p => p.Id == 4).StockQuantity;

        Console.WriteLine($"[SAFE RESULT] Order #10 reservation: {result10Safe}");
        Console.WriteLine($"[SAFE RESULT] Order #11 reservation: {result11Safe}");
        Console.WriteLine($"[SAFE RESULT] Stock remaining in products.json: {stockAfterSafe}");

        if ((result10Safe && !result11Safe) || (!result10Safe && result11Safe))
        {
            Log.Success("RACE CONDITION PREVENTED! Exactly one order succeeded, and stock is 0.");
        }
    }
}
