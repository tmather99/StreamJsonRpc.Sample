using Microsoft.Win32;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RegistryListener;

class Program
{
    private const string SubKey = @"HKEY_CURRENT_USER\Software\MyApp";

    static void Main()
    {
        Console.Title = "Registry Listener Tool";

        Console.WriteLine("=== Registry Listener ===");
        Console.WriteLine("1) Setup auditing (run once as admin)");
        Console.WriteLine("2) Start watcher (listen for registry changes)");
        Console.Write("Select option: ");

        var source1 = new RegistryEventSource(SubKey);
        source1.Subscribe(new ConsoleObserver());
        source1.Start();   // start watcher first!

        // Then trigger registry changes
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\MyApp", "Counter", 123);


        var choice = Console.ReadLine();

        if (choice == "1")
        {
            SetupAuditing();
            return;
        }

        if (choice == "2")
        {
            var source = new RegistryEventSource(SubKey);
            source.Subscribe(new ConsoleObserver());
            source.Start();

            Console.WriteLine("Listening... Press Enter to exit.");
            Console.ReadLine();
            source.Stop();
        }
        else
        {
            Console.WriteLine("Invalid selection.");
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
            RegistryRights.SetValue,
            InheritanceFlags.None,
            PropagationFlags.None,
            AuditFlags.Success);

        security.AddAuditRule(rule);
        key.SetAccessControl(security);
    }

    static void Run(string file, string args)
    {
        Process.Start(new ProcessStartInfo(file, args)
        {
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        })?.WaitForExit();
    }
}
