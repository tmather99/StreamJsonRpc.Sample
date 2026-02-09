# Process Crash & Termination Monitor - Windows Edition

A Windows .NET 10 console application that detects and reports **process crashes and terminations** using the **IObservable/IObserver** pattern (Reactive Extensions) via Windows Event Log monitoring.

## Features

- **Automatic Audit Configuration**: Automatically enables Process Termination auditing on startup
- **Administrator Detection**: Checks for admin privileges and provides clear error messages
- **Comprehensive Event Log Monitoring**:
  - **Crashes**: Application crashes, hangs, and runtime errors
  - **Process Terminations**: Process exits via Security Event Log (Event ID 4689)
  - **Service Changes**: Service stops via System Event Log (Event ID 7036)
- **IObservable/IObserver Pattern**: Clean separation of detection from handling
- **Dual Observable Streams**: 
  - `CrashEvents` - Abnormal terminations only
  - `TerminationEvents` - Process exits tracked by Windows
- **Multiple Observers**: Console display, file logging, and statistics for both streams
- **Real-time Monitoring**: All via Windows Event Log - no WMI dependencies
- **Process Information**: Process name, PID, exit status, user, executable path
- **Detailed Reports**: Exception codes, faulting modules, crash descriptions

## What Gets Detected

### Crash Events (Application Event Log)
- Application crashes (Event ID 1000, 1001)
- Application hangs (Event ID 1002)
- .NET Runtime errors (Event ID 1026)
- Windows Error Reporting events

### Termination Events (Security & System Event Logs)
- **Event ID 4689** (Security Log): Process exits
  - Includes process name, PID, exit status, user
  - Requires Process Tracking audit enabled
- **Event ID 7036** (System Log): Service state changes
  - Detects when Windows services stop

## Prerequisites for Termination Monitoring

⚠️ **Important**: To monitor process terminations (Event ID 4689), audit logging must be enabled.

### Automatic Configuration (Recommended)

**The application will automatically enable Process Termination auditing when you run it as Administrator!**

Simply run the application as Administrator and it will:
1. Check if Process Termination auditing is enabled
2. If not enabled, automatically configure it using `auditpol`
3. Display the status in the console

### Manual Configuration (If Automatic Fails)

If automatic configuration doesn't work, you can enable it manually:

**Option 1: Local Security Policy (GUI)**
1. Press `Win + R`, type `secpol.msc`, press Enter
2. Navigate to: **Local Policies** → **Audit Policy**
3. Double-click **Audit process tracking**
4. Check **Success** (and optionally **Failure**)
5. Click OK

**Option 2: Command Line (Run as Administrator)**
```cmd
auditpol /set /category:"Detailed Tracking" /subcategory:"Process Termination" /success:enable
```

To verify it's enabled:
```cmd
auditpol /get /category:"Detailed Tracking"
```

## Architecture

```
┌──────────────────┐
│ ProcessMonitor   │ (Observable - monitors Event Logs)
│                  │
│ - CrashEvents    │──────┬─────► ConsoleCrashObserver
│   (IObservable)  │      ├─────► FileLogCrashObserver
│                  │      └─────► CrashStatisticsObserver
│                  │
│ - Termination    │──────┬─────► ConsoleTerminationObserver  
│   Events         │      ├─────► FileLogTerminationObserver
│   (IObservable)  │      └─────► TerminationStatisticsObserver
└──────────────────┘
```

### Key Components

**ProcessMonitor** (Observable):
- Creates EventLogWatcher for crash detection (Application log)
- Creates EventLogWatcher for process exits (Security log - Event 4689)
- Creates EventLogWatcher for service changes (System log - Event 7036)
- Publishes events through two separate IObservable streams

**CrashInfo** (Model):
- Process name, PID, exception code, faulting module
- Event properties and full description

**TerminationInfo** (Model):
- Process name, PID, exit status
- User name, executable path, timestamps

**6 Observers** (3 for crashes + 3 for terminations):
- Console observers with color-coded output
- File log observers (crash_log.txt, termination_log.txt)
- Statistics observers for aggregation

