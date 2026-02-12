# Normal success
cmd /c exit 0

# Simple failure
cmd /c exit 1

# Start and force kill
notepad & timeout /t 2 & taskkill /F /IM notepad.exe

# Multiple processes
1..10 | % { Start-Process cmd -ArgumentList "/c exit $_" -WindowStyle Hidden }

# PowerShell with exit code
pwsh -NoProfile -Command "exit 123"

# Start process, wait, then kill
$p = Start-Process calc -PassThru; Start-Sleep 3; Stop-Process $p.Id

# Test CTRL_C (manual)
ping -t localhost
# Press Ctrl+C when ready