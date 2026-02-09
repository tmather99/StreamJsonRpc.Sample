param(
    [string]$Choice
)

function Crash-NullReference {
    Write-Host "Triggering null reference exception..."
    # PowerShell/.NET null dereference
    $obj = $null
    # This will throw a runtime exception
    $len = $obj.Length
}

function Crash-DivideByZero {
    Write-Host "Triggering divide by zero..."
    $x = 10
    $y = 0
    # This will throw a runtime exception
    $z = $x / $y
    Write-Host $z
}

function Crash-StackOverflow {
    Write-Host "Triggering stack overflow..."
    function Invoke-RecursiveCrash {
        Invoke-RecursiveCrash
    }
    Invoke-RecursiveCrash
}

function Crash-AccessViolation {
    Write-Host "Triggering access violation (native)..."
    Add-Type -Namespace CrashTest -Name NativeMethods -Language CSharp -MemberDefinition @"
using System;
using System.Runtime.InteropServices;

public static class NativeMethods
{
    [DllImport("kernel32.dll")]
    public static extern void RtlZeroMemory(IntPtr dest, IntPtr size);

    public static void Crash()
    {
        // Intentionally call RtlZeroMemory with invalid pointer to cause access violation
        IntPtr badPtr = new IntPtr(1);
        IntPtr size = new IntPtr(1024);
        RtlZeroMemory(badPtr, size);
    }
}
"@

    [CrashTest.NativeMethods]::Crash();
}

function Crash-UnhandledTask {
    Write-Host "Triggering unhandled task exception..."
    Add-Type -Namespace CrashTest -Name TaskCrash -Language CSharp -MemberDefinition @"
using System;
using System.Threading.Tasks;

public static class TaskCrash
{
    public static void Run()
    {
        Task.Run(() =>
        {
            throw new Exception(""Unhandled exception in task!"");
        });
        // Give the task a moment to run and crash
        System.Threading.Thread.Sleep(1000);
    }
}
"@

    [CrashTest.TaskCrash]::Run();
}

Write-Host "Test Crash Application"
Write-Host "======================"
Write-Host "This script will crash to test the Process Crash Monitor"
Write-Host
Write-Host "Select crash type:"
Write-Host "1. Null Reference Exception"
Write-Host "2. Divide by Zero"
Write-Host "3. Stack Overflow"
Write-Host "4. Access Violation (native-style)"
Write-Host "5. Unhandled Exception in Task"

if (-not $Choice) {
    $Choice = Read-Host "`nEnter choice (1-5)"
}

Write-Host "`nCrashing in 3 seconds..."
Start-Sleep -Seconds 3

switch ($Choice) {
    "1" { return (Crash-NullReference) }
    "2" { return (Crash-DivideByZero) }
    "3" { return (Crash-StackOverflow) }
    "4" { return (Crash-AccessViolation) }
    "5" { return (Crash-UnhandledTask) }
    default {
        Write-Host "Invalid choice, crashing with null reference..."
        return (Crash-NullReference)
    }
}