## Requirements

- .NET 10 SDK
- Windows OS
- **Administrator privileges** (required for Event Log access)
- **Audit Process Tracking enabled** (for termination monitoring)
- System.Reactive NuGet package (v6.0.0)

## Building

```bash
dotnet restore
dotnet build
```

## Running

**Must run as Administrator:**

```bash
# Using dotnet
dotnet run

# Or run the executable directly (right-click → Run as administrator)
.\bin\Debug\net10.0-windows\win-x64\ProcessCrashMonitor.exe
```

⚠️ **IMPORTANT**: 
1. **Run with administrator privileges** (required)
2. The application will **automatically enable Process Termination auditing** on first run
3. If automatic configuration fails, you can enable it manually (see Prerequisites)

### First Run Output

When you run the application for the first time, you'll see:

```
Process Crash & Termination Monitor - Windows Edition
================================================
Running with Administrator privileges ✓

Checking audit policy for Process Termination...
✓ Process Termination auditing enabled successfully

Crash Observers subscribed:
  - Console output observer
  ...
```

## Output Example


```
Process Crash & Termination Monitor - Windows Edition
================================================
Using IObservable/IObserver Pattern
Monitoring for:
  - Application crashes and hangs
  - Process terminations (via Event Log)
Press Ctrl+C to stop monitoring
================================================

Crash Observers subscribed:
  - Console output observer
  - File logging observer (crash_log.txt)
  - Statistics observer

Termination Observers subscribed:
  - Console output observer
  - File logging observer (termination_log.txt)
  - Statistics observer

Started 4 event log watchers for crashes.
Started Security event log watcher for process terminations.
Started System event log watcher for service state changes.

[2024-02-07 14:23:30] PROCESS TERMINATED
┌─ Process Name: notepad.exe
├─ Process ID (PID): 8456
├─ Exit Status: 0x0
├─ User: DOMAIN\username
└─ Executable: C:\Windows\System32\notepad.exe
--------------------------------------------------------------------------------

[2024-02-07 14:23:45] CRASH DETECTED
Source: Application Crash
Event ID: 1000

┌─ Process Name: MyApplication.exe
├─ Process ID (PID): 12345
├─ Exception Code: 0xc0000005
└─ Faulting Module: KERNELBASE.dll

Event Properties:
  Application Name: MyApplication.exe
  Application Version: 1.0.0.0
  Exception Code: 0xc0000005
  Faulting Module Name: KERNELBASE.dll

Full Description:
Faulting application name: MyApplication.exe, version: 1.0.0.0
Exception code: 0xc0000005 (Access Violation)
--------------------------------------------------------------------------------

[2024-02-07 14:24:12] PROCESS TERMINATED
┌─ Process Name: Windows Update Service (Service)
├─ Process ID (PID): 0
└─ Exit Status: Service Stopped
--------------------------------------------------------------------------------

[Ctrl+C pressed]

Stopping monitor...

Final Statistics:

=== CRASH STATISTICS ===
Total Crashes Detected: 3
Crashes by Source:
  Application Crash: 2
  .NET Runtime Error: 1
Crashes by Process:
  MyApplication.exe: 2
  TestApp.exe: 1

=== TERMINATION STATISTICS ===
Total Terminations Detected: 24
Top 10 Processes by Termination Count:
  notepad.exe: 5
  cmd.exe: 4
  powershell.exe: 3
  ...
Terminations by User:
  DOMAIN\username: 20
  NT AUTHORITY\SYSTEM: 4

Monitoring stopped.
```

## Understanding Event Log-Based Termination Monitoring

**How It Works:**
- Uses **Event ID 4689** from the Security log
- Only logged when "Audit Process Tracking" is enabled
- Provides process name, PID, exit status, and user information
- Also monitors **Event ID 7036** for Windows service stops

