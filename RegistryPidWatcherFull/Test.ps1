# -------------------------------
# Registry Listener Test Script
# -------------------------------
$RegPath = "HKLM:\SOFTWARE\WorkspaceONE\Satori"
$Values = @("TestValue1", "TestValue2", "TestValue3")

Write-Host "Target path: $RegPath"

# Ensure the key exists
if (-not (Test-Path $RegPath)) {
    Write-Host "Creating registry key $RegPath..."
    New-Item -Path $RegPath | Out-Null
}

# Function to set a random value
function Set-RandomRegistryValue {
    param(
        [string]$Key,
        [string[]]$ValueNames
    ) 

    foreach ($name in $ValueNames) {
        $random = Get-Random -Maximum 1000
        Set-ItemProperty -Path $Key -Name $name -Value $random
        Write-Host "Set $name = $random"
        Start-Sleep -Milliseconds 500
    }
}

Write-Host "Starting test modifications..."
Set-RandomRegistryValue -Key $RegPath -ValueNames $Values
Write-Host "Test complete! Check your RegistryListener console for events."

