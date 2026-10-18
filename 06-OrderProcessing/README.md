# Async Order Processing System
## .NET 10 — Async, Threads & Thread Safety

**Student Name:** Ahmed Hamdy  
**Project:** E-Commerce Order Processing System — File Storage Edition  
**Framework:** .NET 10 (`net10.0`)

---

# 1. Project Overview

The Async Order Processing System simulates an e-commerce application that validates customer orders, checks inventory, calculates prices, processes payments, saves order information, and sends confirmation emails.

The project demonstrates the differences between synchronous and asynchronous programming, concurrent processing, thread safety, cancellation, exception handling, and CPU-bound parallel work.

Instead of using a real database or external APIs, the project stores data in JSON files and simulates database and network latency using `Task.Delay()` and `Thread.Sleep()`.

The application is divided into four phases:

- **Phase 1 — Async Data Access Layer:** Implements JSON storage, repositories, order validation, and a synchronous versus asynchronous comparison.
- **Phase 2 — Concurrent Processing:** Implements pricing, safe inventory reservation, concurrent order processing, invoice generation, and reporting.
- **Phase 3 — Full Checkout Workflow:** Implements payment processing, timeouts, refunds, email retries, cancellation, and complete checkout processing.
- **Phase 4 — Scale-Up:** Implements live progress reporting, a customer dashboard, competing shipping carriers, parallel fraud scoring, and concurrent email resending.

## Main Objectives

- Understand the difference between synchronous and asynchronous operations.
- Process multiple orders concurrently without losing data.
- Protect shared resources from race conditions.
- Handle failures without leaving orders, stock, or payments inconsistent.
- Use cancellation tokens throughout asynchronous operations.
- Measure execution time and thread usage.
- Compare concurrent I/O operations with parallel CPU-bound operations.

---

# 2. How to Run the Project

## Requirements

- .NET 10 SDK
- Visual Studio 2026 or another compatible .NET development environment
- A terminal such as PowerShell, Command Prompt, or Git Bash

Check the installed SDK:

```bash
dotnet --list-sdks
```

## Run from the Project Directory

Open a terminal in the `OrderProcessing` directory:

```bash
dotnet restore
dotnet build
dotnet run
```

Running without arguments displays the numbered console menu.

## Run a Specific Phase

```bash
dotnet run -- phase1
dotnet run -- phase2
dotnet run -- phase3
dotnet run -- phase4
```

Run all four phases:

```bash
dotnet run -- all
```

## Run with Automatic Cancellation

The following command cancels Phase 3 after approximately four seconds:

```bash
dotnet run -- phase3 --cancel-after 4000
```

The cancellation delay is specified in milliseconds.

Pressing `Ctrl+C` also requests cancellation of the current run.

**Important:** Each phase starts by calling `DataSeeder.ResetAsync()`. This resets the runtime data to the initial seed data before that phase begins.

## Data Folder Location

The application creates its runtime data directory at:

```text
Path.Combine(AppContext.BaseDirectory, "Data")
```

When running a Debug build, the directory is normally:

```text
OrderProcessing/bin/Debug/net10.0/Data/
```

The exact location depends on the build configuration and output directory.

The `Data` directory is generated at runtime. It should not be committed as ordinary project source data.

---

# 3. Project Structure and Class Responsibilities

The project separates domain models, storage, logging, exceptions, repositories, services, processors, and phase runners.