**Important Notes:**
- ⚠️ **Requires audit policy enabled** (see Prerequisites section)
- Events are only logged when processes exit AFTER monitoring starts
- Not all processes may be captured (depends on Windows auditing scope)
- System processes and services are included
- Less verbose than WMI but more reliable and supported

**Advantages over WMI:**
- No COM/WMI dependencies
- More stable and supported by Microsoft
- Integrates with existing Event Log infrastructure
- Better performance for long-running monitoring
- Easier to filter using Event Log queries

## Event IDs

**Crash Events:**
- **1000**: Application Error (crash)
- **1001**: Windows Error Reporting
- **1002**: Application Hang
- **1026**: .NET Runtime unhandled exception

**Termination Events:**
- **4689**: A process has exited (Security log)
- **7036**: Service state change (System log)

## Filtering Termination Events

Filter events using Reactive Extensions LINQ operators:

### Filter by Process Name
```csharp
// Only monitor specific processes
monitor.TerminationEvents
    .Where(t => t.ProcessName.Equals("MyApp.exe", StringComparison.OrdinalIgnoreCase))
    .Subscribe(terminationObserver);
```

### Filter by User
```csharp
// Only monitor processes terminated by specific user
monitor.TerminationEvents
    .Where(t => t.UserName?.Contains("username") == true)
    .Subscribe(terminationObserver);
```

### Filter by Executable Path
```csharp
// Only monitor processes from a specific directory
monitor.TerminationEvents
    .Where(t => t.ExecutablePath?.StartsWith(@"C:\MyApps\") == true)
    .Subscribe(terminationObserver);
```

### Exclude System Processes
```csharp
// Ignore processes from Windows directory
monitor.TerminationEvents
    .Where(t => !t.ExecutablePath?.StartsWith(@"C:\Windows\", 
                StringComparison.OrdinalIgnoreCase) == true)
    .Subscribe(terminationObserver);
```

### Custom Filter Observer
```csharp
public class FilteredTerminationObserver : IObserver<TerminationInfo>
{
    private readonly HashSet<string> _monitoredProcesses = new()
    {
        "myapp.exe",
        "service.exe",
        "worker.exe"
    };

    public void OnNext(TerminationInfo termination)
    {
        if (_monitoredProcesses.Contains(termination.ProcessName.ToLower()))
        {
            Console.WriteLine($"ALERT: {termination.ProcessName} terminated!");
            Console.WriteLine($"  PID: {termination.ProcessId}");
            Console.WriteLine($"  User: {termination.UserName}");
        }
    }
    
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
```

## Observable Pattern Benefits

1. **Separation of Concerns**: Detection logic separate from handling logic
2. **Multiple Subscribers**: Multiple observers process the same events simultaneously
3. **Easy Extension**: Add new observers without modifying existing code
4. **Thread Safety**: Built-in synchronization through Reactive Extensions
5. **Composability**: Filter, transform, throttle events using LINQ operators
6. **Dual Streams**: Crashes and terminations are separate observables
7. **Event Log Native**: Uses Windows Event Log infrastructure (no WMI dependencies)

## Extending with Custom Observers

Add custom observers by implementing `IObserver<T>`:

```csharp
public class EmailAlertObserver : IObserver<CrashInfo>
{
    public void OnNext(CrashInfo crash)
    {
        // Send email alert for critical crashes
        if (crash.ExceptionCode == "0xc0000005") // Access Violation
        {
            SendEmailAlert(crash);
        }
    }

    public void OnError(Exception error) { }
    public void OnCompleted() { }
}

// Subscribe it
var emailObserver = new EmailAlertObserver();
using var subscription = monitor.CrashEvents.Subscribe(emailObserver);
```

## Advanced Rx Usage

Leverage Reactive Extensions LINQ operators:

