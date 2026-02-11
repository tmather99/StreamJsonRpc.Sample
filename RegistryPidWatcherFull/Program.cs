namespace RegistryMonitor;

class Program
{
    public static bool DumpXml = false;

    static void Main()
    {
        const string key = @"SOFTWARE\WorkspaceONE\Satori";
        const string keyFullPath = $@"MACHINE\{key}";

        ConsoleInput();

        // Set up the registry watcher and event log parser
        using var watcher = new RegistryWatcher(key);

        // Initial printout of help information
        var reader = new EventLogReader(keyFullPath);

        while (true)
        {
            PrintHelp();

            // Wait for a registry change event to be logged before attempting to read it
            watcher.WaitForChange();

            // Small delay to ensure the event log is updated before we read it
            Thread.Sleep(1000);

            // Read and display new events from the event log
            foreach (var (pid,
                          proc,
                          regKey,
                          valueName,
                          accessMaskRaw,
                          accessMaskText,
                          operationType,
                          newValue,
                          inferredAction) in reader.ReadEvents())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now}]");
                Console.ResetColor();
                Console.WriteLine($"  Key: {regKey}");
                Console.WriteLine($"  ValueName: {valueName}");
                Console.WriteLine($"  NewValue: {newValue}");
                Console.WriteLine($"  PID: {pid}");
                Console.WriteLine($"  Process: {proc}");
                Console.WriteLine($"  OperationType: {operationType}");
                Console.WriteLine($"  AccessMask: {accessMaskRaw} ({accessMaskText})");
                Console.WriteLine($"  InferredAction: {inferredAction}");
                Console.WriteLine();
            }
        }

        void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Monitoring registry key:  {keyFullPath}\n");
            Console.ResetColor();
        }

        void ConsoleInput()
        {
            var inputThread = new Thread(() =>
            {
                while (true)
                {
                    var key = Console.ReadKey(true).Key; // blocks here, not main thread

                    if (key == ConsoleKey.Escape)
                    {
                        Console.WriteLine("Exiting...");
                        Environment.Exit(0);
                    }
                    else if (key == ConsoleKey.C)
                    {
                        Console.Clear();
                    }
                    else if (key == ConsoleKey.D)
                    {
                        Program.DumpXml = !Program.DumpXml;
                        Console.WriteLine($"Dump XML: {Program.DumpXml}");
                    }

                    PrintHelp();
                }
            });

            inputThread.IsBackground = true;
            inputThread.Start();
        }
    }
}