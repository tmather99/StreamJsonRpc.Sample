# ETW Process Exit Monitor — Native AOT C#

System-wide process exit detection using the **NT Kernel Logger** ETW provider via
P/Invoke only. No `System.Management`, no `TraceEvent`, no WMI — fully compatible
with .NET Native AOT.

---

## Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│  Your process (elevated / Administrator)                         │
│                                                                  │
│  SystemProcessExitMonitor                                        │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  1. StartTrace()       → creates NT Kernel Logger session  │  │
│  │  2. OpenTrace()        → opens real-time consumer handle   │  │
│  │  3. ProcessTrace()     → blocks on background thread       │  │
│  │     └─ EventRecordCallback                                  │  │
│  │          └─ filters opcode=2 (Process/End)                  │  │
│  │               └─ parses raw MOF data (PID, ExitCode, image) │  │
│  │               └─ raises ProcessExited event                 │  │
│  └────────────────────────────────────────────────────────────┘  │
│                              │                                   │
│               ┌──────────────▼──────────────┐                   │
│               │  ETW kernel session (kernel) │                   │
│               │  NT Kernel Logger            │                   │
│               │  GUID: 9E814AAD-3204-...     │                   │
│               │  Flags: EVENT_TRACE_FLAG_    │                   │
│               │         PROCESS (0x00000001) │                   │
│               └─────────────────────────────┘                   │
└──────────────────────────────────────────────────────────────────┘

         Every process exit on the system fires an ETW event
         with Provider GUID 3D6FA8D0-FE05-... / Opcode 2
```

---

## ETW Event Layout — Process/End (Opcode 2)

The raw `UserData` payload (MOF-based, old-style kernel provider):

```
Offset  Size  Field
──────  ────  ─────────────────────────────────────────
0       8/4   UniqueProcessKey  (pointer, 8 on x64 / 4 on x86)
+ptr    4     ProcessId         (ULONG)
+4      4     ParentId          (ULONG)
+4      4     SessionId         (ULONG)
+4      4     ExitStatus        (LONG)   ← exit code
+4      var   UserSID           (variable-length SID)
+sid    sz    ImageFileName     (null-terminated ANSI string)
```

Version differences:
| Version | Extra field before ImageFileName     |
|---------|--------------------------------------|
| v1      | (none)                               |
| v2      | (none, SID parsing added)            |
| v3      | DirectoryTableBase (ptr)             |
| v4      | + Unicode ImageFileName at tail      |

---

## Build

### Requirements
- Windows 10/11 (x64)
- .NET 9 SDK with Native AOT workload:
  ```
  winget install Microsoft.DotNet.SDK.9
  dotnet workload install wasm-tools  # pulls native AOT cross-compiler
  ```
- Visual C++ Build Tools (for the native linker):
  ```
  winget install Microsoft.VisualStudio.2022.BuildTools
  ```

### Compile (Normal .NET — fast iteration)
```powershell
dotnet run -c Release
```

### Publish as Native AOT binary
```powershell
dotnet publish -c Release
# Output: bin\Release\net9.0\win-x64\publish\EtwProcessMonitor.exe
# ~3 MB self-contained, no .NET runtime required
```

---

## Usage

```csharp
using var monitor = new SystemProcessExitMonitor();

monitor.ProcessExited += (_, e) =>
{
    Console.WriteLine($"PID={e.ProcessId} exited with code {e.ExitCode} ({e.ImageName})");
};

monitor.Start();   // requires Administrator
// ...
monitor.Stop();    // or Dispose()
```

> **Elevation required.** `StartTrace` for the NT Kernel Logger always requires
> `SeSystemProfilePrivilege`, which is held by administrators. Running without
> elevation throws `InvalidOperationException` with HRESULT `0xC0000022`
> (ERROR_ACCESS_DENIED) or `0x00000005`.

---

## Why No `System.Management` / `TraceEvent`?

| Dependency         | Why avoided                                                  |
|--------------------|--------------------------------------------------------------|
| `System.Management`| Uses COM/WMI under the hood; COM is excluded from Native AOT |
| `TraceEvent`       | Reflection-heavy; incompatible with IL trimming + AOT        |
| `EventSource`      | Only for producing events, not consuming kernel events       |
| This solution      | Pure P/Invoke → blittable structs → zero reflection          |

---

## Native AOT Compatibility Checklist

| Concern                 | Solution used                                        |
|-------------------------|------------------------------------------------------|
| No reflection           | All types are concrete; no `Type.GetType()`, no DI   |
| Blittable structs       | All P/Invoke structs use fixed-size fields only       |
| Delegate marshalling    | `[UnmanagedFunctionPointer]` on callback delegates   |
| String marshalling      | `StringMarshalling.Utf16` on `[LibraryImport]`       |
| GC pinning              | `GCHandle.Alloc(this)` keeps monitor alive in callbacks |
| Trimmer roots           | `TrimmerRoots.xml` preserves all public/used types   |
| Unsafe blocks           | `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`        |
| Partial methods (AOT)   | `[LibraryImport]` generates source at build time     |

---

## Caveats

1. **One NT Kernel Logger session** — Windows only allows a single kernel logger
   session at a time system-wide. If another profiler (PerfView, WPR, xperf) is
   already running, `StartTrace` will return `ERROR_ALREADY_EXISTS`. The monitor
   attempts to stop any existing session first, which may interrupt other tools.

2. **Session name is fixed** — `"NT Kernel Logger"` is the reserved name for the
   kernel provider. You cannot rename it.

3. **Not all fields available for short-lived processes** — If a process exits
   before ETW flushes its buffer (~1 sec by default), the exit event may arrive
   slightly late but will not be lost.

4. **x64 only** — `is64bit` pointer-size logic assumes the host is 64-bit. For
   x86 builds, change `<PlatformTarget>` and the pointer-size branch accordingly.

---

## Files

```
EtwProcessMonitor/
├── EtwProcessMonitor.csproj   Project + AOT settings
├── EtwProcessMonitor.cs       Monitor class (public API + session/callback logic)
├── Etw.PInvoke.cs             All P/Invoke structs, constants, and imports
├── Program.cs                 Demo entry point
├── TrimmerRoots.xml           IL-linker roots for Native AOT
└── README.md                  This file
```