```csharp
// Only observe .NET runtime crashes
monitor.CrashEvents
    .Where(crash => crash.EventId == 1026)
    .Subscribe(new DotNetCrashObserver());

// Throttle crash notifications to avoid spam
monitor.CrashEvents
    .Throttle(TimeSpan.FromSeconds(5))
    .Subscribe(consoleObserver);

// Monitor specific process terminations
monitor.TerminationEvents
    .Where(t => t.ProcessName == "myservice.exe")
    .Subscribe(serviceObserver);

// Count terminations per minute
monitor.TerminationEvents
    .Buffer(TimeSpan.FromMinutes(1))
    .Subscribe(batch => Console.WriteLine($"Processes terminated in last minute: {batch.Count}"));

// Alert on processes terminated by specific user
monitor.TerminationEvents
    .Where(t => t.UserName?.Contains("SYSTEM") != true)
    .Subscribe(t => Console.WriteLine($"User-initiated termination: {t.ProcessName}"));
```

## Permissions

**Windows**: Must run as Administrator for:
- Event Log subscription access
- Security log access (for Event ID 4689)

Right-click the executable and select "Run as administrator".

## Output Files

- **crash_log.txt**: Detailed log of all crashes with full information
- **termination_log.txt**: Detailed log of all process terminations
- Both files are created in the same directory as the executable

## Stopping the Monitor

Press `Ctrl+C` to gracefully stop monitoring. All observers will be notified via `OnCompleted()`.

## Testing

### Test Crash Detection
Use the included `TestCrashApp.cs` to generate test crashes:

```bash
# Compile the test app
csc TestCrashApp.cs

# Run it to generate a crash
.\TestCrashApp.exe
```

### Test Termination Detection
1. Ensure Process Tracking audit is enabled
2. Run the monitor as Administrator
3. Open and close Notepad or any other application
4. Observe termination events in the console

## Troubleshooting

### "This application must be run as Administrator"
**Solution**: Right-click the executable and select "Run as administrator".

### No termination events appearing
**Possible causes:**

1. **Automatic audit configuration failed**
   - Check the startup message - did it say "✓ Process Termination auditing enabled successfully"?
   - If it shows a warning, the automatic configuration failed
   - **Fix**: Enable manually using the commands in the Prerequisites section

2. **Audit policy not enabled**
   - Verify with: `auditpol /get /category:"Detailed Tracking"`
   - Look for "Process Termination" with "Success" enabled
   - **Fix**: Run `auditpol /set /category:"Detailed Tracking" /subcategory:"Process Termination" /success:enable`

3. **Security log is full or disabled**
   - **Fix**: Check Event Viewer → Windows Logs → Security
   - Increase log size or enable log archiving

4. **Process terminated before monitoring started**
   - **Note**: Only processes that exit AFTER the monitor starts are captured

### Verify audit is enabled
```cmd
auditpol /get /category:"Detailed Tracking"
```

Look for:
```
Process Termination                Success
```

### Too many events
**Solution**: Use filtering observers (see Filtering section) to focus on specific processes.

### System.Reactive not found
```bash
dotnet restore
```

## Performance Considerations

- Event Log monitoring is efficient and lightweight
- Filtering at the query level (XPath) is faster than filtering in code
- Consider using `.Throttle()` or `.Sample()` for high-frequency events
- Large numbers of termination events are normal on busy systems

## Security Considerations

- Process termination logging may contain sensitive information
- Log files may grow large on busy systems
- Consider log rotation and retention policies
- Restrict access to log files appropriately

## License

This is a sample application for educational purposes.



## Prerequisites Required
To see termination events, you must enable audit logging:

   auditpol /set /category:"Detailed Tracking" /subcategory:"Process Termination" /success:enable

   Or via GUI: secpol.msc → Audit Policy → Audit process tracking

## Advantages over WMI
✅ More reliable - native Windows Event Log infrastructure
✅ Better performance - no COM/WMI overhead
✅ Officially supported - Microsoft recommended approach
✅ Integrates well - same mechanism as crash detection
✅ Easier filtering - can use XPath queries in Event Log


⚠️ Event ID 4689 requires audit policy enabled - without this, no termination events are logged
⚠️ Only captures events after monitoring starts - historical events need Event Viewer
⚠️ Less verbose than WMI - but more stable and appropriate for production use

The appl