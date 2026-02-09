using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Principal;

namespace ProcessCrashMonitor;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Process Crash & Termination Monitor - Windows Edition");
        Console.WriteLine("================================================");
        Console.WriteLine("Using IObservable/IObserver Pattern");
        Console.WriteLine("Monitoring for:");
        Console.WriteLine("  - Application crashes and hangs");
        Console.WriteLine("  - Process terminations (via Event Log)");
        Console.WriteLine("Press Ctrl+C to stop monitoring");
        Console.WriteLine("================================================\n");

        // Check if running as administrator
        if (!IsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: This application must be run as Administrator.");
            Console.WriteLine("Right-click the executable and select 'Run as administrator'.");
            Console.ResetColor();
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Running with Administrator privileges ✓\n");

        // Try to enable audit policy for process termination
        Console.WriteLine("Checking audit policy for Process Termination...");
        if (EnableProcessTerminationAudit())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Process Termination auditing enabled successfully");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Could not enable Process Termination auditing automatically");
            Console.WriteLine("  You may need to enable it manually via:");
            Console.WriteLine("  auditpol /set /category:\"Detailed Tracking\" /subcategory:\"Process Termination\" /success:enable");
            Console.ResetColor();
        }
        Console.WriteLine();

        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("\n\nStopping monitor...");
        };

        try
        {
            var monitor = new ProcessMonitor();

            // Subscribe observers to the crash events
            var consoleObserver = new ConsoleCrashObserver();
            var fileLogObserver = new FileLogCrashObserver("crash_log.txt");
            var statsObserver = new CrashStatisticsObserver();

            // Subscribe observers to process termination events
            var terminationConsoleObserver = new ConsoleTerminationObserver();
            var terminationFileObserver = new FileLogTerminationObserver("termination_log.txt");
            var terminationStatsObserver = new TerminationStatisticsObserver();

            using var crashSub1 = monitor.CrashEvents.Subscribe(consoleObserver);
            using var crashSub2 = monitor.CrashEvents.Subscribe(fileLogObserver);
            using var crashSub3 = monitor.CrashEvents.Subscribe(statsObserver);

            using var termSub1 = monitor.TerminationEvents.Subscribe(terminationConsoleObserver);
            using var termSub2 = monitor.TerminationEvents.Subscribe(terminationFileObserver);
            using var termSub3 = monitor.TerminationEvents.Subscribe(terminationStatsObserver);

            Console.WriteLine("Crash Observers subscribed:");
            Console.WriteLine("  - Console output observer");
            Console.WriteLine("  - File logging observer (crash_log.txt)");
            Console.WriteLine("  - Statistics observer");
            Console.WriteLine();

            Console.WriteLine("Termination Observers subscribed:");
            Console.WriteLine("  - Console output observer");
            Console.WriteLine("  - File logging observer (termination_log.txt)");
            Console.WriteLine("  - Statistics observer");
            Console.WriteLine();

            await monitor.StartMonitoringAsync(cts.Token);

            Console.WriteLine("\nFinal Statistics:");
            Console.WriteLine("\n=== CRASH STATISTICS ===");
            statsObserver.PrintStatistics();
            Console.WriteLine("\n=== TERMINATION STATISTICS ===");
            terminationStatsObserver.PrintStatistics();
        }
        catch (UnauthorizedAccessException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nERROR: Access denied.");
            Console.WriteLine("Please run this application as Administrator.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.ResetColor();
        }

        Console.WriteLine("\nMonitoring stopped.");
    }

    static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    static bool EnableProcessTerminationAudit()
    {
        try
        {
            // First, check current status
            var checkProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "auditpol.exe",
                    Arguments = "/get /category:\"Detailed Tracking\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            checkProcess.Start();
            var output = checkProcess.StandardOutput.ReadToEnd();
            checkProcess.WaitForExit();

            // Check if Process Termination is already enabled
            if (output.Contains("Process Termination") && output.Contains("Success"))
            {
                Console.WriteLine("  Process Termination auditing is already enabled");
                return true;
            }

            // Enable Process Termination auditing
            var enableProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "auditpol.exe",
                    Arguments = "/set /category:\"Detailed Tracking\" /subcategory:\"Process Termination\" /success:enable",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Request elevation if needed
                }
            };

            enableProcess.Start();
            var enableOutput = enableProcess.StandardOutput.ReadToEnd();
            var enableError = enableProcess.StandardError.ReadToEnd();
            enableProcess.WaitForExit();

            if (enableProcess.ExitCode == 0)
            {
                Console.WriteLine("  Successfully enabled Process Termination auditing");
                return true;
            }
            else
            {
                Console.WriteLine($"  Auditpol exit code: {enableProcess.ExitCode}");
                if (!string.IsNullOrEmpty(enableError))
                {
                    Console.WriteLine($"  Error: {enableError}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Exception while enabling audit policy: {ex.Message}");
            return false;
        }
    }
}