```text
06-OrderProcessing/
|
+-- OrderProcessing/
|   |
|   +-- OrderProcessing.csproj
|   +-- Program.cs
|   |
|   +-- Shared/
|   |   +-- Models/
|   |   +-- Storage/
|   |   +-- Logging/
|   |   +-- Exceptions/
|   |
|   +-- Phase1/
|   |   +-- Repositories
|   |   +-- OrderValidator
|   |   +-- Phase1Runner
|   |
|   +-- Phase2/
|   |   +-- PricingService
|   |   +-- OrderPricer
|   |   +-- Inventory reservation
|   |   +-- InvoiceGenerator
|   |   +-- ReportGenerator
|   |   +-- OrderProcessorV2
|   |   +-- Phase2Runner
|   |
|   +-- Phase3/
|   |   +-- CustomerService
|   |   +-- InventoryService
|   |   +-- PaymentGateway
|   |   +-- EmailService
|   |   +-- OrderProcessor
|   |   +-- SyncOrderProcessor
|   |   +-- Phase3Runner
|   |
|   +-- Phase4/
|       +-- CustomerDashboardService
|       +-- ShippingQuoteService
|       +-- Fraud scoring
|       +-- Email resend
|       +-- Phase4Runner
|
+-- sample-output/
|   +-- phase1/
|   +-- phase2/
|   +-- phase3/
|   +-- phase4/
|
+-- screenshots/
|
+-- README.md
```

The names above describe the intended responsibilities and logical organization. Keep the README aligned with the actual files and class names in the submitted project.

## Shared Components

| Component | Responsibility |
|---|---|
| `Program` | Starts the application, parses command-line arguments, and displays the menu. |
| `DataFiles` | Defines the fixed JSON file names. |
| `JsonFileStore` | Reads and writes JSON data, handles missing or corrupted files, and protects file operations. |
| `DataSeeder` | Resets the runtime data and creates the initial seed files. |
| `Log` | Prints timestamped messages, thread IDs, and operation results. |
| `Customer` | Represents a customer and their account information. |
| `Product` | Represents a product, including its price, stock quantity, and weight. |
| `Coupon` | Represents a discount coupon and its expiry date. |
| `OrderItem` | Represents a product and its quantity in an order. |
| `Order` | Holds order items, pricing information, status, and failure details. |
| `PaymentTransaction` | Records payment transactions and their statuses. |
| `PaymentResult` | Represents the result returned by the payment gateway. |
| `EmailMessage` | Represents an email and its retry information. |
| `Report` | Contains order statistics, revenue, and product sales information. |
| Custom exceptions | Represent specific failures such as missing customers, invalid coupons, unavailable shipping, database write failures, SMTP failures, and corrupted JSON files. |

## Phase 1 — Data Access and Validation

Repositories provide access to customers, products, coupons, incoming orders, and processed orders.

`OrderValidator` checks each order in the required order and records the first validation failure.

`SyncCustomerRepository`, `SyncProductRepository`, `SyncCouponRepository`, and `SyncOrderValidator` provide synchronous comparison implementations.

`Phase1Runner` measures the synchronous and asynchronous validation workflows.

## Phase 2 — Concurrent Processing

`PricingService` calculates subtotals, discounts, and shipping costs.

`OrderPricer` compares sequential pricing calls with concurrent asynchronous calls.

`RaceConditionDemo` demonstrates unsafe and safe inventory reservations.

`OrderProcessorV2` processes validated orders and handles failures without stopping unrelated orders.

`InvoiceGenerator` simulates CPU-bound invoice rendering.

`ReportGenerator` creates a JSON report containing order and sales statistics.

`Phase2Runner` coordinates the race-condition demo, pricing measurements, concurrent processing, and reporting.

## Phase 3 — Full Checkout

`CustomerService` loads customer information.

`InventoryService` reserves and releases stock.

`PaymentGateway` simulates payment authorization, fraud checks, capture, decline, timeout, and refunds.

`EmailService` sends confirmation emails and supports retries.

`OrderProcessor` coordinates the complete checkout workflow and compensates for failures when necessary.

`SyncOrderProcessor` provides the synchronous comparison implementation.

`Phase3Runner` runs the checkout workflow, failure scenarios, email retry demonstrations, and performance measurements.

## Phase 4 — Scale-Up

`CustomerDashboardService` loads a customer's profile, orders, payments, and emails concurrently.

`ShippingQuoteService` requests quotes from multiple carriers and selects the first successful quote within the time budget.

The fraud-scoring component compares sequential CPU-bound work with parallel processing.

