# Assignment 05 — Answers

Student name: Ahmed Hamdy  
Group / ID: ahmedhamdy-11

## Failures you observed

### Observed Failures Before Fix:
1. **Crash with `System.ArgumentException`**:
   `Unhandled exception. System.ArgumentException: An item with the same key has already been added. Key: koshary-bowl` in `RecordView` when multiple threads executed `Views.ContainsKey` simultaneously, found it missing, and both attempted to call `Views.Add`.
2. **Lost View Counts / Data Corruption**:
   When no exception occurred, `actual` views were significantly lower than expected (e.g., `Views expected=50000, actual=33715`) because concurrent writes to a non-thread-safe `Dictionary<TKey, TValue>` corrupt internal buckets and drop entries.
3. **Queue Inconsistencies & `InvalidOperationException`**:
   Background workers encountered race conditions between checking `EmailJobs.Count > 0` and executing `EmailJobs.Dequeue()`. Two workers saw `Count == 1`, both called `Dequeue()`, causing the second worker to throw `InvalidOperationException: Queue empty`. Furthermore, non-synchronized `Enqueue` calls corrupted internal circular buffer pointers.

## What you changed

1. **Replaced `Dictionary<string, int>` with `ConcurrentDictionary<string, int>`**:
   In `RecordView(string product)`, utilized the atomic method `Views.AddOrUpdate`:
   ```csharp
   Views.AddOrUpdate(product, 1, (_, current) => current + 1);
   ```
   This guarantees that key insertion and incrementation are performed atomically without throwing `ArgumentException` or losing counts.
2. **Replaced `Queue<string>` with `ConcurrentQueue<string>`**:
   In `EmailJobs`, switched to `ConcurrentQueue<string>`.
   In `EmailWorker()`, eliminated the check-then-act pattern (`Count > 0` then `Dequeue`) in favor of atomic `EmailJobs.TryDequeue(out string? job)`.
   If dequeuing fails and `ProducersDone` is true, an additional `TryDequeue` check confirms the queue is genuinely drained before terminating the worker thread.
3. **Verification After Fix**:
   - **Run 1**: Views expected=50000, actual=50000 | Email jobs placed=20, unique sent=20, total sends=20
   - **Run 2**: Views expected=50000, actual=50000 | Email jobs placed=20, unique sent=20, total sends=20
   - **Run 3**: Views expected=50000, actual=50000 | Email jobs placed=20, unique sent=20, total sends=20
   No exceptions thrown, no duplicate or lost email sends, view count is 100% accurate.

## Collection safety vs business-rule safety

Making a collection thread-safe (e.g., using `ConcurrentQueue<T>` or `ConcurrentDictionary<TKey, TValue>`) ensures that individual operations (`TryAdd`, `TryDequeue`, `AddOrUpdate`) maintain internal memory invariants, prevent data structure corruption, and do not crash the runtime.

However, **collection thread safety does NOT guarantee business rule safety**:
1. **Compound Multi-Step Operations**:
   A business rule often requires multiple dependent steps:
   `Check if condition holds -> Read value -> Compute -> Update value`.
   Even if the underlying collection is thread-safe, the window of time between the separate operations is unprotected. For example:
   ```csharp
   // Even if queue is a ConcurrentQueue, this check-then-act is broken:
   if (queue.Count > 0)
   {
       // Another thread can dequeue here!
       var item = queue.Dequeue(); // Throws or returns unexpected data
   }
   ```
2. **Multi-Entity Invariants**:
   If a business transaction involves two collections (e.g., removing stock from `Inventory` and writing a transaction to `AuditLog`), making each collection a `ConcurrentDictionary` does not make the transfer atomic. If one thread checks inventory and another removes it, the system can oversell stock.
3. **Business Invariant Protection**:
   Business rules require atomic composite operations across the entire transaction boundary. This is achieved either through atomic compound primitives provided by concurrent collections (like `ConcurrentDictionary.AddOrUpdate` or `TryUpdate`), locking the domain entity/resource (e.g. `lock`), or transactional synchronization (like 2-phase commit, mutex, or database transactions).
