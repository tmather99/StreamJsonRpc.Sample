using System.ComponentModel;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace RegistryPidWatcher;

/// <summary>
/// C# equivalent of SetupRegistryAudit.ps1.
/// Enables audit policy for registry and configures SACL/ACL on specified keys
/// so create/delete/write operations on subkeys generate Security log events.
/// </summary>
public static class RegistryAuditConfigurator
{
    /// <summary>
    /// Configure Windows audit policy and registry SACL/ACL for given key paths.
    /// Key paths must be PowerShell-style, e.g. "HKCU:\\Software\\MyApp" or
    /// "HKLM:\\SOFTWARE\\MyOtherApp".
    /// </summary>
    public static void ConfigureRegistryAudit(IEnumerable<string> keyPaths, string? monitorIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(keyPaths);

        if (string.IsNullOrWhiteSpace(monitorIdentity))
        {
            monitorIdentity = WindowsIdentity.GetCurrent().Name;
        }

        EnableRegistryAuditPolicy();

        foreach (var path in keyPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                EnableRegistryKeySacl(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to configure SACL for '{path}': {ex}");
            }

            try
            {
                EnsureMonitorAclAccess(path, monitorIdentity);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to configure ACL for '{monitorIdentity}' on '{path}': {ex}");
            }
        }
    }

    // This method enables the global audit policy for registry access events using the auditpol.exe tool.
    private static void EnableRegistryAuditPolicy()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var auditPolPath = Path.Combine(systemRoot, "System32", "auditpol.exe");

        if (!File.Exists(auditPolPath))
        {
            Debug.WriteLine("auditpol.exe not found; cannot configure audit policy. " +
                            "Ensure 'Audit Object Access' / 'Registry' is enabled manually.");
            return;
        }

        var startInfo = new ProcessStartInfo {
            FileName = auditPolPath,
            Arguments = "/set /subcategory:\"Registry\" /success:enable /failure:enable",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start auditpol.exe.");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            Debug.WriteLine($"Failed to configure audit policy (exit code {process.ExitCode})." +
                            $" You may need to run as Administrator or configure policy via Local/Group Policy.");
        }
    }

    /// <summary>
    /// Get an open RegistryKey for the provided PS-style path (HKCU:\ or HKLM:\).
    /// Optionally creates the key and opens it with rights suitable for SACL/ACL changes.
    /// Mirrors Get-RegistryKeyFromPath in SetupRegistryAudit.ps1 but also supports key creation.
    /// </summary>
    private static RegistryKey GetRegistryKeyFromPath(string keyPath, bool createIfMissing = false)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
        {
            throw new ArgumentException("Key path is null or empty.", nameof(keyPath));
        }

        RegistryKey hive;
        string subKeyPath;

        if (keyPath.StartsWith("HKCU:\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.CurrentUser;
            subKeyPath = keyPath.Substring("HKCU:\\".Length);
        }
        else if (keyPath.StartsWith("HKLM:\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.LocalMachine;
            subKeyPath = keyPath.Substring("HKLM:\\".Length);
        }
        else
        {
            throw new NotSupportedException($"Unsupported registry path format: {keyPath} (expected HKCU:\\ or HKLM:\\).");
        }

        const RegistryRights rights = RegistryRights.ReadKey |
                                      RegistryRights.WriteKey |
                                      RegistryRights.ReadPermissions |
                                      RegistryRights.ChangePermissions |
                                      RegistryRights.TakeOwnership;

        var key = hive.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, rights);

        if (key == null && createIfMissing)
        {
            using var created = hive.CreateSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
            if (created == null)
            {
                throw new Win32Exception($"Cannot create registry key '{keyPath}'.");
            }

            key = hive.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, rights);
        }

        if (key == null)
        {
            throw new Win32Exception($"Cannot open registry key '{keyPath}' with required permissions.");
        }

        return key;
    }

    // This method enables SACL auditing on the specified registry key for the "Everyone" group,
    // allowing both success and failure audits for various registry operations.
    private static void EnableRegistryKeySacl(string keyPath)
    {
        using var regKey = GetRegistryKeyFromPath(keyPath, createIfMissing: true);

        var regSec = regKey.GetAccessControl(AccessControlSections.Audit);

        var identity = new NTAccount("Everyone");

        const RegistryRights rights = RegistryRights.QueryValues |
                                      RegistryRights.SetValue |
                                      RegistryRights.CreateSubKey |
                                      RegistryRights.Delete |
                                      RegistryRights.ChangePermissions |
                                      RegistryRights.TakeOwnership;

        const InheritanceFlags inheritanceFlags = InheritanceFlags.ContainerInherit;
        const PropagationFlags propagationFlags = PropagationFlags.None;
        const AuditFlags auditFlags = AuditFlags.Success | AuditFlags.Failure;

        var rule = new RegistryAuditRule(identity, rights, inheritanceFlags, propagationFlags, auditFlags);

        bool modified;
        regSec.ModifyAuditRule(AccessControlModification.Add, rule, out modified);
        if (!modified)
        {
            regSec.AddAuditRule(rule);
        }

        regKey.SetAccessControl(regSec);
    }

    // This method ensures that the specified monitor identity (e.g., a user account) has the necessary ACL permissions
    private static void EnsureMonitorAclAccess(string keyPath, string monitorIdentity)
    {
        using var regKey = GetRegistryKeyFromPath(keyPath, createIfMissing: true);

        var regSec = regKey.GetAccessControl(AccessControlSections.Access);

        var ntAccount = new NTAccount(monitorIdentity);

        const RegistryRights rights = RegistryRights.ReadKey;
        const InheritanceFlags inheritanceFlags = InheritanceFlags.ContainerInherit;
        const PropagationFlags propagationFlags = PropagationFlags.None;
        const AccessControlType accessControlType = AccessControlType.Allow;

        var rule = new RegistryAccessRule(ntAccount, rights, inheritanceFlags, propagationFlags, accessControlType);

        bool modified;
        regSec.ModifyAccessRule(AccessControlModification.Add, rule, out modified);
        if (!modified)
        {
            regSec.AddAccessRule(rule);
        }

        regKey.SetAccessControl(regSec);
    }
}
