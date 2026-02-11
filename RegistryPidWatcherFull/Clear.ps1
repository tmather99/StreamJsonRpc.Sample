param(
    [string]$RegistryPath = "HKLM:\SOFTWARE\WorkspaceONE\Satori"
)

Write-Host "Stopping registry auditing for $RegistryPath"
Write-Host "Run as Administrator!" -ForegroundColor Yellow

# ----------------------------
# 1️⃣ Disable system audit policy
# ----------------------------
Write-Host "Disabling system registry audit policy..."
auditpol /set /subcategory:"Registry" /success:disable /failure:disable | Out-Null

# ----------------------------
# 2️⃣ Get ACL
# ----------------------------
$acl = Get-Acl -Path $RegistryPath

# ----------------------------
# 3️⃣ Remove all SACL rules
# ----------------------------
$auditRules = $acl.GetAuditRules($true, $true, [System.Security.Principal.NTAccount])
foreach ($rule in $auditRules) {
    Write-Host "Removing audit rule: $($rule.IdentityReference)"
    $acl.RemoveAuditRule($rule) | Out-Null
}

# ----------------------------
# 4️⃣ Disable SACL inheritance
# ----------------------------
Write-Host "Disabling audit inheritance..."
$acl.SetAuditRuleProtection($true, $false)

# ----------------------------
# 5️⃣ Apply
# ----------------------------
Set-Acl -Path $RegistryPath -AclObject $acl

Write-Host "✅ Registry auditing FULLY removed."