The email-resend component sends multiple emails concurrently while respecting the required concurrency limit.

`Phase4Runner` executes these demonstrations and prints their results.

---

# 4. Storage Design

## 4.1 JSON File List

Every collection is stored as a separate JSON file in the runtime `Data` directory.

| File | Purpose |
|---|---|
| `customers.json` | Seeded customer records; read-only during processing. |
| `products.json` | Product information and current stock quantities. |
| `coupons.json` | Discount coupon information and expiry dates. |
| `incoming_orders.json` | The 20 original sample orders; read-only during processing. |
| `orders.json` | Orders processed by the application. |
| `payments.json` | Payment transaction records. |
| `emails.json` | Successfully recorded outgoing email messages. |
| `email_retry.json` | Emails that failed all permitted attempts and require later retry. |
| `invoices/` | Generated invoice text files. |
| `reports/` | Generated JSON reports. |

The `invoices` and `reports` directories are created when needed.

## 4.2 JSON Format

The project uses `System.Text.Json` with one shared `static readonly JsonSerializerOptions` instance.

The required settings are:

- `WriteIndented = true`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `JsonStringEnumConverter` for storing enum values as strings
- UTF-8 encoding

For example, an order uses JSON property names such as `customerId`, `items`, and `totalAmount`. Enum values are stored as readable strings such as `"Completed"` rather than numeric values.

Empty collections must be written as:

```json
[]
```

They must never be represented by an empty file.

Money values use `decimal` rather than `double`.

## 4.3 Reset Policy

Each phase begins with:

```csharp
await DataSeeder.ResetAsync();
```

The seeder deletes and recreates the runtime `Data` directory using the required seed data.

It prints a summary for each seeded file.

Data is not reset again within a phase unless the phase explicitly requires another reset for a comparison or demonstration.

## 4.4 JsonFileStore Responsibilities

`JsonFileStore` is the only class responsible for reading and writing the JSON collection files.

Repositories and services use the store rather than accessing the collection files directly.

Invoice and report output files are the specified exceptions.

The store provides synchronous operations for the synchronous comparison versions and asynchronous operations that accept a `CancellationToken` for asynchronous workflows.

It does not contain simulated business-operation delays. Those delays belong to repositories and services.

Reading a missing collection file returns an empty collection. Invalid JSON results in a `DataFileCorruptedException` whose message identifies the file.

## 4.5 Protecting Files Under Concurrent Access

Multiple orders may attempt to update the same JSON file at the same time. Without synchronization, two operations could read the same old data and overwrite each other's changes.

For example, two reservations could read a stock quantity of one, both decide that the product is available, and both write their results. This is a race condition.

The storage design must therefore protect the complete read-modify-write operation, not just an individual file read or write.

The implementation uses [describe the actual synchronization mechanism used in your code, such as a shared `SemaphoreSlim` or a shared lock].

The protection must be shared by every operation accessing the same file. Separate synchronization objects for individual service instances would not provide the required protection.

For asynchronous operations, an asynchronous-compatible synchronization mechanism allows the code to wait without blocking a thread unnecessarily.

Writes must also be atomic from the application's point of view. A safe approach is to serialize the complete collection to a temporary file and replace the destination only after the temporary file has been written successfully.

This prevents a failed write from leaving the destination file partially written. The actual implementation should be checked to confirm that it follows this approach.

**Important distinction:** Thread-safe file access prevents lost updates and corrupted JSON. Business-level synchronization is still required to ensure that a product's stock is not oversold.

---

# 5. Business Rules

## 5.1 Order Validation

Validation checks the following conditions in order:

1. The customer exists.
2. The customer is active.
3. Every product exists.
4. Sufficient stock is available.
5. The specified coupon exists, if one is provided.
6. The coupon has not expired.

The first failed condition determines the rejection reason.

Expected rejection examples include:

- `Customer is inactive`
- `Insufficient stock for 'Wireless Mouse'`
- `Coupon 'OLD20' has expired`
- `Customer #99 not found`
- `Coupon 'FAKE50' does not exist`

The validator checks stock availability. Actual reservation occurs later in the processing workflow.

## 5.2 Pricing

The subtotal is calculated as:

`SubTotal = Σ(UnitPrice × Quantity)`

The discount percentage is the sum of the customer's loyalty discount and the coupon discount, capped at 30%.

Loyalty discounts:

| Loyalty tier | Discount |
|---|---:|
| Bronze | 0% |
| Silver | 5% |
| Gold | 10% |

The discount amount is:

`Discount = SubTotal × DiscountPercentage / 100`

Shipping is calculated as:

`ShippingCost = 5.00 + 1.50 × TotalQuantity + 2.00 × TotalWeightKg`

Shipping is free when the subtotal is at least 1000.

The final total is:

`TotalAmount = SubTotal - Discount + ShippingCost`

Every money value is rounded using:

```csharp
Math.Round(value, 2, MidpointRounding.AwayFromZero)
```

Reference calculations:

| Order | Subtotal | Discount | Shipping | Total |
|---|---:|---:|---:|---:|
| #1 | 1225.00 | 245.00 | 0.00 | 980.00 |
| #2 | 800.00 | 40.00 | 7.30 | 767.30 |

## 5.3 Inventory and Payment Consistency

Inventory reservations must be safe under concurrent access.

If a reservation fails after some items have been reserved, the previously reserved quantities must be released.

If payment is declined or times out, the reservation is released.

If payment has already been captured but saving the completed order fails, the payment must be refunded and the stock released.

A cancelled order must be restored to the required consistent state. Its status must be `Pending`, and its failure reason must be `Cancelled`. A cancelled order must not be classified as `Failed` or `PaymentFailed`.

## 5.4 Email Retry

Confirmation emails use one initial attempt followed by up to three retries.

The retry delays are 500 ms, 1000 ms, and 2000 ms.

If the final attempt fails, the email is recorded in `email_retry.json` with its attempt count and last error.

An email failure must not change an otherwise successfully paid order to a failed order.

---

# 6. Phase 1 — Questions and Answers

## Q1. Why are the synchronous and asynchronous times almost the same when orders are awaited one after another?

Both versions perform the same work and simulate the same database delays. Since each order is completed before the next one begins, the waiting periods do not overlap.

The asynchronous version avoids blocking a thread during asynchronous waits, but it does not automatically shorten the elapsed time of a sequential workflow.

## Q2. Is the thread ID before an await always the same as after it? Why?

No. An asynchronous method may resume on a different thread after an incomplete `await`.

The continuation can be scheduled on an available thread, depending on the execution environment and synchronization context. Thread IDs from the actual console logs should be used as evidence.

## Q3. What does the thread do while Task.Delay() and Thread.Sleep() are waiting?

`Task.Delay()` creates an asynchronous wait. The method yields control, allowing its thread to perform other work.

`Thread.Sleep()` blocks the current thread for the specified duration. The thread cannot execute other work during that sleep.

## Q4. Why should a repository method return Task<T> instead of T?

A repository method that performs asynchronous I/O should return `Task<T>` so callers can await the result without blocking a thread while the operation is waiting.

This also allows asynchronous operations to compose naturally with other asynchronous methods and cancellation tokens.

## Q5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the asynchronous version occupy a thread while the disk is reading?

`File.ReadAllText` performs a synchronous read and does not return until the operation finishes.

`File.ReadAllTextAsync` returns a task that can be awaited. It allows the calling method to yield while the read is pending.

The asynchronous API does not require the calling thread to remain blocked for the entire read, although the exact implementation and operating-system behavior determine how the underlying I/O is performed.

---

# 7. Phase 2 — Questions and Answers

## Q1. What is the difference between starting a task and awaiting it?

Starting a task initiates an operation and gives the program a task representing its progress.

Awaiting the task waits asynchronously for its completion before continuing with the dependent code.

