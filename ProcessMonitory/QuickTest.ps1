# Quick Test Script for Process Crash & Termination Monitor
# Tests both crashes and normal process exits

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Crash", "Exit", "Both", "Stress")]
    [string]$TestType = "Both"
)

Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "   Quick Test for Process Crash & Termination Monitor" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

function Test-Crash {
    Write-Host "[TEST] Generating a crash..." -ForegroundColor Yellow
    
    Add-Type -TypeDefinition @"
using System;
public class CrashGenerator 
{
    public static void Crash() 
    {
        string text = null;
        Console.WriteLine(text.Length);
    }
}
"@
    
    try {
        [CrashGenerator]::Crash()
    }
    catch {
        Write-Host "✓ Crash generated (caught in PowerShell, but process crashed in C#)" -ForegroundColor Green
    }
}

function Test-NormalExit {
    param([string]$ProcessName)
    
    Write-Host "[TEST] Starting and stopping $ProcessName..." -ForegroundColor Cyan
    
    $process = Start-Process -FilePath $ProcessName -PassThru -WindowStyle Hidden
    Write-Host "  Started PID: $($process.Id)" -ForegroundColor Gray
    Start-Sleep -Seconds 2
    $process.Kill()
    Write-Host "  ✓ Process terminated" -ForegroundColor Green
}

function Test-StressTest {
    Write-Host "[STRESS TEST] Creating and terminating multiple processes..." -ForegroundColor Magenta
    
    for ($i = 1; $i -le 10; $i++) {
        Write-Host "  [$i/10] " -NoNewline -ForegroundColor Gray
        
        $process = Start-Process -FilePath "notepad.exe" -PassThru -WindowStyle Hidden
        Start-Sleep -Milliseconds 500
        $process.Kill()
        
        Write-Host "PID $($process.Id) terminated" -ForegroundColor DarkGray
        Start-Sleep -Milliseconds 200
    }
    
    Write-Host "  ✓ Stress test completed" -ForegroundColor Green
}

# Execute tests based on parameter
switch ($TestType) {
    "Crash" {
        Test-Crash
    }
    "Exit" {
        Test-NormalExit -ProcessName "notepad.exe"
    }
    "Both" {
        Write-Host "Test 1: Normal Process Exit" -ForegroundColor White
        Write-Host "────────────────────────────" -ForegroundColor DarkGray
        Test-NormalExit -ProcessName "notepad.exe"
        
        Write-Host ""
        Write-Host "Waiting 3 seconds..." -ForegroundColor Gray
        Start-Sleep -Seconds 3
        
        Write-Host ""
        Write-Host "Test 2: Process Crash" -ForegroundColor White
        Write-Host "─────────────────────" -ForegroundColor DarkGray
        Test-Crash
    }
    "Stress" {
        Test-StressTest
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Check your Process Monitor for the logged events!" -ForegroundColor Yellow
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
