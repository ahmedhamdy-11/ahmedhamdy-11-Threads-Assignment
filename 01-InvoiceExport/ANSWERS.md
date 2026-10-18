# Assignment 01 — Answers

Student name: Ahmed Hamdy  
Group / ID: ahmedhamdy-11

## Problems with the current design

1. **Synchronous Thread Blocking (Thread.Sleep / Synchronous I/O)**:
   The original design uses `Thread.Sleep` and synchronous file operations (`File.ReadAllText`, `File.WriteAllText`). This blocks operating system / ThreadPool threads while waiting for simulated database queries and disk I/O, wasting system resources and hurting scalability.
2. **Sequential Execution of Independent Orders**:
   Orders are processed in a sequential `foreach` loop (`Order fresh = _db.GetOrderById(...)`, `_customers.ReadProfile(...)`, `_invoices.WriteInvoice(...)`). Processing 5 orders sequentially took ~5.9 seconds. If the system scales to thousands of nightly orders, synchronous sequential processing would take hours.
3. **N+1 Query Pattern / Redundant DB Calls**:
   The service queries `_db.GetPendingOrders()`, and then inside the loop performs an individual `_db.GetOrderById(order.Id)` query for each order sequentially, compounding database latency.

## What you changed

1. **Converted Data Access & File Operations to Asynchronous**:
   - `OrderDatabase`: Replaced `Thread.Sleep` with `await Task.Delay` and returned `Task<List<Order>>` / `Task<Order?>`.
   - `CustomerFileStore`: Converted `ReadProfile` to `ReadProfileAsync`, using `await Task.Delay` and `await File.ReadAllTextAsync(path)`.
   - `InvoiceFileWriter`: Converted `WriteInvoice` to `WriteInvoiceAsync`, using `await Task.Delay` and `await File.WriteAllTextAsync(path, body)`.
2. **Concurrent Asynchronous Processing with One-by-One Await**:
   - In `InvoiceExportService.ExportAllPendingAsync()`, order processing tasks are started concurrently for all pending orders and added to a `List<Task>`.
   - In accordance with the assignment rule ("start tasks and then await them one by one — no API that waits for a group of tasks"), we iterated through the tasks with `foreach (var task in tasks) await task;`.
   - Preserved the required business flow (`load order -> read customer -> write invoice`) and invoice file contents.
3. **Console Output Before and After**:

### Output Before (Sequential Synchronous):
```text
Starting invoice export...
Customer files: C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices

Wrote C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices\invoice-1001.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices\invoice-1002.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices\invoice-1003.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices\invoice-1004.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\AdvancedCsharp-Assignment-01-InvoiceExport\invoices\invoice-1005.txt
Export finished in 5.9 s
```

### Output After (Concurrent Asynchronous):
```text
Starting invoice export...
Customer files: C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices

Wrote C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices\invoice-1002.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices\invoice-1004.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices\invoice-1001.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices\invoice-1005.txt
Wrote C:\Users\Ahmed Hamdy\Desktop\Ass\ahmedhamdy-11-Threads-Assignment\invoices\invoice-1003.txt
Export finished in 1.9 s
```
