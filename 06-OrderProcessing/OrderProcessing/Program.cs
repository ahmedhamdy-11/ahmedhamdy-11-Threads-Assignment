using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing;

public class Program
{
    public static async Task Main(string[] args)
    {
        string mode = "";
        int? cancelAfterMs = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].ToLowerInvariant();
            if (arg is "phase1" or "phase2" or "phase3" or "phase4" or "all")
            {
                mode = arg;
            }
            else if (arg == "--cancel-after" && i + 1 < args.Length && int.TryParse(args[i + 1], out int ms))
            {
                cancelAfterMs = ms;
                i++;
            }
        }

        if (string.IsNullOrEmpty(mode))
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("     SwiftBite Order Processing System (.NET 10)     ");
            Console.WriteLine("=====================================================");
            Console.WriteLine("Select an option:");
            Console.WriteLine("1. Phase 1 — Async Data Access Layer");
            Console.WriteLine("2. Phase 2 — Concurrent Processing");
            Console.WriteLine("3. Phase 3 — Full Checkout Workflow");
            Console.WriteLine("4. Phase 4 — Scale-Up");
            Console.WriteLine("5. Run All Phases");
            Console.WriteLine("6. Demo Cancelled Run (--cancel-after 4000)");
            Console.WriteLine("0. Exit");
            Console.Write("\nEnter choice (0-6): ");

            string? choice = Console.ReadLine()?.Trim();
            mode = choice switch
            {
                "1" => "phase1",
                "2" => "phase2",
                "3" => "phase3",
                "4" => "phase4",
                "5" => "all",
                "6" => "phase3",
                _ => "exit"
            };

            if (choice == "6")
            {
                cancelAfterMs = 4000;
            }

            if (mode == "exit")
            {
                Console.WriteLine("Exiting.");
                return;
            }
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[CTRL+C Pressed] Cancelling run gracefully...");
            cts.Cancel();
        };

        if (cancelAfterMs.HasValue)
        {
            Console.WriteLine($"[Cancellation configured: Automatically cancelling after {cancelAfterMs.Value} ms]");
            cts.CancelAfter(cancelAfterMs.Value);
        }

        try
        {
            switch (mode)
            {
                case "phase1":
                    await Phase1Runner.RunAsync(cts.Token);
                    break;
                case "phase2":
                    await Phase2Runner.RunAsync(cts.Token);
                    break;
                case "phase3":
                    await Phase3Runner.RunAsync(cts.Token);
                    break;
                case "phase4":
                    await Phase4Runner.RunAsync(cts.Token);
                    break;
                case "all":
                    await Phase1Runner.RunAsync(cts.Token);
                    await Phase2Runner.RunAsync(cts.Token);
                    await Phase3Runner.RunAsync(cts.Token);
                    await Phase4Runner.RunAsync(cts.Token);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            await HandleRunCancelledAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[Unhandled Exception]: {ex.Message}");
        }
    }

    private static async Task HandleRunCancelledAsync()
    {
        var store = new JsonFileStore();
        var orders = await store.ReadListAsync<Order>(DataFiles.Orders, CancellationToken.None);
        int finished = orders.Count(o => o.Status != OrderStatus.Pending);
        int cancelled = orders.Count(o => o.FailureReason == "Cancelled" || o.Status == OrderStatus.Pending);

        Console.WriteLine();
        Console.WriteLine($"Run cancelled — {finished} orders finished, {cancelled} cancelled.");
    }
}
