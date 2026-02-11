namespace RegistryMonitor;

class Program
{
    public static bool DumpXml = false;

    static void Main()
    {
        const string key = @"SOFTWARE\WorkspaceONE\Satori";
        const string fullPath = @"MACHINE\SOFTWARE\WorkspaceONE\Satori";

        using var watcher = new RegistryWatcher(key);
        var reader = new EventLogParser(fullPath);

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

        void PrintHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Monitoring registry key:  {fullPath}\n");
            Console.ResetColor();
        }

        inputThread.IsBackground = true;
        inputThread.Start();

        while (true)
        {
            PrintHelp();

            watcher.WaitForChange();
            Thread.Sleep(1000);
            foreach (var (pid, proc, regKey, valueName, accessMaskRaw, accessMaskText, operationType, newValue, inferredAction) in reader.ReadEvents())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{DateTime.Now}]");
                Console.ResetColor();
                Console.WriteLine($"  Key: {regKey}");
                Console.WriteLine($"  Value: {valueName}");
                Console.WriteLine($"  NewValue: {newValue}");
                Console.WriteLine($"  PID: {pid}");
                Console.WriteLine($"  Process: {proc}");
                Console.WriteLine($"  OperationType: {operationType}");
                Console.WriteLine($"  AccessMask: {accessMaskRaw} ({accessMaskText})");
                Console.WriteLine($"  InferredAction: {inferredAction}");
                Console.WriteLine();
            }
        }
    }
}