// Etw.PInvoke.cs
// Complete ETW P/Invoke bindings for Native AOT.
//
// Design rules for AOT compatibility:
//   - Every struct passed to [LibraryImport] must be fully blittable (no managed fields).
//   - Strings in structs become nint (LPWSTR pointer); callers pin managed strings manually.
//   - Delegates in structs become nint; callers use Marshal.GetFunctionPointerForDelegate.
//   - Variable-length byte arrays use "fixed byte[N]" (requires unsafe struct).
//   - OpenTrace takes nint (pointer to native struct) — not ref to managed struct.

using System;
using System.Runtime.InteropServices;

namespace EtwProcessMonitor
{
    internal static unsafe partial class Etw
    {
        // -------------------------------------------------------------------------
        // Well-known GUIDs
        // -------------------------------------------------------------------------

        // {3D6FA8D0-FE05-11D0-9DDA-00C04FD7BA7C}
        public static readonly Guid ProcessProviderGuid =
            new Guid("3D6FA8D0-FE05-11D0-9DDA-00C04FD7BA7C");

        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        public const ulong INVALID_PROCESSTRACE_HANDLE = ulong.MaxValue;

        public const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
        public const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
        public const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
        public const uint WNODE_FLAG_TRACED_GUID = 0x00020000;
        public const uint EVENT_TRACE_CONTROL_STOP = 1;
        public const uint EVENT_TRACE_CONTROL_QUERY = 0;

        public const ushort EVENT_HEADER_FLAG_64_BIT_HEADER = 0x0020;
        public const ushort EVENT_HEADER_FLAG_32_BIT_HEADER = 0x0040;

        // -------------------------------------------------------------------------
        // Callback delegate types
        // BufferCallback receives a pointer to the NATIVE struct (fully blittable).
        // -------------------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate uint BufferCallback(EVENT_TRACE_LOGFILE_NATIVE* logFile);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void EventCallback(EVENT_RECORD* record);

        // -------------------------------------------------------------------------
        // WNODE_HEADER  (48 bytes)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct WNODE_HEADER
        {
            public uint BufferSize;
            public uint ProviderId;
            public ulong HistoricalContext;
            public ulong TimeStamp;
            public Guid Guid;
            public uint ClientContext;
            public uint Flags;
        }

        // -------------------------------------------------------------------------
        // EVENT_TRACE_PROPERTIES  (blittable — stackalloc'd with trailing strings)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_TRACE_PROPERTIES
        {
            public WNODE_HEADER Wnode;
            public uint BufferSize;
            public uint MinimumBuffers;
            public uint MaximumBuffers;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint FlushTimer;
            public uint EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers;
            public uint FreeBuffers;
            public uint EventsLost;
            public uint BuffersWritten;
            public uint LogBuffersLost;
            public uint RealTimeBuffersLost;
            public nint LoggerThreadId;
            public uint LogFileNameOffset;
            public uint LoggerNameOffset;
        }

        // -------------------------------------------------------------------------
        // EVENT_DESCRIPTOR  (12 bytes)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_DESCRIPTOR
        {
            public ushort Id;
            public byte Version;
            public byte Channel;
            public byte Level;
            public byte Opcode;
            public ushort Task;
            public ulong Keyword;
        }

        // -------------------------------------------------------------------------
        // EVENT_HEADER  (~80 bytes on x64)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_HEADER
        {
            public ushort Size;
            public ushort HeaderType;
            public ushort Flags;
            public ushort EventProperty;
            public uint ThreadId;
            public uint ProcessId;
            public long TimeStamp;
            public Guid ProviderId;
            public EVENT_DESCRIPTOR EventDescriptor;
            public ulong ProcessorTime;
            public Guid ActivityId;
        }

        // -------------------------------------------------------------------------
        // ETW_BUFFER_CONTEXT  (4 bytes)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct ETW_BUFFER_CONTEXT
        {
            public byte ProcessorNumber;
            public byte Alignment;
            public ushort LoggerId;
        }

        // -------------------------------------------------------------------------
        // EVENT_HEADER_EXTENDED_DATA_ITEM
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_HEADER_EXTENDED_DATA_ITEM
        {
            public ushort Reserved1;
            public ushort ExtType;
            public ushort Reserved2;
            public ushort DataSize;
            public ulong DataPtr;
        }

