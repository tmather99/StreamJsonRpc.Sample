# RegistryListener

RegistryListener is a **.NET 10 console application** that monitors Windows registry changes in real-time.  
It supports:

- Dynamic registry key monitoring
- Dynamic operation filtering (`ValueUpdated`, `KeyDeleted`, `KeyCreated`, `PermissionChanged`)
- Automatic SACL (Security Auditing) setup for newly monitored keys
- Rx-style `IObservable` stream for events
- Shows process ID, process name, old/new values
- Interactive command-line interface

---

## ⚡ Features

1. **Interactive runtime commands** to add/remove keys and operation filters  
2. **Automatic SACL setup** for newly added keys (requires Admin)  
3. **Event logging** with process info and timestamps  
4. Supports **dynamic filters** (wildcards allowed)  
5. Fully **thread-safe** and production-ready  

---



Getting real-time notifications of registry changes and knowing exactly which process made the change (with its PID)
isn’t something you get “for free” from the standard .NET registry APIs like RegNotifyChangeKeyValue. That API only tells you that a change occurred, not which process caused it.

To get the process ID (PID) of whatever changed the registry value, you have only a few options:

🧠 Option 1 — Use ETW (Event Tracing for Windows) Registry Provider

Windows logs detailed registry operation events via ETW that include the process ID and process name performing the operation.
These events are essentially the same low-level events tools like Sysinternals Process Monitor (ProcMon) use internally.

🛠️ How it Works

Enable an ETW session that listens to the Kernel Registry provider.
Subscribe to events such as RegSetValue, RegDeleteValue, etc.

Each event contains:

PID
Thread ID
Operation type

Key handle and sometimes partial name
(Some providers may only include a “key ID”, so you might need to correlate with other events to resolve full key name.)

🧾 C# Example Using TraceEvent Library

You can use Microsoft’s TraceEvent (NuGet Microsoft.Diagnostics.Tracing.TraceEvent) to listen to registry ETW events:

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

class RegistryEtwWatcher
{
    public void Start()
    {
        using var session = new TraceEventSession("MyRegSession");

        session.EnableKernelProvider(KernelTraceEventParser.Keywords.Registry);

        session.Source.Kernel.RegistrySetValue += data =>
        {
            Console.WriteLine(
                $"[SET] PID {data.ProcessID} ({data.ProcessName}) changed {data.KeyName}\\{data.ValueName}"
            );
        };

        session.Source.Process();
    }
}


🔹 This listens for registry “set value” operations and prints PID and key details.



🧠 Option 2 — Windows Security Auditing (Event Log)

Windows Security Audit can record registry changes with PID/process name as event ID 4657 when auditing is enabled on the key.

How It Works

Set a System Access Control List (SACL) on the registry key you care about to audit writes.

Windows Security Log will record registry changes with:

Process ID
Process name
Old & new value optionally

Limitations

Requires admin and audit policy changes.
Events go to Security Log (not directly to your process).
This approach is good for forensics or logging, not real-time in your app.



❌ Option 3 — WMI RegistryValueChangeEvent

WMI provides types like RegistryValueChangeEvent you can subscribe to from C#. However:

They do not include process ID or process name.
They only notify that some change occurred.

So WMI is not adequate if you need the process that made the change.


🧠 Option 4 — Kernel Filter Driver

The only other way to do truly complete monitoring with detail (including value contents and PID) is to write
a kernel registry filter driver (similar to how ProcMon does it) using the CmRegisterCallbackEx API.

▶ This gives you callbacks for registry ops with:

Operation type
Full key name
Value data
PID

But this requires driver development + signing, which is complex and outside pure C#.

🧩 Summary — What to Choose
Method	Real-time	Gets PID	C# Friendly	Includes Value
RegNotifyChangeKeyValue	✔️	❌	✔️	❌
WMI Registry Events	✔️	❌	✔️	❌
ETW (TraceEvent)	✔️	✔️	✔️	Partial
Security Auditing (Event Log)	⛔	✔️	✔️	✔️
Kernel Filter Driver	✔️	✔️	❌	✔️



Yes 👍 — you can get the PID and process name of whatever modified a registry value by using Windows Security Auditing,
specifically the Event Log event ID 4657 (“A registry value was modified”).
But it won’t happen automatically — you must:

1. Enable Windows audit policy for registry access
2. Add an auditing rule (SACL) on the specific registry key you care about
3. Read the Security Event Log to extract the PID/process info

🛠️ Step 1 — Enable Audit Policy

You must turn on registry auditing in Windows’ audit policy:

You can use Local Security Policy (secpol.msc) →
Security Settings → Advanced Audit Policy Configuration → Object Access → Audit Registry → enable Success (and/or Failure) auditing.

Or via auditpol.exe:

auditpol /set /subcategory:"Registry" /success:enable


This enables registry access auditing in the system, but it alone won’t log anything until you configure auditing on the key itself.

🛠️ Step 2 — Set a SACL on the Registry Key

Windows only logs audit events for registry keys that have a System Access Control List (SACL) configured to audit them.

Example (manually via Regedit)

Open regedit.exe

Navigate to your target key (e.g., HKCU\Software\MyApp)

Right-click → Permissions

Click Advanced

Go to Auditing tab

Add a principal like Everyone or a specific account

Check Set value under Successful (and optionally Failed)

Save → OK

Now Windows will generate a Security Log event ID 4657 whenever the value changes.

🔍 Step 3 — Read Event Log (C#)

The Event Log entry for 4657 contains the process info you want:

Field	Contains
Process ID	The PID that made the change
Process Name	Full path of the executable
Object Name	Full registry key path
Object Value Name	Name of the value changed
Old/New Value	(depending on Windows version)



Event 4657: “A registry value was modified” fires when a value entry inside a key changes.

That includes:

Operation on a VALUE	Logged as 4657?	Notes
✏️ Update existing value	✅ Yes       	Most common case
➕ Create new value	    ✅ Yes       	Logged as “value modified”
❌ Delete a value	    ✅ Yes       	Shows old value but no new value

So if you do:

Set-ItemProperty HKCU:\Software\MyApp -Name Counter -Value 5
New-ItemProperty HKCU:\Software\MyApp -Name Test -Value 10
Remove-ItemProperty HKCU:\Software\MyApp -Name Counter


All three produce 4657 events (as long as SACL auditing is set).



What 4657 does NOT cover

Operation on a KEY	            Logged as 4657?	Event ID instead
📁 Create registry key	        ❌ No	        4656 / 4663
🗑 Delete registry key	        ❌ No	        4660 / 4663
🔐 Change permissions on key	❌ No	        4670

So:

New-Item HKCU:\Software\MyApp\SubKey
Remove-Item HKCU:\Software\MyApp\SubKey


You will NOT get 4657.
You’ll see 4663 (“An attempt was made to access an object”) with access types like:

DELETE
WRITE_DAC
WRITE_OWNER
KEY_CREATE_SUB_KEY


Registry
 └── Key (folder)
      ├── Value1 = 123
      ├── Value2 = "text"

Thing changed	            Event ID
The folder (key)	        4663 / 4660
The files inside (values)	4657


If you want FULL monitoring

Your listener should watch:

Event ID	Purpose
4657	    Value create / modify / delete
4663	    Key operations (create subkey, delete key, writes)
4660	    Object deletion
4670	    Permission changes


Filter in XML like:

<QueryList>
  <Query Id="0" Path="Security">
    <Select Path="Security">
      *[System[(EventID=4657 or EventID=4663 or EventID=4660)]]
    </Select>
  </Query>
</QueryList>