Multiple tasks can be started before awaiting them, allowing independent operations to overlap.

## Q2. Does every Task create a new thread? Does Task.Delay() use a thread while waiting?

No. A `Task` represents an operation, not necessarily a dedicated thread.

`Task.Delay()` uses a timer-based asynchronous wait and does not need to keep a thread blocked throughout the delay.

## Q3. What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous?

`.Result` and `.Wait()` block the current thread until the task completes.

Blocking can reduce scalability and may cause deadlocks in environments with synchronization contexts when a continuation needs a thread that is blocked waiting for it.

The demonstration should be isolated as required by the assignment. Blocking task APIs must not be used in the ordinary asynchronous workflow.

## Q4. What happens to an exception thrown inside a task that is never awaited?

The task can enter the `Faulted` state and retain its exception. If nobody observes or handles the failure, the application may continue without correctly handling the operation's error.

The caller loses the normal opportunity to respond to the failure through `await` and `try/catch` at the appropriate point.

## Q5. When should you use Task.Run(), and when should you avoid it?

`Task.Run()` is useful for moving CPU-bound work onto a thread-pool thread when doing so helps keep the calling thread responsive.

It is generally unnecessary to wrap ordinary asynchronous I/O operations in `Task.Run()`. Asynchronous file and network APIs can already yield while waiting.

## Q6. What is the difference between Task and Task<T>?

`Task` represents an asynchronous operation without returning a result value.

`Task<T>` represents an asynchronous operation that produces a result of type `T`.

For example, sending an email can return `Task`, while retrieving a customer can return `Task<Customer?>`.

## Q7. Why can a lock block not contain an await?

The C# `lock` statement cannot contain an `await` expression because the compiler does not allow asynchronous suspension inside the lock body.

A thread-affine monitor lock must be released correctly by the thread that acquired it. An asynchronous continuation may resume on another thread.

For asynchronous mutual exclusion, use an appropriate asynchronous-compatible mechanism such as `SemaphoreSlim.WaitAsync()`.

## Q8. Why did the unprotected stock reservation oversell, and how did you prevent it?

Both concurrent reservations could read the same stock quantity before either updated the file. Each operation could then make its decision using stale data.

The solution is to protect the complete stock read-check-update-write operation so only one reservation can make the decision at a time.

The expected safe result for one remaining keyboard is one successful reservation, one unsuccessful reservation, and a final stock quantity of zero.

---

# 8. Phase 3 — Questions and Answers

## Q1. Does async make one order's workflow faster?

Not necessarily. Asynchronous programming does not make the individual database or payment operation execute faster.

Its main advantage is that threads are not unnecessarily blocked while waiting for I/O. This allows the application to handle more operations efficiently.

## Q2. Why does async scale better for many orders?

Many orders spend a significant amount of time waiting for simulated database, payment, and email operations.

An asynchronous workflow can suspend these operations and allow available threads to perform other work. This reduces the need to dedicate a blocked thread to every waiting order.

## Q3. What happens to the synchronous version with 1,000 orders?

Creating a dedicated thread for every order consumes substantial operating-system resources and increases scheduling and memory overhead.

The system may become slow or run into resource limits. The exact behavior depends on the environment and the work performed by each thread.

An asynchronous implementation can manage many I/O-bound operations without requiring one dedicated thread per order.

## Q4. Why is async essential for applications that call databases, files, and APIs?

These operations often spend time waiting for external resources.

Asynchronous APIs allow the application to yield during those waits rather than blocking a thread. This improves scalability and responsiveness when many operations are in progress.

---

# 9. Phase 3 — Failure Handling and Fire-and-Forget

## 9.1 Controlled Failures

The full checkout workflow demonstrates the following scenarios:

