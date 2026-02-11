param(
    [int]$Iterations = 100,
    [int]$DelayMs    = 50
)

$ErrorActionPreference = 'Stop'

# HKCU and HKLM roots
$hkcuRoot = 'HKCU:\Software\MyApp1'
$hklmRoot = 'HKLM:\SOFTWARE\WorkspaceONE\Satori'

Write-Host "Starting registry stress test..."
Write-Host "HKCU root: $hkcuRoot"
Write-Host "HKLM root: $hklmRoot"
Write-Host "Iterations: $Iterations, Delay: $DelayMs ms"
Write-Host ""

for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host "Iteration $i of $Iterations"

    # ----- HKCU: key, subkey, values -----
    $hkcuIterKey     = Join-Path $hkcuRoot "Iter_$i"
    $hkcuIterSubKey1 = Join-Path $hkcuIterKey "SubKey1"
    $hkcuIterSubKey2 = Join-Path $hkcuIterKey "SubKey2"

    # Ensure base key exists
    if (-not (Test-Path $hkcuRoot)) {
        New-Item -Path $hkcuRoot -Force | Out-Null
    }

    # Create iteration key and subkeys
    New-Item -Path $hkcuIterKey     -Force | Out-Null
    New-Item -Path $hkcuIterSubKey1 -Force | Out-Null
    New-Item -Path $hkcuIterSubKey2 -Force | Out-Null

    # Create / update values on root
    New-ItemProperty -Path $hkcuRoot -Name "TestValue_$i" -Value "Root_$i" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $hkcuRoot -Name "Counter"      -Value $i       -PropertyType DWord  -Force | Out-Null

    # Create / update values on subkeys
    New-ItemProperty -Path $hkcuIterSubKey1 -Name "Val1" -Value "Sub1_$i" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $hkcuIterSubKey2 -Name "Val2" -Value "Sub2_$i" -PropertyType String -Force | Out-Null

    # Delete one value and one subkey to generate delete events
    if (Test-Path -Path $hkcuIterSubKey1) {
        Remove-ItemProperty -Path $hkcuIterSubKey1 -Name "Val1" -ErrorAction SilentlyContinue
    }
    if (Test-Path -Path $hkcuIterSubKey2) {
        Remove-Item -Path $hkcuIterSubKey2 -Recurse -Force -ErrorAction SilentlyContinue
    }

    # ----- HKLM: key, subkey, values -----
    $hklmIterKey     = Join-Path $hklmRoot "Iter_$i"
    $hklmIterSubKey1 = Join-Path $hklmIterKey "SubKey1"
    $hklmIterSubKey2 = Join-Path $hklmIterKey "SubKey2"

    if (-not (Test-Path $hklmRoot)) {
        New-Item -Path $hklmRoot -Force | Out-Null
    }

    New-Item -Path $hklmIterKey     -Force | Out-Null
    New-Item -Path $hklmIterSubKey1 -Force | Out-Null
    New-Item -Path $hklmIterSubKey2 -Force | Out-Null

    New-ItemProperty -Path $hklmRoot -Name "TestValue_$i" -Value "Root_$i" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $hklmRoot -Name "Counter"      -Value $i       -PropertyType DWord  -Force | Out-Null

    New-ItemProperty -Path $hklmIterSubKey1 -Name "Val1" -Value "Sub1_$i" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $hklmIterSubKey2 -Name "Val2" -Value "Sub2_$i" -PropertyType String -Force | Out-Null

    if (Test-Path -Path $hklmIterSubKey1) {
        Remove-ItemProperty -Path $hklmIterSubKey1 -Name "Val1" -ErrorAction SilentlyContinue
    }
    if (Test-Path -Path $hklmIterSubKey2) {
        Remove-Item -Path $hklmIterSubKey2 -Recurse -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Milliseconds $DelayMs
}

Write-Host ""
Write-Host "Stress test completed."