// Program.cs

using System;
using System.Threading;
using EtwProcessMonitor;

Console.WriteLine("=== ETW Process Exit Monitor ===");
Console.WriteLine("Requires: Windows, elevated (Administrator)");
Console.WriteLine("Set ETWMON_DIAG=1 to print raw hex dumps of each event.\n");

bool diag = Environment.GetEnvironmentVariable("ETWMON_DIAG") == "1";

using var monitor = new SystemProcessExitMonitor();

monitor.ProcessExited += (_, e) =>
{
    string exitInfo = e.ExitCode == 0
        ? $"ExitCode=0 (SUCCESS)"
        : $"ExitCode={e.ExitCode} ({e.ExitCodeDescription})";

    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss.fff}] EXIT  PID={e.ProcessId,-6} " +
        $"{exitInfo,-40} Image={e.ImageName}");
};

if (diag)
{
    monitor.DiagnosticEvent += (_, msg) =>
        Console.WriteLine($"  DIAG: {msg}");
}

try
{
    monitor.Start();
    Console.WriteLine("Listening... (Ctrl+C or Enter to stop)\n");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to start: {ex.Message}");
    Console.Error.WriteLine("Make sure you are running as Administrator.");
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
new Thread(() => { Console.ReadLine(); cts.Cancel(); }) { IsBackground = true }.Start();

cts.Token.WaitHandle.WaitOne();
Console.WriteLine("\nStopping...");
monitor.Stop();
Console.WriteLine("Done.");