        // -------------------------------------------------------------------------
        // EVENT_RECORD
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_RECORD
        {
            public EVENT_HEADER EventHeader;
            public ETW_BUFFER_CONTEXT BufferContext;
            public ushort ExtendedDataCount;
            public ushort UserDataLength;
            public EVENT_HEADER_EXTENDED_DATA_ITEM* ExtendedData;
            public void* UserData;
            public void* UserContext;
        }

        // -------------------------------------------------------------------------
        // EVENT_TRACE_HEADER  (classic, 48 bytes, embedded in EVENT_TRACE)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_TRACE_HEADER
        {
            public ushort Size;
            public ushort FieldTypeFlags;
            public byte Type;
            public byte Level;
            public ushort Version;
            public uint ThreadId;
            public uint ProcessId;
            public long TimeStamp;
            public Guid Guid;
            public uint KernelTime;
            public uint UserTime;
        }

        // -------------------------------------------------------------------------
        // EVENT_TRACE  (classic event, embedded in EVENT_TRACE_LOGFILE_NATIVE)
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_TRACE
        {
            public EVENT_TRACE_HEADER Header;
            public uint InstanceId;
            public uint ParentInstanceId;
            public Guid ParentGuid;
            public nint MofData;
            public uint MofLength;
            public uint ClientContext;
        }

        // -------------------------------------------------------------------------
        // TRACE_LOGFILE_HEADER  — fully blittable.
        // TIME_ZONE_INFORMATION is 172 bytes in the Windows SDK; represented as a
        // fixed byte array to avoid managed array bloat and stay AOT-safe.
        // -------------------------------------------------------------------------

        // TRACE_LOGFILE_HEADER contains a union at the LogInstanceGuid position:
        //   union {
        //       GUID LogInstanceGuid;      // 16 bytes
        //       struct {
        //           ULONG StartBuffers;    // +0
        //           ULONG PointerSize;     // +4  "Default pointer size, in bytes"
        //           ULONG EventsLost;      // +8
        //           ULONG CpuSpeedInMHz;   // +12
        //       };
        //   };
        // We use Explicit layout so both LogInstanceGuid and PointerSize are accessible.
        // All offsets are computed for the 64-bit form of the header (nint = 8 bytes).
        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct TRACE_LOGFILE_HEADER
        {
            [FieldOffset(0)] public uint BufferSize;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint ProviderVersion;
            [FieldOffset(12)] public uint NumberOfProcessors;
            [FieldOffset(16)] public long EndTime;
            [FieldOffset(24)] public uint TimerResolution;
            [FieldOffset(28)] public uint MaximumFileSize;
            [FieldOffset(32)] public uint LogFileMode;
            [FieldOffset(36)] public uint BuffersWritten;
            // Union: LogInstanceGuid / { StartBuffers, PointerSize, EventsLost, CpuSpeedInMHz }
            [FieldOffset(40)] public Guid LogInstanceGuid;
            [FieldOffset(40)] public uint StartBuffers;    // union alias
            [FieldOffset(44)] public uint PointerSize;     // authoritative pointer width!
            [FieldOffset(48)] public uint EventsLost2;     // union alias (not EventsLost on logfile)
            [FieldOffset(52)] public uint CpuSpeedInMHz;   // union alias
            // Two nint pointer fields (repurposed as HAL timer source on Win 7+, not dereferenced)
            [FieldOffset(56)] public nint LoggerName;
            [FieldOffset(64)] public nint LogFileName;
            // TIME_ZONE_INFORMATION starts at offset 72 on x64
            [FieldOffset(72)] public fixed byte TimeZoneInfo[172];
            // Fields after TimeZoneInfo (72 + 172 = 244)
            [FieldOffset(244)] public long BootTime;
            [FieldOffset(252)] public long PerfFreq;
            [FieldOffset(260)] public long StartTime;
            [FieldOffset(268)] public uint ReservedFlags;
            [FieldOffset(272)] public uint BuffersLost;
        }

