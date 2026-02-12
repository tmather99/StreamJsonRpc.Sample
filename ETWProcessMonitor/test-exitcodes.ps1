# test-exitcodes.ps1
# Run various processes to test exit code monitoring

Write-Host "=== ETW Process Exit Monitor Test Suite ===" -ForegroundColor Cyan
Write-Host "Make sure EtwProcessMonitor.exe is running in another window!" -ForegroundColor Yellow
Write-Host ""

# Test 1: Normal exit (0)
Write-Host "[Test 1] Normal exit (ExitCode=0)..." -ForegroundColor Green
cmd /c "echo Test 1: Normal exit"
Start-Sleep -Seconds 1

# Test 2: Exit with code 1
Write-Host "[Test 2] Exit with code 1..." -ForegroundColor Green
cmd /c "exit /b 1"
Start-Sleep -Seconds 1

# Test 3: Exit with code 42
Write-Host "[Test 3] Exit with custom code 42..." -ForegroundColor Green
powershell -Command "exit 42"
Start-Sleep -Seconds 1

# Test 4: Start and kill process (CTRL_C_EXIT)
Write-Host "[Test 4] Start process and kill it (CTRL_C_EXIT)..." -ForegroundColor Green
$proc = Start-Process notepad -PassThru
Start-Sleep -Seconds 2
Stop-Process -Id $proc.Id -Force
Start-Sleep -Seconds 1

# Test 5: Multiple rapid exits
Write-Host "[Test 5] Multiple rapid process exits..." -ForegroundColor Green
1..5 | ForEach-Object {
    Start-Process cmd -ArgumentList "/c", "exit 0" -WindowStyle Hidden
}
Start-Sleep -Seconds 2

# Test 6: PowerShell exit codes
Write-Host "[Test 6] PowerShell with different exit codes..." -ForegroundColor Green
pwsh -Command "exit 0"
pwsh -Command "exit 1"
pwsh -Command "exit 255"
Start-Sleep -Seconds 1

# Test 7: Failed command (error code)
Write-Host "[Test 7] Failed command..." -ForegroundColor Green
cmd /c "dir C:\NonExistentFolder\xyz.txt 2>nul"
Start-Sleep -Seconds 1

# Test 8: Cancelled operation
Write-Host "[Test 8] Launching process that will auto-close..." -ForegroundColor Green
$proc = Start-Process powershell -ArgumentList "-Command", "Start-Sleep 3; exit 1223" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 4

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host "Check the EtwProcessMonitor output for all exit events" -ForegroundColor Yellow