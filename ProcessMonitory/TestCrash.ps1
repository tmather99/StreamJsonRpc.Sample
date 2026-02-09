# Test Crash Generator for Process Crash Monitor
# This script generates various types of crashes to test the monitoring application

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("NullReference", "DivideByZero", "StackOverflow", "AccessViolation", "IndexOutOfRange", "InvalidCast", "OutOfMemory", "All")]
    [string]$CrashType = "Menu"
)

function Show-Menu {
    Clear-Host
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "        Test Crash Generator for C# Monitor" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Select crash type to generate:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1. Null Reference Exception" -ForegroundColor White
    Write-Host "  2. Divide by Zero" -ForegroundColor White
    Write-Host "  3. Stack Overflow" -ForegroundColor White
    Write-Host "  4. Access Violation (unsafe code)" -ForegroundColor White
    Write-Host "  5. Index Out of Range" -ForegroundColor White
    Write-Host "  6. Invalid Cast Exception" -ForegroundColor White
    Write-Host "  7. Out of Memory" -ForegroundColor White
    Write-Host "  8. Application Hang (infinite loop)" -ForegroundColor White
    Write-Host "  9. Generate Multiple Crashes" -ForegroundColor White
    Write-Host "  0. Exit" -ForegroundColor White
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Invoke-CrashTest {
    param([string]$Type)
    
    Write-Host "`nGenerating crash: $Type" -ForegroundColor Yellow
    Write-Host "Crash will occur in 3 seconds..." -ForegroundColor Red
    Start-Sleep -Seconds 3
    
    # Create temporary C# crash application
    $crashCode = @"
using System;

class CrashTest
{
    static void Main()
    {
        Console.WriteLine("Starting crash test: $Type");
        System.Threading.Thread.Sleep(500);
        
        switch("$Type")
        {
            case "NullReference":
                CrashNullReference();
                break;
            case "DivideByZero":
                CrashDivideByZero();
                break;
            case "StackOverflow":
                CrashStackOverflow();
                break;
            case "AccessViolation":
                CrashAccessViolation();
                break;
            case "IndexOutOfRange":
                CrashIndexOutOfRange();
                break;
            case "InvalidCast":
                CrashInvalidCast();
                break;
            case "OutOfMemory":
                CrashOutOfMemory();
                break;
            case "Hang":
                CrashHang();
                break;
        }
    }
    
    static void CrashNullReference()
    {
        Console.WriteLine("Triggering Null Reference Exception...");
        string text = null;
        Console.WriteLine(text.Length);
    }
    
    static void CrashDivideByZero()
    {
        Console.WriteLine("Triggering Divide by Zero...");
        int x = 10;
        int y = 0;
        Console.WriteLine(x / y);
    }
    
    static void CrashStackOverflow()
    {
        Console.WriteLine("Triggering Stack Overflow...");
        CrashStackOverflow();
    }
    
    static void CrashAccessViolation()
    {
        Console.WriteLine("Triggering Access Violation...");
        unsafe
        {
            int* ptr = (int*)0;
            *ptr = 42;
        }
    }
    
    static void CrashIndexOutOfRange()
    {
        Console.WriteLine("Triggering Index Out of Range...");
        int[] arr = new int[5];
        Console.WriteLine(arr[10]);
    }
    
    static void CrashInvalidCast()
    {
        Console.WriteLine("Triggering Invalid Cast...");
        object obj = "Hello";
        int number = (int)obj;
    }
    
    static void CrashOutOfMemory()
    {
        Console.WriteLine("Triggering Out of Memory...");
        var list = new System.Collections.Generic.List<byte[]>();
        while(true)
        {
            list.Add(new byte[100000000]); // 100MB chunks
        }
    }
    
    static void CrashHang()
    {
        Console.WriteLine("Triggering Application Hang...");
        while(true)
        {
            // Infinite loop - will trigger hang detection
            System.Threading.Thread.Sleep(100);
        }
    }
}
"@

    # Save to temp file
    $tempFile = [System.IO.Path]::GetTempFileName() + ".cs"
    $exeFile = [System.IO.Path]::ChangeExtension($tempFile, ".exe")
    
    try {
        # Write C# code
        $crashCode | Out-File -FilePath $tempFile -Encoding UTF8
        
        # Compile
        Write-Host "Compiling crash test application..." -ForegroundColor Cyan
        
        $compileParams = @(
            "/unsafe"
            "/out:$exeFile"
            $tempFile
        )
        
        $cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
        if (-not (Test-Path $cscPath)) {
            $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
        }
        
        $result = & $cscPath $compileParams 2>&1
        
        if (Test-Path $exeFile) {
            Write-Host "✓ Compiled successfully" -ForegroundColor Green
            Write-Host "Executing crash test application..." -ForegroundColor Yellow
            Write-Host ""
            
            # Execute the crash application
            & $exeFile
            
            Write-Host ""
            Write-Host "Process exited (crash should have been logged)" -ForegroundColor Magenta
        }
        else {
            Write-Host "✗ Compilation failed:" -ForegroundColor Red
            Write-Host $result
        }
    }
    catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }
    finally {
        # Cleanup
        Start-Sleep -Seconds 1
        if (Test-Path $tempFile) { Remove-Item $tempFile -Force -ErrorAction SilentlyContinue }
        if (Test-Path $exeFile) { Remove-Item $exeFile -Force -ErrorAction SilentlyContinue }
    }
}

