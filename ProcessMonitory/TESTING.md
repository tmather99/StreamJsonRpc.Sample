# Testing Guide for Process Crash & Termination Monitor

This guide explains how to test the monitoring application using the provided PowerShell scripts.

## Prerequisites

1. **Run the Monitor as Administrator**
   ```powershell
   # In Administrator PowerShell
   cd path\to\ProcessCrashMonitor
   dotnet run
   ```

2. **Ensure the monitor is running and shows:**
   ```
   ✓ Process Termination auditing enabled successfully
   Started 4 event log watchers for crashes.
   Started Security event log watcher for process terminations.
   ```

## Quick Testing (Recommended)

### QuickTest.ps1 - Simple and Fast

This script provides quick tests for both crashes and terminations.

**Usage:**

```powershell
# Test both crash and normal exit (default)
.\QuickTest.ps1

# Test only crashes
.\QuickTest.ps1 -TestType Crash

# Test only normal exits
.\QuickTest.ps1 -TestType Exit

# Stress test - create/terminate 10 processes rapidly
.\QuickTest.ps1 -TestType Stress
```

**What it tests:**
- ✓ Normal process termination (starts and kills Notepad)
- ✓ Application crash (Null Reference Exception)
- ✓ Stress testing (multiple rapid terminations)

**Expected Monitor Output:**

After running `.\QuickTest.ps1`, you should see:

```
[2024-02-08 10:30:15] PROCESS TERMINATED
┌─ Process Name: notepad.exe
├─ Process ID (PID): 12345
├─ Exit Status: 0x0
└─ User: DOMAIN\username
--------------------------------------------------------------------------------

[2024-02-08 10:30:18] CRASH DETECTED
Source: Application Crash
Event ID: 1000
┌─ Process Name: powershell.exe
├─ Process ID (PID): 67890
├─ Exception Code: 0xe0434352
└─ Faulting Module: KERNELBASE.dll
--------------------------------------------------------------------------------
```

## Comprehensive Testing

### TestCrash.ps1 - Detailed Crash Testing

This script provides an interactive menu with various crash types.

**Usage:**

```powershell
# Interactive menu
.\TestCrash.ps1

# Specific crash type
.\TestCrash.ps1 -CrashType NullReference
.\TestCrash.ps1 -CrashType DivideByZero
.\TestCrash.ps1 -CrashType StackOverflow
.\TestCrash.ps1 -CrashType AccessViolation

# Run all crash tests
.\TestCrash.ps1 -CrashType All
```

**Available Crash Types:**

1. **Null Reference Exception** - Most common .NET crash
2. **Divide by Zero** - Arithmetic exception
3. **Stack Overflow** - Recursive function crash
4. **Access Violation** - Unsafe pointer access
5. **Index Out of Range** - Array bounds violation
6. **Invalid Cast Exception** - Type casting error
7. **Out of Memory** - Memory exhaustion
8. **Application Hang** - Infinite loop (triggers Event ID 1002)

**Expected Monitor Output Example:**

For Null Reference crash:
```
[2024-02-08 10:35:22] CRASH DETECTED
Source: Application Crash
Event ID: 1000

┌─ Process Name: CrashTest.exe
├─ Process ID (PID): 45678
├─ Exception Code: 0xc0000005
└─ Faulting Module: KERNELBASE.dll

Event Properties:
  Application Name: CrashTest.exe
  Exception Code: 0xc0000005
  Faulting Module Name: KERNELBASE.dll
--------------------------------------------------------------------------------
```

## Manual Testing

### Test Normal Process Termination

```powershell
# Start a process
Start-Process notepad.exe

# Wait a few seconds, then close it via Task Manager or:
Get-Process notepad | Stop-Process
```

### Test Application Crash

```powershell
# Compile and run the TestCrashApp.cs
csc TestCrashApp.cs
.\TestCrashApp.exe
# Select option 1 (Null Reference)
```

## Verification Checklist

After running tests, verify the following in the monitor output:

### For Crashes:
- ✓ Red "CRASH DETECTED" message appears
- ✓ Event ID is shown (1000, 1001, 1002, or 1026)
- ✓ Process Name is captured
- ✓ Process ID (PID) is shown
- ✓ Exception Code is displayed
- ✓ Entry appears in `crash_log.txt`

### For Terminations:
- ✓ Cyan "PROCESS TERMINATED" message appears
- ✓ Process Name matches (e.g., notepad.exe)
- ✓ Process ID (PID) is shown
- ✓ User name is displayed (DOMAIN\username)
- ✓ Entry appears in `termination_log.txt`

