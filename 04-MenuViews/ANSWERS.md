# Assignment 04 — Answers

Student name: Ahmed Hamdy  
Group / ID: ahmedhamdy-11

## Why Expected and Actual disagree

### Initial Run Observation:
- **Expected**: `400000` (8 threads × 50,000 views)
- **Actual**: `209090` (differed on each run, consistently losing roughly half of all increments)

### Explanation:
The operation `Views++` is not atomic. In CIL/assembly, `Views++` decomposes into three distinct steps:
1. Load the current value of `Views` from memory into a CPU register.
2. Increment the value in the register.
3. Store the updated value from the register back into memory.

When 8 threads execute `Views++` concurrently on multi-core processors, their execution steps interleave. Two or more threads read the same initial value simultaneously, increment it locally, and write back the identical value. This overwrites earlier increments, causing lost updates.

## What you changed

Replaced `Views++` in `MenuViewCounter.RegisterView()` with:
```csharp
Interlocked.Increment(ref Views);
```
`Interlocked.Increment` uses CPU-level atomic instructions (such as `LOCK XADD` on x86/x64). It performs the read, increment, and write in an indivisible hardware operation without the memory overhead, context-switching cost, or contention bottlenecks of a heavyweight `lock`.

### Verification After Fix:
- **Run 1**: Expected: `400000`, Actual: `400000`
- **Run 2**: Expected: `400000`, Actual: `400000`
- **Run 3**: Expected: `400000`, Actual: `400000`

## When would you protect the update differently

`Interlocked` operations are only capable of protecting single primitive variables (int, long, pointer). You would protect updates differently using a `lock`, `Monitor`, or `ReaderWriterLockSlim` in the following scenarios:
1. **Multi-field or compound state invariants**: When updating multiple interrelated variables atomically (for example, deducting from one account and crediting another, or updating a count and an associated timestamp together).
2. **Conditional business logic (Check-Then-Act)**: When an update depends on a check (e.g., checking if stock > 0 before decrementing). `Interlocked.CompareExchange` can handle simple optimistic concurrency loops, but complex conditional logic with multiple conditions requires a mutex/lock.
3. **Complex collections or reference graphs**: Updating non-thread-safe collections (such as `List<T>` or `Dictionary<TKey, TValue>`) or invoking external delegate callbacks within the critical section.