function Invoke-MultipleCrashes {
    Write-Host "`n╔═══════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  Generating Multiple Crashes (5 second delay) ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    $crashTypes = @("NullReference", "DivideByZero", "IndexOutOfRange", "InvalidCast")
    
    foreach ($crash in $crashTypes) {
        Write-Host "`n[$($crashTypes.IndexOf($crash) + 1)/$($crashTypes.Count)]" -ForegroundColor Yellow
        Invoke-CrashTest -Type $crash
        if ($crash -ne $crashTypes[-1]) {
            Write-Host "`nWaiting 5 seconds before next crash..." -ForegroundColor Gray
            Start-Sleep -Seconds 5
        }
    }
    
    Write-Host "`n✓ All crash tests completed!" -ForegroundColor Green
}

# Main execution
if ($CrashType -eq "Menu") {
    do {
        Show-Menu
        $choice = Read-Host "Enter your choice (0-9)"
        
        switch ($choice) {
            "1" { Invoke-CrashTest -Type "NullReference"; Read-Host "`nPress Enter to continue" }
            "2" { Invoke-CrashTest -Type "DivideByZero"; Read-Host "`nPress Enter to continue" }
            "3" { Invoke-CrashTest -Type "StackOverflow"; Read-Host "`nPress Enter to continue" }
            "4" { Invoke-CrashTest -Type "AccessViolation"; Read-Host "`nPress Enter to continue" }
            "5" { Invoke-CrashTest -Type "IndexOutOfRange"; Read-Host "`nPress Enter to continue" }
            "6" { Invoke-CrashTest -Type "InvalidCast"; Read-Host "`nPress Enter to continue" }
            "7" { Invoke-CrashTest -Type "OutOfMemory"; Read-Host "`nPress Enter to continue" }
            "8" { Invoke-CrashTest -Type "Hang"; Read-Host "`nPress Enter to continue" }
            "9" { Invoke-MultipleCrashes; Read-Host "`nPress Enter to continue" }
            "0" { 
                Write-Host "`nExiting..." -ForegroundColor Cyan
                exit 
            }
            default { 
                Write-Host "`nInvalid choice. Press Enter to continue..." -ForegroundColor Red
                Read-Host
            }
        }
    } while ($true)
}
elseif ($CrashType -eq "All") {
    Invoke-MultipleCrashes
}
else {
    Invoke-CrashTest -Type $CrashType
}
