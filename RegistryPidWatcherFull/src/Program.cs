namespace RegistryPidWatcher;

class Program
{
    public static bool DumpXml = false;

    // ConsoleObserver implements IObserver<RegistryEvent> to receive registry events
    // and print them to the console. It also implements IDisposable to allow unsubscribing
    // from the watcher when disposed.
    private sealed class ConsoleObserver : IObserver<RegistryEvent>, IDisposable
    {
        private readonly RegistryWatcher _watcher;
        private bool _disposed;

        public ConsoleObserver(RegistryWatcher watcher)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        }

        public void OnCompleted() { }
        public void OnError(Exception error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Observer error: {error}");
            Console.ResetColor();
        }

        public void OnNext(RegistryEvent value)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{DateTime.Now}]");
            Console.ResetColor();
            Console.WriteLine($"  Key: {value.Key}");
            Console.WriteLine($"  ValueName: {value.ValueName}");
            Console.WriteLine($"  NewValue: {value.NewValue}");
            Console.WriteLine($"  PID: {value.Pid}");
            Console.WriteLine($"  Process: {value.Process}");
            Console.WriteLine($"  OperationType: {value.OperationType}");
            Console.WriteLine($"  AccessMask: {value.AccessMaskRaw} ({value.AccessMaskText})");
            Console.WriteLine($"  InferredAction: {value.InferredAction}");
            Console.WriteLine();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            try { _watcher.Unsubscribe(this); } catch { }
        }
    }

    // The Main method sets up two RegistryWatcher instances to monitor specific
    // registry paths in HKCU and HKLM.
    static void Main()
    {
        // event log source is "USER\Software\MyApp1"
        const string hkcuSubKey = @"Software\MyApp1";
        const string hkcuKeyFullPath = $@"USER\{hkcuSubKey}";

        // event log source is "MACHINE\SOFTWARE\WorkspaceONE\Satori"
        const string hklmSubKey = @"SOFTWARE\WorkspaceONE\Satori";
        const string hklmKeyFullPath = $@"MACHINE\{hklmSubKey}";

        // Set up the registry watchers for both HKCU and HKLM and subscribe console observers
        using var hkcuWatcher = new RegistryWatcher(hkcuSubKey, useCurrentUser: true);
        using var hklmWatcher = new RegistryWatcher(hklmSubKey);

        var hkcuObserver = new ConsoleObserver(hkcuWatcher);
        var hklmObserver = new ConsoleObserver(hklmWatcher);

        IDisposable hkcuSubscription = hkcuWatcher.Subscribe(hkcuObserver);
        IDisposable hklmSubscription = hklmWatcher.Subscribe(hklmObserver);

        ConsoleInput();

        // Let RegistryWatcher deliver events to subscribers.
        // Keep the main thread alive while background threads do the work.
        Thread.Sleep(Timeout.Infinite);

        void PrintMonitorRegistry()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Monitoring registry:");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  {hkcuKeyFullPath}");
            Console.WriteLine($"  {hklmKeyFullPath}");
            Console.WriteLine();
            Console.ResetColor();
        }

        void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Press ESC to exit, C to clear, D to toggle XML dump.");
            Console.WriteLine();
            Console.ResetColor();
        }

        void ConsoleInput()
        {
            PrintMonitorRegistry();

            var inputThread = new Thread(() =>
            {
                while (true)
                {
                    PrintHelp();

                    var key = Console.ReadKey(true).Key; // blocks here, not main thread

                    if (key == ConsoleKey.Escape)
                    {
                        hkcuSubscription.Dispose();
                        hklmSubscription.Dispose();

                        hkcuWatcher.Dispose();
                        hklmWatcher.Dispose();

                        Console.WriteLine("Exiting...");
                        Environment.Exit(0);
                    }
                    else if (key == ConsoleKey.C)
                    {
                        Console.Clear();
                        PrintMonitorRegistry();
                    }
                    else if (key == ConsoleKey.D)
                    {
                        Program.DumpXml = !Program.DumpXml;
                        Console.WriteLine($"Dump XML: {Program.DumpXml}");
                    }
                }
            });

            inputThread.IsBackground = true;
            inputThread.Start();
        }
    }
}