using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace RegistryMonitor;

public class RegistryWatcher : IDisposable
{
    private readonly string _subKey;
    private IntPtr _hKey;
    private IntPtr _hEvent;
    private EventWaitHandle _waitHandle = null!;
    private bool _disposed;
    private static bool _globalAuditingEnabled = false;

    public RegistryWatcher(string subKey)
    {
        _subKey = subKey;

        // 1. Make sure audit policy is enabled at OS level
        EnableGlobalRegistryAuditing();

        // 2. Configure SACL/ACL on the root key (HKLM style)
        string psPath = $"HKLM:\\{_subKey}";
        RegistryAuditConfigurator.ConfigureRegistryAudit([psPath]);

        // 3. Open the key and start watching
        OpenKey();
    }

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

    private void OpenKey()
    {
        if (_hKey != IntPtr.Zero)
        {
            NativeMethods.RegCloseKey(_hKey);
            _hKey = IntPtr.Zero;
        }

        int result = NativeMethods.RegOpenKeyEx(NativeMethods.HKEY_LOCAL_MACHINE, _subKey, 0, NativeMethods.KEY_NOTIFY, out _hKey);

        if (result != 0)
        {
            using var key = Registry.LocalMachine.CreateSubKey(_subKey, RegistryKeyPermissionCheck.ReadWriteSubTree);
            result = NativeMethods.RegOpenKeyEx(NativeMethods.HKEY_LOCAL_MACHINE, _subKey, 0, NativeMethods.KEY_NOTIFY, out _hKey);
            if (result != 0) throw new Win32Exception(result, $"Failed to open or create {_subKey}");
        }

        _hEvent = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
        _waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset) {
            SafeWaitHandle = new SafeWaitHandle(_hEvent, ownsHandle: false)
        };
    }

    public void WaitForChange()
    {
        while (true)
        {
            try
            {
                int res = NativeMethods.RegNotifyChangeKeyValue(_hKey, true,
                    NativeMethods.REG_NOTIFY_CHANGE_LAST_SET | NativeMethods.REG_NOTIFY_CHANGE_NAME, _hEvent, true);
                if (res != 0) throw new Win32Exception(res);

                _waitHandle.WaitOne();
                NativeMethods.ResetEvent(_hEvent);
                break;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1018)
            {
                Console.WriteLine("Root key deleted. Waiting for recreation...");
                WaitForRecreation();
            }
        }
    }

    private void WaitForRecreation()
    {
        while (true)
        {
            try
            {
                string psPath = $"HKLM:\\{_subKey}";
                RegistryAuditConfigurator.ConfigureRegistryAudit([psPath]);
                OpenKey();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Root key recreated. Monitoring resumed.\n");
                Console.ResetColor();
                break;
            }
            catch
            {
                System.Threading.Thread.Sleep(1000);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _waitHandle?.Dispose();
        if (_hKey != IntPtr.Zero)
            NativeMethods.RegCloseKey(_hKey);
    }
}
