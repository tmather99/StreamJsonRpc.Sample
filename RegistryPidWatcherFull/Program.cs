namespace RegistryMonitor;

class Program
{
    public static bool DumpXml = false;

    static void Main()
    {
        // event log source is "USER\Software\MyApp1"
        const string hkcuSubKey = @"Software\MyApp1";
        const string hkcuKeyFullPath = $@"USER\{hkcuSubKey}";

        // event log source is "MACHINE\SOFTWARE\WorkspaceONE\Satori"
        const string hklmSubKey = @"SOFTWARE\WorkspaceONE\Satori";
        const string hklmKeyFullPath = $@"MACHINE\{hklmSubKey}";

        ConsoleInput();

        // Set up the registry watchers and event log readers for both HKCU and HKLM
        using var hkcuWatcher = new RegistryWatcher(hkcuSubKey, useCurrentUser: true);
        using var hklmWatcher = new RegistryWatcher(hklmSubKey);

        var hkcuReader = new EventLogReader(hkcuKeyFullPath);
        var hklmReader = new EventLogReader(hklmKeyFullPath);

        while (true)
        {
            // Wait for either HKCU or HKLM to have a change
            WaitForAnyChange();

            // Read and display new events from the HKLM event log
            ReadEvents(hkcuReader);
            ReadEvents(hklmReader);
        }

        // Local function to wait for either HKCU or HKLM change
        void WaitForAnyChange()
        {
            PrintHelp();

            // wait for either HKCU or HKLM
            int index = WaitHandle.WaitAny(new[] { hkcuWatcher.WaitHandle, hklmWatcher.WaitHandle });

            // re-arm whichever fired
            if (index == 0)
            {
                hkcuWatcher.WaitForChange();
            }
            else
            {
                hklmWatcher.WaitForChange();
            }

            // small delay to ensure event log is written
            Thread.Sleep(1000);
        }

        // Local function to read and display events from an EventLogReader
        void ReadEvents(EventLogReader eventLogReader)
        {
            // Read and display new events from the HKCU event log
            foreach (var (pid,
                         proc,
                         regKey,
                         valueName,
                         accessMaskRaw,
                         accessMaskText,
                         operationType,
                         newValue,
                         inferredAction) in eventLogReader.ReadEvents())
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
                    var key = Console.ReadKey(true).Key; // blocks here, not main thread

                    if (key == ConsoleKey.Escape)
                    {
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

                    PrintHelp();
                }
            });

            inputThread.IsBackground = true;
            inputThread.Start();
        }
    }
}