### Statistics:
- ✓ Final statistics show correct counts
- ✓ Processes are grouped correctly
- ✓ Both crash and termination counts are accurate

## Troubleshooting Tests

### No Events Appearing

**Problem**: Tests run but nothing appears in the monitor

**Solutions:**

1. **Check if monitor is running as Administrator**
   ```
   Should see: "Running with Administrator privileges ✓"
   ```

2. **Verify audit policy is enabled**
   ```powershell
   auditpol /get /category:"Detailed Tracking"
   ```
   Should show: `Process Termination    Success`

3. **Check Security Event Log access**
   - Open Event Viewer (eventvwr.msc)
   - Navigate to: Windows Logs → Security
   - Look for Event ID 4689 entries

4. **Ensure monitor started BEFORE running tests**
   - The monitor only captures events that occur AFTER it starts

### Crashes Not Detected

**Problem**: Terminations work but crashes don't appear

**Solutions:**

1. **Check Application Event Log**
   ```powershell
   # View recent application errors
   Get-EventLog -LogName Application -EntryType Error -Newest 5
   ```

2. **Verify crash actually occurred**
   - TestCrash.ps1 should show compilation and execution
   - Windows might show crash dialog box

3. **Try different crash types**
   - Some crashes may be caught by PowerShell
   - Use TestCrash.ps1 menu options 1-4

### Terminations Not Detected

**Problem**: Crashes work but terminations don't appear

**Solutions:**

1. **Verify audit policy**
   ```powershell
   # Check current audit settings
   auditpol /get /category:"Detailed Tracking"
   
   # Enable if not already
   auditpol /set /category:"Detailed Tracking" /subcategory:"Process Termination" /success:enable
   ```

2. **Restart the monitor**
   - After enabling audit policy, restart the monitor

3. **Check Security log**
   - Event Viewer → Security
   - Filter for Event ID 4689

## Performance Testing

### Stress Test

Test the monitor's ability to handle many events:

```powershell
# Run the stress test
.\QuickTest.ps1 -TestType Stress

# Or manually create many processes
1..20 | ForEach-Object { 
    Start-Process notepad.exe -WindowStyle Hidden
    Start-Sleep -Milliseconds 200
    Get-Process notepad | Stop-Process
}
```

**Expected Behavior:**
- Monitor should capture all terminations
- No crashes or performance degradation
- Statistics should be accurate

## Log File Verification

After testing, verify log files:

```powershell
# View crash log
Get-Content crash_log.txt -Tail 20

# View termination log
Get-Content termination_log.txt -Tail 20

# Count total entries
(Get-Content crash_log.txt | Select-String "CRASH DETECTED").Count
(Get-Content termination_log.txt | Select-String "PROCESS TERMINATED").Count
```

## Recommended Testing Sequence

For comprehensive testing, run tests in this order:

1. **Initial Setup**
   ```powershell
   # Start monitor as Administrator
   dotnet run
   ```

2. **Quick Sanity Check**
   ```powershell
   # In another PowerShell window
   .\QuickTest.ps1
   ```
   Verify both crash and termination appear

3. **Detailed Crash Testing**
   ```powershell
   .\TestCrash.ps1 -CrashType NullReference
   .\TestCrash.ps1 -CrashType DivideByZero
   ```

4. **Stress Testing**
   ```powershell
   .\QuickTest.ps1 -TestType Stress
   ```

5. **Verify Results**
   - Check console output
   - Review log files
   - Verify statistics

## Tips

- **Keep Event Viewer open** (eventvwr.msc) to see events in real-time
- **Use two monitors** or split screen to watch both test and monitor
- **Wait 2-3 seconds** between tests to see clear separation in logs
- **Check timestamps** to correlate test actions with detected events
- **Review both log files** after testing to ensure all events were captured

## Common Test Scenarios

### Scenario 1: Application Development
Test your own app crashing:
```powershell
# Run your app
.\YourApp.exe

# Monitor will detect if it crashes
```

### Scenario 2: Service Monitoring
Test service stop detection:
```powershell
# Stop a service (as Administrator)
Stop-Service -Name "SomeService"

# Monitor should show service termination
```

### Scenario 3: Task Kill
Test forced termination:
```powershell
Start-Process notepad.exe
taskkill /F /IM notepad.exe

# Monitor should show termination with exit status
```

## Success Criteria

Your monitor is working correctly if:
- ✓ All crash types from TestCrash.ps1 are detected
- ✓ Process terminations appear within 1-2 seconds
- ✓ Process names and PIDs are accurate
- ✓ Statistics match actual test counts
- ✓ Log files contain all events
- ✓ No errors or exceptions in monitor output
