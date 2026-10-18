# Assignment 03 � Answers

Student name: Ahmed Hamdy  
Group / ID: ahmedhamdy-11

## 1. What is System.Threading.Monitor used for?
`System.Threading.Monitor` is a static class in .NET providing a monitor-based synchronization mechanism to control access to objects across threads. It provides mutual exclusion (ensuring only one thread executes a critical section at any given time) and condition-variable thread coordination (allowing threads to wait and signal state changes via `Wait`, `Pulse`, and `PulseAll` without spinning or busy-waiting).

## 2. What does the C# lock statement compile to?
The C# `lock (obj) { /* critical section */ }` statement is syntactic sugar that compiles into a `try...finally` construct using `Monitor.Enter(object, ref bool)` and `Monitor.Exit(object)`:

```csharp
bool lockTaken = false;
try
{
    Monitor.Enter(obj, ref lockTaken);
    // Critical section code
}
finally
{
    if (lockTaken)
    {
        Monitor.Exit(obj);
    }
}
```
The `lockTaken` boolean pattern ensures that `Monitor.Exit` is only invoked if `Monitor.Enter` actually succeeded, avoiding runtime `SynchronizationLockException` and preventing lock leaks even if unexpected exceptions occur during lock acquisition.

## 3. Differences between using lock and calling Monitor APIs directly
1. **Timeout and Non-Blocking Acquisition**:
   The `lock` keyword always blocks indefinitely until the lock is acquired. Calling `Monitor.TryEnter(obj, timeout)` allows a thread to specify a maximum wait time (or zero milliseconds for a non-blocking check), returning `false` if the lock could not be acquired, which helps prevent deadlocks and responsive thread starvation.
2. **Advanced Signaling & Coordination (`Wait` / `Pulse`)**:
   The `lock` block provides mutual exclusion only. Direct `Monitor` APIs like `Monitor.Wait(obj)` and `Monitor.Pulse(obj)` / `Monitor.PulseAll(obj)` allow threads inside the lock to temporarily yield the lock and wait for an event or state transition, enabling advanced concurrency patterns like blocking producer-consumer queues.
3. **Syntactic Safety**:
   `lock` guarantees release at the end of the lexical scope via the compiler-generated `finally` block. Calling `Monitor.Enter` manually is error-prone because forgetting the `finally` block or misplacing code before `try` can cause permanently dangling locks if an exception is thrown.

## 4. When might a developer use Monitor.TryEnter, Monitor.Pulse, or Monitor.Wait instead of a plain lock?
- **`Monitor.TryEnter`**: Used when avoiding deadlocks (e.g., trying to acquire multiple resources without risking cyclic dependencies), or in latency-sensitive threads (such as UI threads or timer callbacks) where doing fallback work is better than blocking indefinitely.
- **`Monitor.Wait`**: Used when a worker thread needs to wait until a specific condition becomes true (e.g., waiting for items to appear in an empty queue). It atomically releases the monitor lock and suspends the thread until notified, avoiding CPU waste.
- **`Monitor.Pulse` / `PulseAll`**: Used by producer threads after updating shared state (e.g., enqueueing a new task) to wake up one or all waiting threads so they can re-acquire the lock and continue processing.

## 5. Critical Section Comparison

### Using `lock`:
```csharp
private readonly object _sync = new();
private int _balance;

public void Deposit(int amount)
{
    lock (_sync)
    {
        _balance += amount;
    }
}
```

### Using `Monitor.Enter` / `Monitor.Exit`:
```csharp
private readonly object _sync = new();
private int _balance;

public void Deposit(int amount)
{
    bool lockTaken = false;
    try
    {
        Monitor.Enter(_sync, ref lockTaken);
        _balance += amount;
    }
    finally
    {
        if (lockTaken)
        {
            Monitor.Exit(_sync);
        }
    }
}
```

## Links:
- https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor
- https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock
