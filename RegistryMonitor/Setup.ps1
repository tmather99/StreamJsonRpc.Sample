# ==============================
# CONFIG — CHANGE THIS KEY
# ==============================
$RegistryPath = "HKCU:\Software\MyApp"

Write-Host "`n=== Enabling Registry Auditing ===" -ForegroundColor Cyan

# --------------------------------
# 1️⃣ Enable system audit policy
# --------------------------------
Write-Host "Enabling 'Audit Registry' policy..."

auditpol /set /subcategory:"Registry" /success:enable /failure:enable | Out-Null

# Also ensure Object Access auditing is on
auditpol /set /category:"Object Access" /success:enable /failure:enable | Out-Null

Write-Host "Audit policy enabled.`n"

# --------------------------------
# 2️⃣ Add SACL to registry key
# --------------------------------
Write-Host "Setting SACL on $RegistryPath ..."

# Get current ACL
$acl = Get-Acl $RegistryPath

# Create auditing rule (Everyone → SetValue)
$identity = New-Object System.Security.Principal.NTAccount("Everyone")

$rule = New-Object System.Security.AccessControl.RegistryAuditRule(
    $identity,
    [System.Security.AccessControl.RegistryRights]::SetValue,
    [System.Security.AccessControl.InheritanceFlags]::None,
    [System.Security.AccessControl.PropagationFlags]::None,
    [System.Security.AccessControl.AuditFlags]::Success
)

# Add rule
$acl.AddAuditRule($rule)

# Apply
Set-Acl -Path $RegistryPath -AclObject $acl

Write-Host "SACL applied.`n" -ForegroundColor Green

# --------------------------------
# 3️⃣ Confirm settings
# --------------------------------
Write-Host "Current audit policy:"
auditpol /get /subcategory:"Registry"

Write-Host "`nSACL entries on key:"
(Get-Acl $RegistryPath).Audit

Write-Host "`nSetup complete!"
Write-Host "Now registry value changes under this key will generate Security Event 4657."
