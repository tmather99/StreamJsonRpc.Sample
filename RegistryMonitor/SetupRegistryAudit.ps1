<#
.SYNOPSIS
  Enable registry auditing (SACL) for specified key(s)
.DESCRIPTION
  Sets auditing to track value changes, creation, deletion, and permission changes.
  Logs will appear in Security Event Log (Event ID 4657, 4660, 4663, 4670).
.PARAMETER KeyPaths
  Registry key paths to audit (string array). Example: "HKCU:\Software\MyApp"
.EXAMPLE
  .\SetupRegistryAudit.ps1 -KeyPaths "HKCU:\Software\MyApp","HKLM:\SOFTWARE\MyOtherApp"
#>

param(
    [Parameter(Mandatory = $true)]
    [string[]] $KeyPaths
)

# Function to convert to RegistrySecurity
function Enable-Audit {
    param(
        [string] $KeyPath
    )

    Write-Host "Processing: $KeyPath"

    # Open the registry key
    $regKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($KeyPath.Substring(6), 
              [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree, 
              [System.Security.AccessControl.RegistryRights]::ChangePermissions)

    if (-not $regKey) {
        Write-Warning "Cannot open key: $KeyPath"
        return
    }

    $regSec = $regKey.GetAccessControl([System.Security.AccessControl.AccessControlSections]::Audit)

    # Define the audit rule
    $rule = New-Object System.Security.AccessControl.RegistryAuditRule (
        "Everyone",                              # User/Group to audit
        "SetValue, CreateSubKey, Delete, ChangePermissions", # Rights to audit
        "Success, Failure"
    )

    # Add the rule
    $regSec.AddAuditRule($rule)

    # Apply changes
    $regKey.SetAccessControl($regSec)
    $regKey.Close()

    Write-Host "SACL auditing enabled for $KeyPath"
}

foreach ($path in $KeyPaths) {
    Enable-Audit -KeyPath $path
}

Write-Host "✅ Auditing setup complete. Run RegistryListener with Administrator privileges."