| Order | Failure | Expected handling |
|---|---|---|
| #5 | Payment timeout | Release stock and save `PaymentFailed`. |
| #8 | Payment declined | Release stock and save `PaymentFailed`. |
| #13 | Database save fails after capture | Refund payment, release stock, and save the appropriate failure state. |
| #3 | Email fails twice | Retry until the third attempt succeeds. |
| #17 | Email always fails | Store the final email in `email_retry.json`. |
| #15 | Customer does not exist | Reject the order with the customer-not-found reason. |

The order processor must handle failures without leaving the order, inventory, and payment data inconsistent.

## 9.2 Why Is Await Necessary?

Awaiting an asynchronous operation allows the caller to know when the operation has completed and to observe its result or exception.

For example, the checkout processor must await payment before deciding whether to mark the order completed.

It must also await the order save before reporting that the order was successfully saved.

## 9.3 When Is Fire-and-Forget Acceptable?

Fire-and-forget means starting an operation without awaiting its completion.

It can be acceptable for genuinely noncritical background work when a reliable mechanism exists to track failures and manage the operation's lifetime.

It is inappropriate when the operation is essential to the business workflow, such as charging a payment, saving an order, or guaranteeing an email attempt.

In this project, the broken implementation demonstrates that the method can print its completion message before the email finishes. The application may also exit before the email operation completes.

An exception in the unawaited email task is not handled by the caller's normal `try/catch` around that task.

The corrected implementation awaits the email operation or explicitly delegates it to a reliable background-processing mechanism.

## 9.4 Why Is async void Dangerous?

An `async void` method does not return a task that callers can await. Callers cannot normally track its completion or observe its exceptions through a returned task.

This makes testing, cancellation coordination, and error handling more difficult.

The normal exception is an event handler, where the event signature requires a `void` return type.

## 9.5 Email Retry Results

The expected final state includes:

- Order #3 succeeds in sending its email on attempt 3.
- Order #17 exhausts its initial attempt and three retries.
- Order #17 is stored in `email_retry.json` with `Attempts = 4` and a nonempty `LastError`.
- Email failure does not turn an otherwise completed order into a failed order.

---

##  10. Phase 4 — Scale-Up: Questions and Answers

### 1. For each of Tasks 10.1–10.4: which tool did you use, and why can't "start, then await one by one" meet the requirement?

- **Task 10.1 — Live checkout progress: `Task.WhenEach`**

  `Task.WhenEach` allows the program to process tasks as they complete, rather than in the order they were started. This lets the application print each order's result immediately and maintain the completion counter from `[1/20]` to `[20/20]`. Awaiting tasks one by one could make the program wait for a slow order while other orders have already finished, delaying their output and preventing true completion-order reporting.

- **Task 10.2 — Customer dashboard: `Task.WhenAll`**

  `Task.WhenAll` allows the profile, orders, payments, and emails loads to run concurrently and provides one task that completes when all four loads finish. The total time is therefore approximately the duration of the slowest load, rather than the sum of all four durations. Starting and awaiting each load one by one would make the loads sequential and increase the total waiting time.

- **Task 10.3 — Fastest shipping quote: `Task.WhenAny`**

  `Task.WhenAny` lets the program wait for whichever carrier task completes first. The application can inspect that result, continue waiting if the carrier failed, and select the first successful quote. Once a winner is chosen, the remaining carriers must be cancelled. Awaiting carriers one by one would make the result depend on their order in the code, not on which carrier responds first, and could exceed the 1.2-second time budget.

- **Task 10.4 A — Fraud scoring: `Parallel.ForEachAsync`**

  `Parallel.ForEachAsync` distributes CPU-intensive scoring work across multiple workers, with the degree of parallelism limited to `Environment.ProcessorCount`. A sequential loop uses one execution path and cannot use all available CPU cores for this workload. Starting and awaiting tasks one by one would not provide parallel CPU execution.

- **Task 10.4 B — Email re-send: `Parallel.ForEachAsync`**

  `Parallel.ForEachAsync` can run asynchronous email sends concurrently while limiting the maximum degree of parallelism to four. Since sending emails is I/O-bound, the program can make progress on other sends while individual sends await I/O. Awaiting each send one by one would serialize the 30 emails and take much longer than the expected approximately eight seconds.

