public class ConsoleObserver : IObserver<RegistryChangeEvent>
{
    public void OnNext(RegistryChangeEvent e)
    {
        Console.WriteLine($"[{e.Time}] EventID: {(int)e.AuditEventId} ({e.AuditEventId})");
        Console.WriteLine($" Operation: {e.OperationType}");
        Console.WriteLine($" Key      : {e.KeyPath}");
        Console.WriteLine($" Value    : {e.ValueName}");
        Console.WriteLine($" Access   : {e.AccessTypeRaw}");
        Console.WriteLine($" PID      : {e.ProcessId}");
        Console.WriteLine($" Process  : {e.ProcessName}");

        if (e.OldValue != null || e.NewValue != null)
        {
            Console.WriteLine($" Old      : {e.OldValue}");
            Console.WriteLine($" New      : {e.NewValue}");
        }

        Console.WriteLine(new string('-', 60));
    }

    public void OnError(Exception error) => Console.WriteLine(error);
    public void OnCompleted() { }
}
