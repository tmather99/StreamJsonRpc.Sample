<#
.SYNOPSIS
  Enable system audit policy and registry auditing (SACL) for specified key(s)

.DESCRIPTION
  1. Enables Windows Advanced Audit Policy for registry changes.
  2. Sets SACL on the specified registry keys so changes generate Security log events.
  3. Optionally ensures the monitoring identity has read permissions on those keys.

  Logs will typically appear in the Security Event Log (e.g., 4657, 4663).

.PARAMETER KeyPaths
  Registry key paths to audit (string array). Example: "HKCU:\Software\MyApp"

.PARAMETER MonitorIdentity
  Account that runs RegistryMonitor and must be able to access the keys.
  Example: "NT AUTHORITY\SYSTEM" or "DOMAIN\User". Default is current user.

.EXAMPLE
  .\SetupRegistryAudit.ps1 -KeyPaths "HKCU:\Software\MyApp","HKLM:\SOFTWARE\WorkspaceONE\Satori"

.EXAMPLE
  .\SetupRegistryAudit.ps1 -KeyPaths "HKLM:\SOFTWARE\MyOtherApp" -MonitorIdentity "NT AUTHORITY\SYSTEM"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]] $KeyPaths,

    [string] $MonitorIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
)

function Enable-RegistryAuditPolicy {
    <#
        Enables the OS audit policy so that registry SACLs actually produce 46xx events.

        Uses `auditpol` to enable:
          - Object Access -> Registry
    #>
    Write-Host "Enabling Windows audit policy for registry object access..." -ForegroundColor Cyan

    # Note: requires elevated/Admin PowerShell.
    $auditpol = Join-Path $env:SystemRoot "System32\auditpol.exe"

    if (-not (Test-Path $auditpol)) {
        Write-Warning "auditpol.exe not found; cannot configure audit policy. Ensure 'Audit Object Access' / 'Registry' is enabled manually."
        return
    }

    # Enable success/failure auditing for subcategory "Registry"
    $arguments = '/set /subcategory:"Registry" /success:enable /failure:enable'

    $proc = Start-Process -FilePath $auditpol -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) {
        Write-Warning "Failed to configure audit policy (exit code $($proc.ExitCode)). You may need to run as Administrator or configure policy via Local/Group Policy."
    }
    else {
        Write-Host "Windows audit policy for 'Registry' is enabled." -ForegroundColor Green
    }
}

function Get-RegistryKeyFromPath {
    param(
        [Parameter(Mandatory)]
        [string] $KeyPath
    )

    if ($KeyPath -like 'HKCU:\*') {
        $hive = [Microsoft.Win32.Registry]::CurrentUser
        $subKeyPath = $KeyPath.Substring(6)  # strip 'HKCU:\'
    }
    elseif ($KeyPath -like 'HKLM:\*') {
        $hive = [Microsoft.Win32.Registry]::LocalMachine
        $subKeyPath = $KeyPath.Substring(6)  # strip 'HKLM:\'
    }
    else {
        throw "Unsupported registry path format: $KeyPath (expected HKCU:\ or HKLM:\)."
    }

    $hive.OpenSubKey(
        $subKeyPath,
        [Microsoft.Win32.RegistryKeyPermissionCheck]::ReadWriteSubTree,
        # Need both ChangePermissions (for SACL/DACL) and ReadPermissions
        [System.Security.AccessControl.RegistryRights]::ChangePermissions -bor
        [System.Security.AccessControl.RegistryRights]::ReadPermissions
    )
}

function Enable-RegistryKeySacl {
    param(
        [Parameter(Mandatory)]
        [string] $KeyPath
    )

    Write-Host "Configuring SACL for: $KeyPath"

    $regKey = Get-RegistryKeyFromPath -KeyPath $KeyPath
    if (-not $regKey) {
        Write-Warning "Cannot open key: $KeyPath (it may not exist or permissions are insufficient)."
        return
    }

    # Get current SACL section
    $regSec = $regKey.GetAccessControl(
        [System.Security.AccessControl.AccessControlSections]::Audit
    )

    # Identity to audit: Everyone (you can narrow this if needed)
    $identity = New-Object System.Security.Principal.NTAccount("Everyone")

    # Rights to audit
    $rights =
        [System.Security.AccessControl.RegistryRights]::QueryValues    -bor
        [System.Security.AccessControl.RegistryRights]::SetValue       -bor
        [System.Security.AccessControl.RegistryRights]::CreateSubKey   -bor
        [System.Security.AccessControl.RegistryRights]::Delete         -bor
        [System.Security.AccessControl.RegistryRights]::ChangePermissions -bor
        [System.Security.AccessControl.RegistryRights]::TakeOwnership

    # ContainerInherit so subkeys inherit auditing as well
    $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]::None
    $auditFlags       = [System.Security.AccessControl.AuditFlags]::Success -bor
                        [System.Security.AccessControl.AuditFlags]::Failure

    $rule = New-Object System.Security.AccessControl.RegistryAuditRule(
        $identity,
        $rights,
        $inheritanceFlags,
        $propagationFlags,
        $auditFlags
    )

    $regSec.AddAuditRule($rule)
    $regKey.SetAccessControl($regSec)
    $regKey.Close()

    Write-Host "SACL auditing enabled for $KeyPath" -ForegroundColor Green
}

function Ensure-MonitorAclAccess {
    param(
        [Parameter(Mandatory)]
        [string] $KeyPath,

        [Parameter(Mandatory)]
        [string] $MonitorIdentity
    )

    Write-Host "Ensuring ACL read access for '$MonitorIdentity' on: $KeyPath"

    $regKey = Get-RegistryKeyFromPath -KeyPath $KeyPath
    if (-not $regKey) {
        Write-Warning "Cannot open key: $KeyPath (it may not exist or permissions are insufficient)."
        return
    }

    $regSec = $regKey.GetAccessControl(
        [System.Security.AccessControl.AccessControlSections]::Access
    )

    $ntAccount = New-Object System.Security.Principal.NTAccount($MonitorIdentity)

    # Rights required for typical monitoring (read)
    $rights =
        [System.Security.AccessControl.RegistryRights]::ReadKey

    $inheritanceFlags = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit
    $propagationFlags = [System.Security.AccessControl.PropagationFlags]::None
    $accessControlType = [System.Security.AccessControl.AccessControlType]::Allow

    $rule = New-Object System.Security.AccessControl.RegistryAccessRule(
        $ntAccount,
        $rights,
        $inheritanceFlags,
        $propagationFlags,
        $accessControlType
    )

    $regSec.AddAccessRule($rule)
    $regKey.SetAccessControl($regSec)
    $regKey.Close()

    Write-Host "ACL updated for '$MonitorIdentity' on $KeyPath" -ForegroundColor Green
}

# --- Main ---

Write-Host "Configuring system audit policy and registry auditing..." -ForegroundColor Cyan

Enable-RegistryAuditPolicy

foreach ($path in $KeyPaths) {
    Enable-RegistryKeySacl -KeyPath $path
    Ensure-MonitorAclAccess -KeyPath $path -MonitorIdentity $MonitorIdentity
}

Write-Host "Audit policy and registry auditing setup complete." -ForegroundColor Cyan
Write-Host "Verify Security log events (e.g., 4657/4663) when changing values under the configured keys." -ForegroundColor Yellow