### 2. In 10.2, when several loads fail, how do you get every exception? What does a plain await give you?

I use `Task.WhenAll` to wait for all four load tasks. If multiple loads fail, the returned task records the failures. To inspect every exception, I examine the completed task's `Exception` property, which is an `AggregateException`, and enumerate its `InnerExceptions`.

A plain `await Task.WhenAll(...)` normally throws one of the underlying exceptions to the caller. Catching that exception alone does not automatically show every failure. Therefore, I inspect the combined task's `Exception.InnerExceptions` when the requirement is to report every failed load.

For customer #99, the dashboard must report both `Customer #99 not found` and `Payments store timeout`.

### 3. In 10.3, what happens to the losing carriers if you never cancel them?

If the losing carriers are not cancelled, their asynchronous operations continue running until they finish, fail, or encounter another cancellation or timeout condition. The caller may already have a winning quote, but the remaining work still consumes time and resources. Their logs and exceptions may also occur after the winning quote has been returned.

I use a linked `CancellationTokenSource` for the carrier requests and cancel it as soon as a successful quote is selected, when the 1.2-second budget expires, or when the caller cancels. Each carrier must observe the cancellation token and stop its work promptly.

### 4. Why does 10.4 A get faster with more CPU cores, while 10.4 B does not depend on the number of cores?

**Fraud scoring (10.4 A)** is CPU-bound. It performs many calculations, so additional CPU cores can execute independent scoring operations simultaneously and reduce the total processing time. The actual speed-up depends on available cores, scheduling overhead, and other system factors.

**Email re-send (10.4 B)** is I/O-bound. Most of the time is spent waiting for email operations to complete, rather than continuously using the CPU. Asynchronous concurrency allows other sends to make progress during those waits. Its concurrency is explicitly limited to four simultaneous sends, so its expected duration is approximately `30 / 4 × 1 second = 7.5 seconds`, or about eight seconds, rather than being determined directly by the number of CPU cores.

### 5. What is the difference between parallel work (10.4 A) and concurrent asynchronous work (10.1)?

**Parallel work** means multiple pieces of work execute at the same time, typically on different CPU cores. In Task 10.4 A, CPU-intensive fraud calculations are spread across available processors.

**Concurrent asynchronous work** means multiple operations can be in progress during overlapping periods, without requiring a separate thread for every operation. In Task 10.1, all 20 checkout tasks are started, and the program reports them as each one completes. Asynchronous operations may spend much of their time waiting for I/O.

In short, parallelism focuses on executing CPU work simultaneously, while asynchronous concurrency focuses on making progress across multiple operations without unnecessarily blocking threads. A program can use both techniques when appropriate.

### 6. Show a cancelled run (`--cancel-after`) and explain what happened to the orders, the stock and the payments.

I ran the application with a cancellation timeout, for example:

```bash
dotnet run -- all --cancel-after 500
```

**Actual console output:**

```text
[Paste the actual console output from your cancelled run here.]
```

**Explanation:**

Cancellation is cooperative: the cancellation token signals that the application should stop, and each operation must observe that token. The final state depends on which operations had completed before cancellation was observed.

- **Orders:** Orders that had not completed may be cancelled or left in their last persisted state, depending on the workflow's cancellation handling. Orders already committed as completed or failed are not automatically rolled back by cancellation.
- **Stock:** A stock change that was already persisted is not automatically reversed just because cancellation occurs. If the workflow reserves stock before cancellation, the implementation must release that reservation or compensate for it where required.
- **Payments:** A payment that has already succeeded is not automatically reversed by cancellation. The workflow must handle the payment result and issue a refund or other compensation if the order cannot proceed. A cancellation or timeout does not, by itself, prove that the payment provider did not charge the customer.

I verified the final orders, stock, and payment records against the persisted JSON files after the cancelled run. The output and explanation above must reflect the actual state produced by the implementation.
