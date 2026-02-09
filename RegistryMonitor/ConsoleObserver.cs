namespace RegistryListener;

public class ConsoleObserver : IObserver<RegistryChangeEvent>
{
    public void OnNext(RegistryChangeEvent e)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[{e.Time}] Registry Modified");
        Console.ResetColor();

        Console.WriteLine($" Key     : {e.KeyPath}");
        Console.WriteLine($" Value   : {e.ValueName}");
        Console.WriteLine($" PID     : {e.ProcessId}");
        Console.WriteLine($" Process : {e.ProcessName}");
        Console.WriteLine(new string('-', 50));
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}