// Model for crash information
public record CrashInfo(
    DateTime Timestamp,
    string Source,
    int EventId,
    string? ProcessName,
    int? ProcessId,
    string? ExceptionCode,
    string? FaultingModule,
    string? Description,
    Dictionary<string, string> Properties
);

// Model for process termination information
public record TerminationInfo(
    DateTime Timestamp,
    string ProcessName,
    int ProcessId,
    string? ExitStatus,
    string? ExecutablePath,
    string? UserName,
    DateTime? StartTime,
    DateTime? EndTime
);

// Observable process monitor
public class ProcessMonitor
{
    private readonly Subject<CrashInfo> _crashSubject = new();
    private readonly Subject<TerminationInfo> _terminationSubject = new();
    private readonly List<EventLogWatcher> _watchers = new();

    public IObservable<CrashInfo> CrashEvents => _crashSubject.AsObservable();
    public IObservable<TerminationInfo> TerminationEvents => _terminationSubject.AsObservable();

    public async Task StartMonitoringAsync(CancellationToken cancellationToken)
    {
        // Create event log watchers for crashes
        CreateCrashWatcher(
            "Application",
            "*[System[(EventID=1000 or EventID=1001)]]",
            "Application Crash"
        );

        CreateCrashWatcher(
            "Application",
            "*[System[Provider[@Name='Windows Error Reporting']]]",
            "Windows Error Reporting"
        );

        CreateCrashWatcher(
            "Application",
            "*[System[(EventID=1002)]]",
            "Application Hang"
        );

        CreateCrashWatcher(
            "Application",
            "*[System[Provider[@Name='.NET Runtime'] and (EventID=1026)]]",
            ".NET Runtime Error"
        );

        Console.WriteLine($"Started {_watchers.Count} event log watchers for crashes.");

        // Create event log watcher for process terminations
        // Event ID 4689 - A process has exited (Security log)
        // Event ID 5 - The Event Log service was stopped (can indicate shutdown)
        try
        {
            CreateTerminationWatcher(
                "Security",
                "*[System[(EventID=4689)]]",
                "Process Exit"
            );
            Console.WriteLine("Started Security event log watcher for process terminations.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not start Security log monitoring: {ex.Message}");
            Console.WriteLine("Process termination events require:");
            Console.WriteLine("  1. Administrator privileges");
            Console.WriteLine("  2. Audit Process Tracking enabled in Local Security Policy");
            Console.WriteLine("     Run: secpol.msc > Local Policies > Audit Policy > Audit process tracking");
        }

        // Alternative: Monitor System log for service stops (Event ID 7036)
        try
        {
            CreateTerminationWatcher(
                "System",
                "*[System[Provider[@Name='Service Control Manager'] and (EventID=7036)]]",
                "Service State Change"
            );
            Console.WriteLine("Started System event log watcher for service state changes.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not start System log monitoring: {ex.Message}");
        }

        Console.WriteLine();

        // Start all watchers
        foreach (var watcher in _watchers)
        {
            watcher.Enabled = true;
        }

        // Wait until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            // Stop all watchers
            foreach (var watcher in _watchers)
            {
                watcher.Enabled = false;
                watcher.Dispose();
            }

            _crashSubject.OnCompleted();
            _terminationSubject.OnCompleted();
        }
    }

    private void CreateCrashWatcher(string logName, string query, string source)
    {
        var eventQuery = new EventLogQuery(logName, PathType.LogName, query);
        var watcher = new EventLogWatcher(eventQuery);

        watcher.EventRecordWritten += (sender, e) =>
        {
            if (e.EventRecord != null)
            {
                var crashInfo = ParseEventRecord(e.EventRecord, source);
                _crashSubject.OnNext(crashInfo);
            }
        };

        _watchers.Add(watcher);
    }

    private void CreateTerminationWatcher(string logName, string query, string source)
    {
        var eventQuery = new EventLogQuery(logName, PathType.LogName, query);
        var watcher = new EventLogWatcher(eventQuery);

        watcher.EventRecordWritten += (sender, e) =>
        {
            if (e.EventRecord != null)
            {
                var terminationInfo = ParseTerminationEvent(e.EventRecord, source);
                if (terminationInfo != null)
                {
                    _terminationSubject.OnNext(terminationInfo);
                }
            }
        };

        _watchers.Add(watcher);
    }

    private TerminationInfo? ParseTerminationEvent(EventRecord record, string source)
    {
        try
        {
            if (record.Id == 4689) // Process Exit (Security log)
            {
                // Event ID 4689 structure:
                // Subject section (user who terminated)
                // Process Information section
                string? processName = null;
                int? processId = null;
                string? exitStatus = null;
                string? userName = null;

                // Try to get data from event XML
                var xml = record.ToXml();
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(xml);

                var nsmgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
                nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/win/2004/08/events/event");

                // Extract process name
                var processNameNode = doc.SelectSingleNode("//ns:Data[@Name='ProcessName']", nsmgr);
                if (processNameNode != null)
                {
                    var fullPath = processNameNode.InnerText;
                    processName = System.IO.Path.GetFileName(fullPath);
                }

                // Extract process ID (in hex)
                var processIdNode = doc.SelectSingleNode("//ns:Data[@Name='ProcessId']", nsmgr);
                if (processIdNode != null && !string.IsNullOrEmpty(processIdNode.InnerText))
                {
                    // Process ID is in hex format like 0x1234
                    var pidStr = processIdNode.InnerText.Replace("0x", "");
                    if (int.TryParse(pidStr, System.Globalization.NumberStyles.HexNumber, null, out int pid))
                    {
                        processId = pid;
                    }
                }

                // Extract exit status
                var exitStatusNode = doc.SelectSingleNode("//ns:Data[@Name='Status']", nsmgr);
                if (exitStatusNode != null)
                {
                    exitStatus = exitStatusNode.InnerText;
                }

                // Extract user name
                var subjectUserNode = doc.SelectSingleNode("//ns:Data[@Name='SubjectUserName']", nsmgr);
                var subjectDomainNode = doc.SelectSingleNode("//ns:Data[@Name='SubjectDomainName']", nsmgr);
                if (subjectUserNode != null && subjectDomainNode != null)
                {
                    userName = $"{subjectDomainNode.InnerText}\\{subjectUserNode.InnerText}";
                }

                if (processName != null && processId.HasValue)
                {
                    return new TerminationInfo(
                        Timestamp: record.TimeCreated ?? DateTime.Now,
                        ProcessName: processName,
                        ProcessId: processId.Value,
                        ExitStatus: exitStatus,
                        ExecutablePath: processNameNode?.InnerText,
                        UserName: userName,
                        StartTime: null,
                        EndTime: record.TimeCreated
                    );
                }
            }
            else if (record.Id == 7036) // Service state change
            {
                // Check if service stopped
                var description = record.FormatDescription();
                if (description != null && description.Contains("stopped"))
                {
                    // Extract service name from description
                    // Format: "The {ServiceName} service entered the stopped state."
                    var match = System.Text.RegularExpressions.Regex.Match(
                        description,
                        @"The (.+?) service entered the stopped state"
                    );

                    if (match.Success)
                    {
                        var serviceName = match.Groups[1].Value;
                        return new TerminationInfo(
                            Timestamp: record.TimeCreated ?? DateTime.Now,
                            ProcessName: $"{serviceName} (Service)",
                            ProcessId: record.ProcessId ?? 0,
                            ExitStatus: "Service Stopped",
                            ExecutablePath: null,
                            UserName: null,
                            StartTime: null,
                            EndTime: record.TimeCreated
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing termination event: {ex.Message}");
        }

        return null;
    }

    private CrashInfo ParseEventRecord(EventRecord record, string source)
    {
        string? processName = null;
        int? processId = null;
        string? exceptionCode = null;
        string? faultingModule = null;
        var properties = new Dictionary<string, string>();

        try
        {
            // Extract process ID from event
            processId = record.ProcessId;

            // Parse event properties based on event ID
            if (record.Properties.Count > 0)
            {
                if (record.Id == 1000 || record.Id == 1001) // Application Error
                {
                    if (record.Properties.Count > 0 && record.Properties[0]?.Value != null)
                        processName = record.Properties[0].Value.ToString();

                    if (record.Properties.Count > 6 && record.Properties[6]?.Value != null)
                        exceptionCode = record.Properties[6].Value.ToString();

                    if (record.Properties.Count > 3 && record.Properties[3]?.Value != null)
                        faultingModule = record.Properties[3].Value.ToString();

                    // Store all properties
                    for (int i = 0; i < Math.Min(record.Properties.Count, 15); i++)
                    {
                        if (record.Properties[i]?.Value != null)
                        {
                            var label = GetPropertyLabel(record.Id, i);
                            properties[label] = record.Properties[i].Value.ToString() ?? "";
                        }
                    }
                }
                else if (record.Id == 1002) // Application Hang
                {
                    if (record.Properties.Count > 0 && record.Properties[0]?.Value != null)
                        processName = record.Properties[0].Value.ToString();

                    for (int i = 0; i < Math.Min(record.Properties.Count, 10); i++)
                    {
                        if (record.Properties[i]?.Value != null)
                        {
                            var label = GetPropertyLabel(record.Id, i);
                            properties[label] = record.Properties[i].Value.ToString() ?? "";
                        }
                    }
                }
                else if (record.Id == 1026) // .NET Runtime
                {
                    if (record.Properties.Count > 0 && record.Properties[0]?.Value != null)
                        processName = record.Properties[0].Value.ToString();

                    for (int i = 0; i < Math.Min(record.Properties.Count, 5); i++)
                    {
                        if (record.Properties[i]?.Value != null)
                        {
                            var label = GetPropertyLabel(record.Id, i);
                            properties[label] = record.Properties[i].Value.ToString() ?? "";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            properties["ParseError"] = ex.Message;
        }

        string? description = null;
        try
        {
            description = record.FormatDescription();
        }
        catch { }

        return new CrashInfo(
            Timestamp: DateTime.Now,
            Source: source,
            EventId: record.Id,
            ProcessName: processName,
            ProcessId: processId,
            ExceptionCode: exceptionCode,
            FaultingModule: faultingModule,
            Description: description,
            Properties: properties
        );
    }

    private static string GetPropertyLabel(int eventId, int index)
    {
        if (eventId == 1000 || eventId == 1001) // Application Error / WER (Application)
        {
            return index switch {
                0 => "Application Name",
                1 => "Application Version",
                2 => "Application Timestamp",
                3 => "Faulting Module Name",
                4 => "Faulting Module Version",
                5 => "Faulting Module Timestamp",
                6 => "Exception Code",
                7 => "Exception Offset",
                8 => "OS Version",
                9 => "Locale ID",
                _ => $"Property {index}"
            };
        }
        else if (eventId == 1002) // Application Hang
        {
            return index switch {
                0 => "Application Name",
                1 => "Application Version",
                2 => "Application Timestamp",
                3 => "Hang Signature",
                4 => "Hang Type",
                _ => $"Property {index}"
            };
        }
        else if (eventId == 1026 || eventId == 5000) // .NET Runtime
        {
            return index switch {
                0 => "Application",
                1 => "Exception",
                2 => "Additional Info 1",
                3 => "Additional Info 2",
                4 => "Additional Info 3",
                _ => $"Property {index}"
            };
        }
        else if (eventId == 1023 || eventId == 1025) // CLR initialization / failure
        {
            return index switch {
                0 => "Details 0",
                1 => "Details 1",
                2 => "Details 2",
                _ => $"Property {index}"
            };
        }
        else if (eventId == 1004 || eventId == 1005 || eventId == 1009) // Other app errors
        {
            return index switch {
                0 => "Application / Image",
                1 => "Module / Resource",
                _ => $"Property {index}"
            };
        }

        return $"Property {index}";
    }
}

// Observer: Console output for crashes
public class ConsoleCrashObserver : IObserver<CrashInfo>
{
    private readonly object _lock = new();

    public void OnNext(CrashInfo crash)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[{crash.Timestamp:yyyy-MM-dd HH:mm:ss}] CRASH DETECTED");
            Console.ResetColor();

            Console.WriteLine($"Source: {crash.Source}");
            Console.WriteLine($"Event ID: {crash.EventId}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            if (!string.IsNullOrEmpty(crash.ProcessName))
            {
                Console.WriteLine($"┌─ Process Name: {crash.ProcessName}");
            }
            if (crash.ProcessId.HasValue)
            {
                Console.WriteLine($"{(crash.ProcessName != null ? "├" : "┌")}─ Process ID (PID): {crash.ProcessId}");
            }
            if (!string.IsNullOrEmpty(crash.ExceptionCode))
            {
                Console.WriteLine($"{(crash.ProcessName != null || crash.ProcessId.HasValue ? "├" : "┌")}─ Exception Code: {crash.ExceptionCode}");
            }
            if (!string.IsNullOrEmpty(crash.FaultingModule))
            {
                Console.WriteLine($"└─ Faulting Module: {crash.FaultingModule}");
            }
            Console.ResetColor();

            if (crash.Properties.Count > 0)
            {
                Console.WriteLine("\nEvent Properties:");
                foreach (var prop in crash.Properties)
                {
                    Console.WriteLine($"  {prop.Key}: {prop.Value}");
                }
            }

            if (!string.IsNullOrEmpty(crash.Description))
            {
                Console.WriteLine("\nFull Description:");
                Console.WriteLine(crash.Description);
            }

            Console.WriteLine(new string('-', 80));
        }
    }

    public void OnError(Exception error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error in crash monitoring: {error.Message}");
        Console.ResetColor();
    }

    public void OnCompleted()
    {
        Console.WriteLine("\nConsole crash observer: Monitoring completed.");
    }
}

// Observer: Console output for terminations
public class ConsoleTerminationObserver : IObserver<TerminationInfo>
{
    private readonly object _lock = new();

    public void OnNext(TerminationInfo termination)
    {
        lock (_lock)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[{termination.Timestamp:yyyy-MM-dd HH:mm:ss}] PROCESS TERMINATED");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"┌─ Process Name: {termination.ProcessName}");
            Console.WriteLine($"├─ Process ID (PID): {termination.ProcessId}");

            if (!string.IsNullOrEmpty(termination.ExitStatus))
            {
                var (hex, reason) = ExitStatusHelper.Describe(termination.ExitStatus);
                Console.WriteLine($"├─ Exit Status (raw): {termination.ExitStatus}");
                Console.WriteLine($"├─ Exit Status (hex): {hex}");
                Console.WriteLine($"├─ Exit Status (reason): {reason}");
            }

            if (!string.IsNullOrEmpty(termination.UserName))
            {
                Console.WriteLine($"├─ User: {termination.UserName}");
            }

            if (!string.IsNullOrEmpty(termination.ExecutablePath))
            {
                Console.WriteLine($"└─ Executable: {termination.ExecutablePath}");
            }
            else
            {
                Console.WriteLine("└─ (Additional info not available)");
            }

            Console.ResetColor();
            Console.WriteLine(new string('-', 80));
        }
    }

    public void OnError(Exception error)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error in termination monitoring: {error.Message}");
        Console.ResetColor();
    }

    public void OnCompleted()
    {
        Console.WriteLine("\nConsole termination observer: Monitoring completed.");
    }
}

// Observer: File logger for crashes
public class FileLogCrashObserver : IObserver<CrashInfo>
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogCrashObserver(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public void OnNext(CrashInfo crash)
    {
        lock (_lock)
        {
            try
            {
                var logEntry = $"""
                    [{crash.Timestamp:yyyy-MM-dd HH:mm:ss}] CRASH DETECTED
                    Source: {crash.Source}
                    Event ID: {crash.EventId}
                    Process Name: {crash.ProcessName ?? "N/A"}
                    Process ID: {crash.ProcessId?.ToString() ?? "N/A"}
                    Exception Code: {crash.ExceptionCode ?? "N/A"}
                    Faulting Module: {crash.FaultingModule ?? "N/A"}
                    
                    Properties:
                    {string.Join("\n", crash.Properties.Select(p => $"  {p.Key}: {p.Value}"))}
                    
                    Description:
                    {crash.Description ?? "N/A"}
                    
                    {new string('=', 80)}
                    
                    """;

                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to crash log file: {ex.Message}");
            }
        }
    }

    public void OnError(Exception error)
    {
        File.AppendAllText(_logFilePath, $"ERROR: {error.Message}\n\n");
    }

    public void OnCompleted()
    {
        File.AppendAllText(_logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Crash monitoring completed.\n\n");
    }
}

// Observer: File logger for terminations
public class FileLogTerminationObserver : IObserver<TerminationInfo>
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogTerminationObserver(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public void OnNext(TerminationInfo termination)
    {
        lock (_lock)
        {
            try
            {
                var (hex, reason) = ExitStatusHelper.Describe(termination.ExitStatus);

                var logEntry = $"""
                    [{termination.Timestamp:yyyy-MM-dd HH:mm:ss}] PROCESS TERMINATED
                    Process Name: {termination.ProcessName}
                    Process ID: {termination.ProcessId}
                    Exit Status (raw): {termination.ExitStatus ?? "N/A"}
                    Exit Status (hex): {hex}
                    Exit Status (reason): {reason}
                    User: {termination.UserName ?? "N/A"}
                    Executable Path: {termination.ExecutablePath ?? "N/A"}
                    
                    {new string('=', 80)}
                    
                    """;

                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to termination log file: {ex.Message}");
            }
        }
    }

    public void OnError(Exception error)
    {
        File.AppendAllText(_logFilePath, $"ERROR: {error.Message}\n\n");
    }

    public void OnCompleted()
    {
        File.AppendAllText(_logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Termination monitoring completed.\n\n");
    }
}

// Observer: Statistics collector for crashes
public class CrashStatisticsObserver : IObserver<CrashInfo>
{
    private readonly object _lock = new();
    private int _totalCrashes = 0;
    private readonly Dictionary<string, int> _crashesBySource = new();
    private readonly Dictionary<string, int> _crashesByProcess = new();
    private readonly Dictionary<int, int> _crashesByEventId = new();

    public void OnNext(CrashInfo crash)
    {
        lock (_lock)
        {
            _totalCrashes++;

            // Count by source
            if (!_crashesBySource.ContainsKey(crash.Source))
                _crashesBySource[crash.Source] = 0;
            _crashesBySource[crash.Source]++;

            // Count by process name
            if (!string.IsNullOrEmpty(crash.ProcessName))
            {
                if (!_crashesByProcess.ContainsKey(crash.ProcessName))
                    _crashesByProcess[crash.ProcessName] = 0;
                _crashesByProcess[crash.ProcessName]++;
            }

            // Count by event ID
            if (!_crashesByEventId.ContainsKey(crash.EventId))
                _crashesByEventId[crash.EventId] = 0;
            _crashesByEventId[crash.EventId]++;
        }
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"Statistics observer error: {error.Message}");
    }

    public void OnCompleted()
    {
        Console.WriteLine("\nCrash statistics observer: Monitoring completed.");
    }

    public void PrintStatistics()
    {
        lock (_lock)
        {
            Console.WriteLine($"Total Crashes Detected: {_totalCrashes}");

            if (_crashesBySource.Count > 0)
            {
                Console.WriteLine("\nCrashes by Source:");
                foreach (var kvp in _crashesBySource.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (_crashesByProcess.Count > 0)
            {
                Console.WriteLine("\nCrashes by Process:");
                foreach (var kvp in _crashesByProcess.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (_crashesByEventId.Count > 0)
            {
                Console.WriteLine("\nCrashes by Event ID:");
                foreach (var kvp in _crashesByEventId.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }
    }
}

// Observer: Statistics collector for terminations
public class TerminationStatisticsObserver : IObserver<TerminationInfo>
{
    private readonly object _lock = new();
    private int _totalTerminations = 0;
    private readonly Dictionary<string, int> _terminationsByProcess = new();
    private readonly Dictionary<string, int> _terminationsByUser = new();

    public void OnNext(TerminationInfo termination)
    {
        lock (_lock)
        {
            _totalTerminations++;

            // Count by process name
            if (!_terminationsByProcess.ContainsKey(termination.ProcessName))
                _terminationsByProcess[termination.ProcessName] = 0;
            _terminationsByProcess[termination.ProcessName]++;

            // Count by user
            if (!string.IsNullOrEmpty(termination.UserName))
            {
                if (!_terminationsByUser.ContainsKey(termination.UserName))
                    _terminationsByUser[termination.UserName] = 0;
                _terminationsByUser[termination.UserName]++;
            }
        }
    }

    public void OnError(Exception error)
    {
        Console.WriteLine($"Termination statistics observer error: {error.Message}");
    }

    public void OnCompleted()
    {
        Console.WriteLine("\nTermination statistics observer: Monitoring completed.");
    }

    public void PrintStatistics()
    {
        lock (_lock)
        {
            Console.WriteLine($"Total Terminations Detected: {_totalTerminations}");

            if (_terminationsByProcess.Count > 0)
            {
                Console.WriteLine("\nTop 10 Processes by Termination Count:");
                foreach (var kvp in _terminationsByProcess.OrderByDescending(x => x.Value).Take(10))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }

            if (_terminationsByUser.Count > 0)
            {
                Console.WriteLine("\nTerminations by User:");
                foreach (var kvp in _terminationsByUser.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
        }
    }
}

public static class ExitStatusHelper
{
    public static (string Hex, string Reason) Describe(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ("N/A", "Unknown / not provided");
        }

        // Status from event 4689 is usually NTSTATUS in hex or decimal
        // Try hex first (handles both "0xC0000005" and "C0000005")
        string trimmed = status.Trim();
        string hexSource = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(2)
            : trimmed;

        if (!uint.TryParse(hexSource,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out uint code))
        {
            // Fall back to decimal parse
            if (!uint.TryParse(trimmed,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out code))
            {
                return (trimmed, "Unrecognized status format");
            }
        }

        string hex = $"0x{code:X8}";
        string reason = GetReason(code);

        return (hex, reason);
    }

    private static string GetReason(uint code)
    {
        // Common NTSTATUS / Win32 equivalents. Extend as needed.
        return code switch {
            0x00000000 => "STATUS_SUCCESS (normal exit)",
            0xC0000005 => "STATUS_ACCESS_VIOLATION (access violation / AV)",
            0xC000001D => "STATUS_ILLEGAL_INSTRUCTION",
            0xC0000008 => "STATUS_INVALID_HANDLE",
            0xC0000409 => "STATUS_STACK_BUFFER_OVERRUN",
            0xC000013A => "STATUS_CONTROL_C_EXIT (terminated by Ctrl+C or closing console)",
            0x40010004 => "DBG_TERMINATE_PROCESS",

            // Some common Win32 exit codes (if event stores them as decimal)
            0x00000002 => "ERROR_FILE_NOT_FOUND",
            0x00000003 => "ERROR_PATH_NOT_FOUND",
            0x00000057 => "ERROR_INVALID_PARAMETER",
            0x00000103 => "WAIT_TIMEOUT",

            _ => "Unknown code (NTSTATUS/Win32)"
        };
    }
}
