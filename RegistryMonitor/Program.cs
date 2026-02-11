using System.Diagnostics;
using System.Reactive.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace RegistryListener;

class Program
{
    private const string SubKey = @"HKCU\Software\MyApp";

    static void Main()
    {
        Console.WriteLine("RegistryMonitor (Interactive + Dynamic Keys + Auto-Audit)");
        Console.WriteLine("Run as Administrator.\n");

        var source1 = new RegistryEventSource([SubKey]);
        source1.Events.Subscribe(new ConsoleObserver());
        source1.Start();   // start watcher first!

        // Default keys to monitor at startup
        string[] defaultKeys = [@"HKCU\Software\MyApp",
                                @"HKLM\SOFTWARE\MyOtherApp"];

        // var source = new RegistryEventSource(
        //     filterKeys: new[] { @"\REGISTRY\USER\S-1-5-21-1938705280-1971118850-259519415-1000\Software\MyApp*" },
        //     operationFilters: new[] { RegistryOperationType.ValueUpdated, RegistryOperationType.KeyDeleted }
        // );
        //
        RegistryEventSource source = new(filterKeys: defaultKeys);

        // Subscribe with Rx operators
        var subscription = source.Events
            .Throttle(TimeSpan.FromSeconds(1))
            .Subscribe(e =>
            {
                Console.WriteLine($"[{e.Time:HH:mm:ss}] {e.OperationType} -> " +
                                  $"{e.KeyPath}\\{e.ValueName} = {e.NewValue} (PID {e.ProcessId}, Process {e.ProcessName})");
            });

        // Start listening
        source.Start();

        // Interactive command loop
        while (true)
        {
            Console.WriteLine("\nCommands: addkey | removekey | listkeys | addop | removeop | listops | exit");
            Console.Write("> ");

            string? cmd = Console.ReadLine()?.Trim().ToLower();
            if (string.IsNullOrEmpty(cmd)) continue;

            switch (cmd)
            {
                case "addkey":
                    Console.Write("Enter full registry path (e.g., HKCU\\Software\\MyNewApp): ");
                    string? newKey = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(newKey))
                        // Add filter and enable audit for this key
                        source.AddFilter(newKey);
                    break;

                case "removekey":
                    Console.Write("Enter registry path to remove from monitoring: ");
                    string? removeKey = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(removeKey))
                    {
                        source.RemoveFilter(removeKey);
                        Console.WriteLine($"Removed filter for {removeKey}");
                    }

                    break;

                case "listkeys":
                    Console.WriteLine("Currently monitored keys (filters):");
                    source.ListFilters();
                    break;

                case "addop":
                    Console.WriteLine(
                        "Available operations: ValueUpdated, KeyDeleted, KeyCreated, PermissionChanged");
                    Console.Write("Enter operation to add: ");
                    var opAdd = Console.ReadLine()?.Trim();
                    if (Enum.TryParse<RegistryOperationType>(opAdd, true, out var op1))
                    {
                        source.AddOperationFilter(op1);
                        Console.WriteLine($"Added operation filter: {op1}");
                    }
                    else Console.WriteLine("Invalid operation.");

                    break;

                case "removeop":
                    Console.WriteLine("Enter operation to remove: ");
                    var opRemove = Console.ReadLine()?.Trim();
                    if (Enum.TryParse<RegistryOperationType>(opRemove, true, out var op2))
                    {
                        source.RemoveOperationFilter(op2);
                        Console.WriteLine($"Removed operation filter: {op2}");
                    }
                    else Console.WriteLine("Invalid operation.");

                    break;

                case "listops":
                    source.ListOperationFilters();
                    break;

                case "exit":
                    subscription.Dispose();
                    source.Stop();
                    Console.WriteLine("Stopped.");
                    return;

                default:
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
    }

    // ================= SETUP =================

    static void SetupAuditing()
    {
        EnableAuditPolicy();
        Registry.CurrentUser.CreateSubKey(SubKey);
        AddSacl(SubKey);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nSetup complete. Event 4657 will now be logged.");
        Console.ResetColor();
    }

    static void EnableAuditPolicy()
    {
        Run("auditpol", "/set /subcategory:\"Registry\" /success:enable /failure:enable");
        Run("auditpol", "/set /category:\"Object Access\" /success:enable /failure:enable");
    }

    static void AddSacl(string subPath)
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
            RegistryRights.SetValue | RegistryRights.CreateSubKey | RegistryRights.Delete | RegistryRights.ChangePermissions,
            InheritanceFlags.None,
            PropagationFlags.None,
            AuditFlags.Success | AuditFlags.Failure);

        security.AddAuditRule(rule);
        key.SetAccessControl(security);
    }

    static void Run(string file, string args)
    {
        Process.Start(new ProcessStartInfo(file, args) {
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        })?.WaitForExit();
    }
}
