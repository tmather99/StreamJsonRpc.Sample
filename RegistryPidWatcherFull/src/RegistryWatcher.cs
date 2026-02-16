using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;
using static RegistryPidWatcher.NativeMethods;

namespace RegistryPidWatcher;

// This class is responsible for watching a specific registry key for changes. 
// It ensures that the necessary audit policies are in place, configures the SACL/ACL for the key,
// and uses Windows API calls to monitor for changes. This class also implements
// IObservable<RegistryEvent> and will monitor the registry and the Security event log,
// pushing parsed RegistryEvent instances to subscribers.
public class RegistryWatcher : IObservable<RegistryEvent>, IDisposable
{
    private readonly string _subKey;
    private readonly UIntPtr _rootHive;
    private IntPtr _hKey;
    private IntPtr _hEvent;
    private bool _disposed;
    private static bool _globalAuditingEnabled = false;
    private readonly List<IObserver<RegistryEvent>> _observers = [];
    private readonly Task _monitorTask;
    private readonly string _registryPath;

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

        // compute registry path used in Security event records
        _registryPath = (useCurrentUser ? "USER\\" : "MACHINE\\") + _subKey;

        // start background work on the thread pool that waits for registry changes and reads event log entries
        _monitorTask = Task.Run(() => MonitorLoopAsync());
    }

    public IDisposable Subscribe(IObserver<RegistryEvent> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        lock (_observers)
        {
            _observers.Add(observer);
        }

        return (observer as IDisposable)!;
    }

    // Allow explicit unsubscription by caller.
    public void Unsubscribe(IObserver<RegistryEvent> observer)
    {
        if (observer == null) return;

        lock (_observers)
        {
            _observers.Remove(observer);
        }

        (observer as IDisposable)?.Dispose();
    }

    private async Task MonitorLoopAsync()
    {
        while (!_disposed)
        {
            try
            {
                // This will block until a change occurs (or the key is deleted/recreated internally handles)
                WaitForChange();

                // small delay to allow the Security log to be written
                await Task.Delay(1000);

                foreach (RegistryEvent ev in ReadRecentEvents(_registryPath))
                {
                    NotifyObservers(ev);
                }
            }
            catch (Exception ex)
            {
                // notify observers of error
                lock (_observers)
                {
                    foreach (var o in _observers)
                    {
                        try { o.OnError(ex); } catch { }
                    }
                }
            }
        }
    }

    private void NotifyObservers(RegistryEvent ev)
    {
        lock (_observers)
        {
            foreach (var o in _observers)
            {
                try { o.OnNext(ev); } catch { }
            }
        }
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
    }

    // This method waits for a change to occur on the monitored registry key.
    // If the key is deleted while monitoring, it will wait for the key to be recreated.
    public void WaitForChange()
    {
        // Wait for a change to occur on the monitored registry key.
        while (true)
        {
            try
            {
                // Set up the registry change notification. This will signal the event when a change occurs.
                // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regnotifychangekeyvalue
                int notifyRes = RegNotifyChangeKeyValue(_hKey,
                                                         bWatchSubtree: true,
                                                         REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_CHANGE_NAME,
                                                         _hEvent,
                                                         fAsynchronous: true);

                if (notifyRes != 0) throw new Win32Exception(notifyRes);

                // Wait for the event to be signaled, indicating a registry change has occurred.
                uint waitRes = WaitForSingleObject(_hEvent, NativeMethods.INFINITE);

                if (waitRes == NativeMethods.WAIT_OBJECT_0)
                {
                    ResetEvent(_hEvent);
                }

                break;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1018)
            {
                // ERROR_KEY_DELETED means the key was deleted while we were waiting.
                // In this case, we need to wait for the key to be recreated before we can continue monitoring.
                Console.WriteLine("Root key deleted. Waiting for recreation...");

                // Recreate root node!!!
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
        // close native event handle
        try { if (_hEvent != IntPtr.Zero) NativeMethods.ResetEvent(_hEvent); } catch { }
        // Background task will exit when _disposed is true; nothing to interrupt on thread pool tasks.
        if (_hKey != IntPtr.Zero)
            RegCloseKey(_hKey);
    }

    // The following method embeds the EventLogReader logic to read recent events matching a registry path.
    private IEnumerable<RegistryEvent> ReadRecentEvents(string registryPath)
    {
        int delta = 5;
        int ms = delta * 1000;

        string timeFilter =
            $"""
            *[System[
                (EventID=4657 or EventID=4659 or EventID=4660 or EventID=4663)
                and TimeCreated[timediff(@SystemTime) <= {ms}]
            ]]
            """;

        EventLogQuery query = new("Security", PathType.LogName, timeFilter) {
            ReverseDirection = true,
            TolerateQueryErrors = true
        };

        using EventLogReader reader = new(query);

        for (EventRecord evt = reader.ReadEvent(); evt != null; evt = reader.ReadEvent())
        {
            if (string.IsNullOrEmpty(registryPath))
            {
                continue;
            }

            if (evt.TimeCreated == null) continue;

            string xml = evt.ToXml();
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            if (Program.DumpXml)
            {
                XDocument pretty = XDocument.Parse(xml);
                Console.WriteLine("\n" + pretty.ToString(SaveOptions.None) + "\n");
            }

            string key = Get("ObjectName");

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // Normalize registry path from Security log into the same format as _registryPath
            string normalizedKey = NormalizeRegistryPath(key);

            // skip events that don't match the prefix
            if (string.IsNullOrEmpty(normalizedKey) ||
                !normalizedKey.StartsWith(registryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string rawPid = Get("ProcessId");
            if (string.IsNullOrEmpty(rawPid)) continue;
            int pid = ParsePid(rawPid);

            string proc = Get("ProcessName");
            string valueName = Get("ObjectValueName");
            string oldValueType = Get("OldValueType");
            string oldValue = Get("OldValue");
            string newValueType = Get("NewValueType");

            string newValue = Get("NewValue");
            string accessMaskRaw = Get("AccessMask");
            string accessMaskText = DecodeRegistryAccessMask(accessMaskRaw);
            string operationTypeRaw = Get("OperationType");
            string operationType = DecodeOperationType(operationTypeRaw, oldValueType, oldValue, newValueType, newValue);

            string inferredAction = InferredAction();

            yield return new RegistryEvent {
                Pid = pid,
                Process = proc,
                Key = key,
                ValueName = valueName,
                AccessMaskRaw = accessMaskRaw,
                AccessMaskText = accessMaskText,
                OperationType = operationType,
                NewValue = newValue,
                InferredAction = inferredAction
            };

            string NormalizeRegistryPath(string raw)
            {
                if (raw.StartsWith(@"\REGISTRY\MACHINE\", StringComparison.OrdinalIgnoreCase))
                {
                    return "MACHINE\\" + raw.Substring(@"\REGISTRY\MACHINE\".Length);
                }

                if (raw.StartsWith(@"\REGISTRY\USER\", StringComparison.OrdinalIgnoreCase))
                {
                    var afterUser = raw.Substring(@"\REGISTRY\USER\".Length);
                    var firstBackslash = afterUser.IndexOf('\\');
                    if (firstBackslash > 0)
                    {
                        var withoutSid = afterUser.Substring(firstBackslash + 1);
                        return "USER\\" + withoutSid;
                    }
                }

                return raw;
            }

            string Get(string name)
            {
                var node = doc.SelectSingleNode(
                    "/*[local-name()='Event']/*[local-name()='EventData']/*[local-name()='Data' and @Name='" + name + "']");
                return node?.InnerText?.Trim() ?? string.Empty;
            }

            int ParsePid(string rawPid1)
            {
                int i = rawPid1.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(rawPid1[2..], 16)
                    : int.Parse(rawPid1);
                return i;
            }

            string InferredAction()
            {
                string s;
                if (!string.IsNullOrEmpty(valueName))
                {
                    s = operationType;
                }
                else if (!string.IsNullOrEmpty(accessMaskRaw))
                {
                    s = accessMaskText.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
                        ? "KeyDeleteOrHandleClose"
                        : "KeyAccess";
                }
                else
                {
                    s = string.Empty;
                }

                return s;
            }

            static string DecodeRegistryAccessMask(string hex)
            {
                if (string.IsNullOrWhiteSpace(hex))
                    return string.Empty;

                int mask = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToInt32(hex[2..], 16)
                    : Convert.ToInt32(hex, 16);

                var parts = new List<string>();

                if ((mask & 0x1) != 0) parts.Add("KEY_QUERY_VALUE");
                if ((mask & 0x2) != 0) parts.Add("KEY_SET_VALUE");
                if ((mask & 0x4) != 0) parts.Add("KEY_CREATE_SUB_KEY");
                if ((mask & 0x8) != 0) parts.Add("KEY_ENUMERATE_SUB_KEYS");
                if ((mask & 0x10) != 0) parts.Add("KEY_NOTIFY");
                if ((mask & 0x20) != 0) parts.Add("KEY_CREATE_LINK");

                if ((mask & 0x00010000) != 0) parts.Add("DELETE");
                if ((mask & 0x00020000) != 0) parts.Add("READ_CONTROL");
                if ((mask & 0x00040000) != 0) parts.Add("WRITE_DAC");
                if ((mask & 0x00080000) != 0) parts.Add("WRITE_OWNER");

                if ((mask & unchecked((int)0x80000000)) != 0) parts.Add("GENERIC_READ");
                if ((mask & 0x40000000) != 0) parts.Add("GENERIC_WRITE");
                if ((mask & 0x20000000) != 0) parts.Add("GENERIC_EXECUTE");
                if ((mask & 0x10000000) != 0) parts.Add("GENERIC_ALL");

                return parts.Count == 0 ? mask.ToString("X") : string.Join("|", parts);
            }

            static string DecodeOperationType(string op,
                                              string oldValueType, string oldValue,
                                              string newValueType, string newValue)
            {
                if (op == "%%1906" &&
                    !string.IsNullOrEmpty(oldValueType) &&
                    (string.Equals(newValueType, "-", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(newValue, "-", StringComparison.OrdinalIgnoreCase)))
                {
                    return "ValueDeleted";
                }

                if (op == "%%1905" &&
                    (string.IsNullOrEmpty(oldValueType) ||
                     string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase)) &&
                    string.IsNullOrEmpty(oldValue) &&
                    !string.IsNullOrEmpty(newValue) &&
                    !string.Equals(newValue, "-", StringComparison.OrdinalIgnoreCase))
                {
                    return "ValueCreated";
                }

                if (op == "%%1905" &&
                    (!string.IsNullOrEmpty(oldValue) ||
                     (!string.IsNullOrEmpty(oldValueType) &&
                      !string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase))))
                {
                    return "ValueModified";
                }

                if (op == "%%1904" &&
                    (string.IsNullOrEmpty(oldValueType) ||
                     string.Equals(oldValueType, "-", StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(oldValue) ||
                     string.Equals(oldValue, "-", StringComparison.OrdinalIgnoreCase)))
                {
                    return "ValueCreated";
                }

                return op switch {
                    "%%1904" => "ValueDeleted",
                    "%%1905" => "ValueCreated",
                    "%%1906" => "ValueModified",
                    _ => op
                };
            }
        }
    }
}
