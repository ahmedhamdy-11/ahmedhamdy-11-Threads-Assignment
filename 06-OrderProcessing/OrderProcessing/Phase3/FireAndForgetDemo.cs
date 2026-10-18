using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository;
    private readonly IEmailService _emailService;

    public FireAndForgetDemo(OrderRepository orderRepository, IEmailService emailService)
    {
        _orderRepository = orderRepository;
        _emailService = emailService;
    }

    // Broken method with CS4014 warning suppressed
    public async Task ProcessOrderBrokenAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);
#pragma warning disable CS4014
        _emailService.SendConfirmationAsync(order, customer); // not awaited
#pragma warning restore CS4014
        Console.WriteLine("Order processing finished");
    }

    // Fixed method awaiting email sending
    public async Task ProcessOrderFixedAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        await _orderRepository.SaveAsync(order, ct);
        try
        {
            await _emailService.SendConfirmationAsync(order, customer, ct);
        }
        catch (Exception ex)
        {
            Log.Error($"Caught handled exception during email sending: {ex.Message}");
        }
        Console.WriteLine("Order processing finished");
    }

    public static async Task RunDemoAsync(JsonFileStore store, CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Task 9.7: Fire-and-Forget Investigation =====");

        var orderRepo = new OrderRepository(store);
        var emailServiceBroken = new EmailService(store, new Dictionary<int, int> { [999] = 1 });
        var demo = new FireAndForgetDemo(orderRepo, emailServiceBroken);

        var customer = new Customer { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com" };
        var order = new Order { Id = 999, CustomerId = 1, TotalAmount = 150m, Status = OrderStatus.Completed };

        Console.WriteLine("\n--- 1. Broken Implementation (Unawaited Task) ---");
        Console.WriteLine("Invoking ProcessOrderBrokenAsync...");
        await demo.ProcessOrderBrokenAsync(order, customer);
        Console.WriteLine("Notice: 'Order processing finished' printed BEFORE email finished or threw!");
        // Give background unawaited task a moment to demonstrate unobserved/background behavior
        await Task.Delay(1200, ct);

        Console.WriteLine("\n--- 2. Fixed Implementation (Awaited Task with Error Handling) ---");
        Console.WriteLine("Invoking ProcessOrderFixedAsync...");
        await demo.ProcessOrderFixedAsync(order, customer, ct);
        Console.WriteLine("Notice: Order processing finished only after the entire workflow was safely completed.");
    }
}
