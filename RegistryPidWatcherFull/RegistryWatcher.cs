using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using static RegistryMonitor.NativeMethods;

namespace RegistryMonitor;

// This class is responsible for watching a specific registry key for changes. 
// It ensures that the necessary audit policies are in place, configures the SACL/ACL for the key,
// and uses Windows API calls to monitor for changes.
public class RegistryWatcher : IDisposable
{
    private readonly string _subKey;
    private readonly UIntPtr _rootHive;
    private IntPtr _hKey;
    private IntPtr _hEvent;
    private EventWaitHandle _waitHandle = null!;
    private bool _disposed;
    private static bool _globalAuditingEnabled = false;

    public WaitHandle WaitHandle => _waitHandle;

    public RegistryWatcher(string subKey, bool useCurrentUser = false)
    {
        _subKey = subKey;
        _rootHive = useCurrentUser ? HKEY_CURRENT_USER : HKEY_LOCAL_MACHINE;

        // 1. Make sure audit policy is enabled at OS level
        EnableGlobalRegistryAuditing();

        // 2. Configure SACL/ACL on the root key. We currently only configure auditing via PowerShell-style
        // paths for HKLM and HKCU.
        string hivePrefix = useCurrentUser ? "HKCU" : "HKLM";
        string psPath = $"{hivePrefix}:\\{_subKey}";
        RegistryAuditConfigurator.ConfigureRegistryAudit([psPath]);

        // 3. Open the key and start watching
        OpenKey();

        // Initial wait to ensure we're subscribed before any events are fired
        this.WaitForChange();
    }

    // This method enables global auditing for registry events using the auditpol.exe command-line tool.
    private void EnableGlobalRegistryAuditing()
    {
        if (_globalAuditingEnabled) return;

        var psi = new ProcessStartInfo("auditpol.exe") {
            Arguments = "/set /subcategory:\"Registry\" /success:enable /failure:enable",
            Verb = "runas",
            CreateNoWindow = true,
            UseShellExecute = true
        };

        using var process = Process.Start(psi);
        if (process != null)
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new Exception($"auditpol failed with code {process.ExitCode}");
        }

        _globalAuditingEnabled = true;
    }

    // This method attempts to open the specified registry key with the necessary permissions for monitoring.
    private void OpenKey()
    {
        if (_hKey != IntPtr.Zero)
        {
            RegCloseKey(_hKey);
            _hKey = IntPtr.Zero;
        }

        int result = RegOpenKeyEx(_rootHive, _subKey, 0, KEY_NOTIFY, out _hKey);

        if (result != 0)
        {
            using var key = (_rootHive == HKEY_CURRENT_USER ? Registry.CurrentUser : Registry.LocalMachine)
                .CreateSubKey(_subKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
            // Confirming usage of HKEY_CURRENT_USER constant for compilation
            result = RegOpenKeyEx(_rootHive, _subKey, 0, KEY_NOTIFY, out _hKey);
            if (result != 0) throw new Win32Exception(result, $"Failed to open or create {_subKey}");
        }

        // Create an event that will be signaled when a registry change occurs.
        // This event will be used in the RegNotifyChangeKeyValue call.
        _hEvent = CreateEvent(IntPtr.Zero, true, false, null);
        _waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset) {
            SafeWaitHandle = new SafeWaitHandle(_hEvent, ownsHandle: false)
        };
    }

    // This method waits for a change to occur on the monitored registry key.
    // If the key is deleted while monitoring, it will wait for the key to be recreated.
    public void WaitForChange(bool blocked = false)
    {
        while (true)
        {
            try
            {
                // Set up the registry change notification. This will signal the event when a change occurs.
                // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue
                int res = RegNotifyChangeKeyValue(_hKey,
                                                  bWatchSubtree: true,
                                                  REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_CHANGE_NAME,
                                                  _hEvent,
                                                  fAsynchronous: true);

                if (res != 0) throw new Win32Exception(res);

                if (blocked)
                {
                    // Wait for the event to be signaled, indicating a registry change has occurred.
                    _waitHandle.WaitOne();
                    ResetEvent(_hEvent);
                }

                break;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1018)
            {
                // ERROR_KEY_DELETED means the key was deleted while we were waiting.
                // In this case, we need to wait for the key to be recreated before we can continue monitoring.
                Console.WriteLine("Root key deleted. Waiting for recreation...");
                RecreateRegistryMonitor();
            }
        }
    }

    // This method continuously attempts to open the registry key until it succeeds,
    // which indicates that the key has been recreated after being deleted.
    private void RecreateRegistryMonitor()
    {
        try
        {
            // Reconfigure audit settings in case the key was recreated without the necessary SACL/ACL.
            string hivePrefix = _rootHive == HKEY_CURRENT_USER ? "HKCU" : "HKLM";
            string psPath = $"{hivePrefix}:\\{_subKey}";
            RegistryAuditConfigurator.ConfigureRegistryAudit([psPath]);
            OpenKey();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Root key recreated. Monitoring resumed.\n");
            Console.ResetColor();
        }
        catch
        {
            System.Threading.Thread.Sleep(1000);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _waitHandle?.Dispose();
        if (_hKey != IntPtr.Zero)
            RegCloseKey(_hKey);
    }
}