        // -------------------------------------------------------------------------
        // EVENT_TRACE_LOGFILE_NATIVE  — fully blittable shadow of EVENT_TRACE_LOGFILE.
        //
        // All managed-type fields replaced:
        //   string?          → nint  (LPWSTR pointer; caller pins the string)
        //   delegate?        → nint  (function pointer; caller uses GetFunctionPointerForDelegate)
        //
        // This struct is pinned on the stack in OpenAndProcess() and its address
        // is passed to OpenTrace() as nint.
        // -------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct EVENT_TRACE_LOGFILE_NATIVE
        {
            public nint LogFileName;       // LPWSTR, null for real-time
            public nint LoggerName;        // LPWSTR, session name for real-time
            public long CurrentTime;
            public uint BuffersRead;
            public uint ProcessTraceMode;
            public EVENT_TRACE CurrentEvent;
            public TRACE_LOGFILE_HEADER LogfileHeader;
            public nint BufferCallback;    // PEVENT_TRACE_BUFFER_CALLBACKW
            public uint BufferSize;
            public uint Filled;
            public uint EventsLost;
            public nint EventCallback;     // union: PEVENT_CALLBACK | PEVENT_RECORD_CALLBACK
            public uint IsKernelTrace;
            public nint Context;
        }

        // -------------------------------------------------------------------------
        // -------------------------------------------------------------------------
        // P/Invoke -- ETW controller APIs (StartTrace / ControlTrace)
        //
        // DLL routing history (all still export the W-suffix names):
        //   Win XP / Vista / 7: advapi32.dll (native)
        //   Win 8:               kernelbase.dll (advapi32 stubs to it)
        //   Win 8.1+:            sechost.dll   (advapi32 stubs to it)
        //
        // Using "advapi32.dll" works on every version because the stubs are always
        // present. The explicit EntryPoint with the "W" suffix is REQUIRED --
        // Windows only exports "StartTraceW" and "ControlTraceW", never the
        // undecorated names. Without EntryPoint, [LibraryImport] asks for
        // "StartTrace" (no suffix) and GetProcAddress fails with
        // EntryPointNotFoundException at runtime.
        // -------------------------------------------------------------------------

        [LibraryImport("advapi32.dll", EntryPoint = "StartTraceW",
            StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        public static partial int StartTrace(
            out ulong SessionHandle,
            string SessionName,
            EVENT_TRACE_PROPERTIES* Properties);

        [LibraryImport("advapi32.dll", EntryPoint = "ControlTraceW",
            StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        public static partial int ControlTrace(
            ulong SessionHandle,
            string? SessionName,
            EVENT_TRACE_PROPERTIES* Properties,
            uint ControlCode);

        // -------------------------------------------------------------------------
        // P/Invoke -- ETW consumer APIs (OpenTrace / ProcessTrace / CloseTrace)
        //
        // On Win 8.1+ these live natively in sechost.dll; advapi32 stubs redirect
        // there. "advapi32.dll" works universally.
        //
        // OpenTraceW requires explicit EntryPoint for the same W-suffix reason.
        // ProcessTrace and CloseTrace have no A/W variants -- no suffix needed,
        // but we specify EntryPoint explicitly anyway to be unambiguous.
        //
        // OpenTrace receives nint (pointer to EVENT_TRACE_LOGFILE_NATIVE) because
        // the struct contains a 'fixed byte[]' field making it an unsafe type that
        // [LibraryImport] cannot express as 'ref'.
        // -------------------------------------------------------------------------

        [LibraryImport("advapi32.dll", EntryPoint = "OpenTraceW",
            SetLastError = true)]
        public static partial ulong OpenTrace(nint Logfile);

        // ProcessTrace and CloseTrace have no A/W variants.
        [LibraryImport("advapi32.dll", EntryPoint = "ProcessTrace",
            SetLastError = true)]
        public static partial uint ProcessTrace(
            ulong* HandleArray,
            uint HandleCount,
            nint StartTime,   // LPFILETIME -- nint.Zero = from now
            nint EndTime);    // LPFILETIME -- nint.Zero = real-time (open-ended)

        [LibraryImport("advapi32.dll", EntryPoint = "CloseTrace",
            SetLastError = true)]
        public static partial uint CloseTrace(ulong TraceHandle);
    }
}