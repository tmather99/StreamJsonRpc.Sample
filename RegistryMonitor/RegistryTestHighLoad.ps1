# ⚠ WARNING: Run as Administrator
# High-load registry test script for RegistryListener
# Simulates multiple parallel processes modifying multiple registry keys

# Base keys for testing
$baseKeys = @(
    "HKCU:\Software\TestLoad1",
    "HKCU:\Software\TestLoad2",
    "HKCU:\Software\TestLoad3",
    "HKCU:\Software\TestLoad4",
    "HKCU:\Software\TestLoad5"
)

$valueName = "Counter"

# Number of iterations per task
$iterations = 100

# Number of parallel tasks per key
$tasksPerKey = 3

# Helper functions
function Create-Key($key) {
    if (-not (Test-Path $key)) {
        New-Item -Path $key -Force | Out-Null
        Write-Host "Created key: $key"
    }
}

function Delete-Key($key) {
    if (Test-Path $key) {
        Remove-Item -Path $key -Recurse -Force
        Write-Host "Deleted key: $key"
    }
}

function Update-Value($key, $name, $value) {
    Set-ItemProperty -Path $key -Name $name -Value $value
}

function Change-Permissions($key) {
    $acl = Get-Acl -Path $key
    $rule = New-Object System.Security.AccessControl.RegistryAccessRule("Everyone","FullControl","Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -Path $key -AclObject $acl
}

# Function to run stress test for a single key
function Stress-Key($key, $taskId) {
    Create-Key $key

    for ($i = 1; $i -le $iterations; $i++) {
        $rand = Get-Random -Minimum 1 -Maximum 10000

        Update-Value $key $valueName $rand
        Change-Permissions $key

        # Randomly delete the key (10% chance)
        if ((Get-Random) -lt 0.1) {
            Delete-Key $key
            Start-Sleep -Milliseconds 100
            Create-Key $key
        }

        # Small random delay
        Start-Sleep -Milliseconds (Get-Random -Minimum 50 -Maximum 200)
    }

    Write-Host "Task $taskId for key $key completed."
}

# Main parallel execution
$jobs = @()
$taskId = 0

foreach ($key in $baseKeys) {
    for ($t = 1; $t -le $tasksPerKey; $t++) {
        $taskId++
        $jobs += [System.Threading.Tasks.Task]::Run({ Stress-Key $using:key $using:taskId })
    }
}

# Wait for all tasks to complete
[System.Threading.Tasks.Task]::WaitAll($jobs.ToArray())

Write-Host "High-load registry stress test completed."
