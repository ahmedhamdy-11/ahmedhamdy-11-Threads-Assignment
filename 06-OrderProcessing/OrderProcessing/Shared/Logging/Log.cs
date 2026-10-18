namespace OrderProcessing.Shared.Logging;

public static class Log
{
    private static readonly object _consoleLock = new();

    public static void Info(string message) => WriteMessage(message, ConsoleColor.Gray);
    public static void Success(string message) => WriteMessage(message, ConsoleColor.Green);
    public static void Error(string message) => WriteMessage(message, ConsoleColor.Red);

    private static void WriteMessage(string message, ConsoleColor color)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        int threadId = Environment.CurrentManagedThreadId;
        string line = $"[{timestamp}] [Thread {threadId}] {message}";

        lock (_consoleLock)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = prevColor;
        }
    }
}
