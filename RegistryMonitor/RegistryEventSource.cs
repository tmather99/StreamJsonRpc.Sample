using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Reactive.Subjects;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;
using static RegistryAuditEventId;
using static RegistryOperationType;

public class RegistryEventSource : IDisposable
{
    private EventLogWatcher? _watcher;
    private bool _running;

    private readonly List<string> _filterKeys = new();
    private readonly List<Regex> _filterRegexes = new();
    private readonly HashSet<RegistryOperationType> _operationFilters = new();
    private readonly object _lock = new();

    private readonly Subject<RegistryChangeEvent> _subject = new();
    public IObservable<RegistryChangeEvent> Events => _subject;

    // ---------------------------
    // Constructor
    // ---------------------------
    public RegistryEventSource(IEnumerable<string> filterKeys, IEnumerable<RegistryOperationType>? opFilters = null)
    {
        if (filterKeys == null || !filterKeys.Any())
            throw new ArgumentException("At least one filter key is required.");

        foreach (string key in filterKeys)
        {
            this.AddFilter(key);
        }

        if (opFilters != null)
        {
            foreach (RegistryOperationType op in opFilters)
            {
                this.AddOperationFilter(op);
            }
        }
    }

    //
    // AddFilter(@"HKCU\Software\MyApp*");
    // will match:
    //    HKCU\Software\MyApp\SubKey1
    //    HKCU\Software\MyApp\SubKey2\SubSubKey
    //    HKCU\Software\MyApp\Counter
    //
    public void AddFilter(string filterKey)
    {
        if (string.IsNullOrWhiteSpace(filterKey)) return;

        SetupAuditing(filterKey);

        // Convert HKCU / HKLM to Security Log internal path
        string mappedKey = this.MapKeyToSecurityLogPath(filterKey);

        lock (_lock)
        {
            //EnableAudit(filterKey);

            _filterKeys.Add(mappedKey);

            // To detect events for subkeys, make sure the filter regex ends with .* (wildcard).
            string pattern = "^" + Regex.Escape(mappedKey).Replace("\\*", ".*") + "$";
            _filterRegexes.Add(new Regex(pattern, RegexOptions.IgnoreCase));
        }
    }

    // =============================
    // 🔧 Registry Auditing Setup
    // ============================
    private static void EnableAudit(string keyPath)
    {
        try
        {
            Console.WriteLine($"Setting SACL on {keyPath}");

            string hive = keyPath.Split('\\')[0];
            string subKeyPath = keyPath.Substring(hive.Length + 1);

            RegistryKey baseKey = hive switch {
                "HKCU" => Registry.CurrentUser,
                "HKLM" => Registry.LocalMachine,
                _ => throw new InvalidOperationException("Unsupported hive")
            };

            using var regKey = baseKey.OpenSubKey(subKeyPath,
                                                  RegistryKeyPermissionCheck.ReadWriteSubTree,
                                                  RegistryRights.ChangePermissions);
            if (regKey == null)
            {
                Console.WriteLine($"Cannot open key: {keyPath}");
                return;
            }

            var regSec = regKey.GetAccessControl(AccessControlSections.Audit);

            // Audit rule: Everyone, monitor creation, deletion, value set, permissions change
            var rule = new RegistryAuditRule(
                new NTAccount("Everyone"),
                RegistryRights.SetValue | RegistryRights.CreateSubKey | RegistryRights.Delete |
                RegistryRights.ChangePermissions,
                InheritanceFlags.None,
                PropagationFlags.None,
                AuditFlags.Success | AuditFlags.Failure
            );

            regSec.AddAuditRule(rule);
            regKey.SetAccessControl(regSec);

            Console.WriteLine($"✅ SACL auditing enabled for {keyPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Failed to set audit on {keyPath}: {ex.Message}");
        }
    }

    private static void SetupAuditing(string keyPath)
    {
        EnableAuditPolicy();
        Registry.CurrentUser.CreateSubKey(keyPath);
        AddSacl(keyPath);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nSetup complete. Event 4657 will now be logged.");
        Console.ResetColor();
    }

    private static void EnableAuditPolicy()
    {
        Run("auditpol", "/set /subcategory:\"Registry\" /success:enable /failure:enable");
        Run("auditpol", "/set /category:\"Object Access\" /success:enable /failure:enable");
    }

    private static void AddSacl(string subPath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            subPath,
            RegistryKeyPermissionCheck.ReadWriteSubTree,
            RegistryRights.ChangePermissions | RegistryRights.ReadKey);

        if (key == null)
        {
            throw new InvalidOperationException($"Registry key '{subPath}' could not be opened.");
        }

        var security = key.GetAccessControl(AccessControlSections.Audit);

        var rule = new RegistryAuditRule(
            new NTAccount("Everyone"),
            RegistryRights.SetValue,
            InheritanceFlags.None,
            PropagationFlags.None,
            AuditFlags.Success);

