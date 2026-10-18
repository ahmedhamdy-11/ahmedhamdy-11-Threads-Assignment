# Assignment 02 — Answers

Student name: Ahmed Hamdy  
Group / ID: ahmedhamdy-11

## What is wrong

### Observed Results Before Fix:
- **Run 1**: `Booked=1, TablesLeft=4` (while all 40 customer threads printed booking messages)
- **Run 2**: `Booked=3, TablesLeft=0`
- **Run 3**: `Booked=5, TablesLeft=-2` (oversold, negative table count)

### Root Cause Analysis:
1. **Check-Then-Act Race Condition (TOCTOU)**:
   In `FridayNightBooking.Book()`, the check `if (TablesLeft <= 0) return;` is decoupled from the state update. Right after checking, `Thread.Sleep(20)` yields execution. While one thread is sleeping, dozens of other concurrent threads pass the `TablesLeft <= 0` guard because `TablesLeft` has not yet been decremented.
2. **Non-Atomic Operations & Data Races**:
   The statements `TablesLeft--;` and `Booked++;` are not atomic. They compile into multiple CPU instructions (read variable into register, decrement/increment register, store value back to memory). When 40 threads execute these instructions concurrently without synchronization, thread context switches cause interleaved reads and writes, resulting in lost updates, inconsistent states, and negative table counts.

## How you fixed it

1. **Introduced Mutual Exclusion with `lock`**:
   - Added a private dedicated synchronization object `readonly object _lock = new();`.
   - Enclosed the validation check (`if (TablesLeft <= 0) return;`), the simulated processing delay, and the state mutations (`TablesLeft--; Booked++;`) inside a single `lock (_lock)` block.
2. **Guaranteed Atomicity and Invariant Preservation**:
   - Only one thread at a time can enter the critical section.
   - When a thread enters, it evaluates the true, current `TablesLeft`. If available, it decrements the table count, increments `Booked`, and logs.
   - Once the 5 tables are booked, subsequent threads see `TablesLeft <= 0` and return immediately without altering the counts.
3. **Verified Results After Fix**:
   - **Run 1**: `Booked=5, TablesLeft=0`
   - **Run 2**: `Booked=5, TablesLeft=0`
   - **Run 3**: `Booked=5, TablesLeft=0`
