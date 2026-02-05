using StreamJsonRpc.Aot.Common;

namespace StreamJsonRpc.Aot.Client;

public class CounterObserver : ICounterObserver
{
    public void OnNext(int value)
    {
        if (value != -1)
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"        CounterObserver - OnNext Couter = {value}");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("        CounterObserver - Value inserted by client.");
        Console.ResetColor();
    }

    public void OnCompleted()
    {
        Console.WriteLine("         CounterObserver - Counter completed.");
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"        CounterObserver - Counter error: {error.Message}");
    }
}