        security.AddAuditRule(rule);
        key.SetAccessControl(security);
    }

    private static void Run(string file, string args)
    {
        Process.Start(new ProcessStartInfo(file, args) {
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        })?.WaitForExit();
    }

    private string MapKeyToSecurityLogPath(string key)
    {
        if (key.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\REGISTRY\USER\" + GetCurrentUserSID() + key.Substring(4) + "*";
        }
        else if (key.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
        {
            return @"\REGISTRY\MACHINE\" + key.Substring(4) + "*";
        }
        else
        {
            // Leave other keys as-is, just append wildcard
            return key + "*";
        }
    }

    private string GetCurrentUserSID()
    {
        using var user = System.Security.Principal.WindowsIdentity.GetCurrent();
        return user.User!.Value;
    }

    public void RemoveFilter(string filterKey)
    {
        if (string.IsNullOrWhiteSpace(filterKey)) return;

        string mappedKey = MapKeyToSecurityLogPath(filterKey);

        lock (_lock)
        {
            // Remove from filter keys
            _filterKeys.RemoveAll(k => k.Equals(mappedKey, StringComparison.OrdinalIgnoreCase));

            // Remove corresponding regex
            _filterRegexes.RemoveAll(r => r.ToString().Equals(
                "^" + Regex.Escape(mappedKey).Replace("\\*", ".*") + "$",
                StringComparison.OrdinalIgnoreCase
            ));
        }
    }

    public void ClearFilters()
    {
        lock (_lock) { _filterKeys.Clear(); _filterRegexes.Clear(); }
    }

    public void AddOperationFilter(RegistryOperationType op)
    {
        lock (_lock) _operationFilters.Add(op);
    }

    public void RemoveOperationFilter(RegistryOperationType op)
    {
        lock (_lock) _operationFilters.Remove(op);
    }

    public void ClearOperationFilters()
    {
        lock (_lock) _operationFilters.Clear();
    }

    public void ListFilters()
    {
        lock (_lock)
        {
            if (_filterKeys.Count == 0) Console.WriteLine("No filters set.");
            else foreach (var f in _filterKeys) Console.WriteLine($"- {f}");
        }
    }

    public void ListOperationFilters()
    {
        lock (_lock)
        {
            if (_operationFilters.Count == 0) Console.WriteLine("No operation filters set.");
            else foreach (var op in _operationFilters) Console.WriteLine($"- {op}");
        }
    }

    public void Start()
    {
        if (_running) return;

        // Log warning if no operation filters are set
        lock (_lock)
        {
            if (_operationFilters.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠ Warning: No operation filters defined. All registry operations will be captured.");
                Console.ResetColor();
            }
        }

        var query = new EventLogQuery("Security", PathType.LogName,
            "*[System[(EventID=4657 or EventID=4663 or EventID=4660 or EventID=4670)]]");

        _watcher = new EventLogWatcher(query);
        _watcher.EventRecordWritten += OnEvent;
        _watcher.Enabled = true;

        _running = true;
    }

    public void Stop()
    {
        if (!_running) return;

        if (_watcher != null)
        {
            _watcher.EventRecordWritten -= OnEvent;
            _watcher.Enabled = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _subject.OnCompleted();
        _running = false;
    }

    private void OnEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null) return;

        var xml = e.EventRecord.ToXml();
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        string Get(string name) => doc.SelectSingleNode($"//EventData/Data[@Name='{name}']")?.InnerText ?? "";

        var time = e.EventRecord.TimeCreated ?? DateTime.Now;
        var id = (RegistryAuditEventId)e.EventRecord.Id;
        var keyPath = Get("ObjectName");

        var opType = MapOperation(id, Get("AccessMask"));

        lock (_lock)
        {
            if (_filterRegexes.Count > 0 && !_filterRegexes.Any(r => r.IsMatch(keyPath))) return;
            // no op filter = all operation types are captured
            if (_operationFilters.Count > 0 && !_operationFilters.Contains(opType)) return;
        }

        var evt = new RegistryChangeEvent {
            Time = time,
            AuditEventId = id,
            KeyPath = keyPath,
            ValueName = Get("ObjectValueName"),
            ProcessId = Get("ProcessId"),
            ProcessName = Get("ProcessName"),
            AccessTypeRaw = Get("AccessMask"),
            OperationType = opType,
            OldValue = Get("OldValue"),
            NewValue = Get("NewValue")
        };

        _subject.OnNext(evt);
    }

    private RegistryOperationType MapOperation(RegistryAuditEventId id, string access)
    {
        return id switch {
            RegistryValueModified => ValueUpdated,
            RegistryObjectDeleted => KeyDeleted,
            RegistryPermissionsChanged => PermissionChanged,
            RegistryKeyAccessed when access.Contains("DELETE") => KeyDeleted,
            RegistryKeyAccessed when access.Contains("CREATE") => KeyCreated,
            _ => Unknown
        };
    }

    public void Dispose()
    {
        Stop();
        _subject.Dispose();
    }
}
