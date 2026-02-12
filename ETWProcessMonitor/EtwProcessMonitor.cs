// EtwProcessMonitor.cs
// Native AOT-compatible system-wide process exit detection via ETW.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace EtwProcessMonitor
{
    public sealed class ProcessExitEventArgs : EventArgs
    {
        public int ProcessId { get; }
        public int ExitCode { get; }
        public string ImageName { get; }
        public string ExitCodeDescription { get; }

        internal ProcessExitEventArgs(int pid, int exitCode, string imageName)
        {
            ProcessId = pid;
            ExitCode = exitCode;
            ImageName = imageName;
            ExitCodeDescription = GetExitCodeDescription(exitCode);
        }

        private static string GetExitCodeDescription(int exitCode)
        {
            // Success case
            if (exitCode == 0)
                return "SUCCESS";

            // Convert to unsigned for NTSTATUS codes (0xC0000000 range)
            uint code = unchecked((uint)exitCode);

            return code switch {
                // Common NTSTATUS codes
                0xC0000005 => "ACCESS_VIOLATION",
                0xC000013A => "CTRL_C_EXIT",
                0xC0000135 => "DLL_NOT_FOUND",
                0xC0000142 => "DLL_INIT_FAILED",
                0xC000001D => "ILLEGAL_INSTRUCTION",
                0xC0000094 => "INTEGER_DIVIDE_BY_ZERO",
                0xC0000409 => "STACK_BUFFER_OVERRUN",
                0xC00000FD => "STACK_OVERFLOW",
                0xC000041D => "FATAL_USER_CALLBACK_EXCEPTION",
                0xC0000374 => "HEAP_CORRUPTION",
                0xC015000F => "FAIL_FAST",

                // Common Win32 error codes
                1 => "ERROR_INVALID_FUNCTION",
                2 => "ERROR_FILE_NOT_FOUND",
                3 => "ERROR_PATH_NOT_FOUND",
                5 => "ERROR_ACCESS_DENIED",
                87 => "ERROR_INVALID_PARAMETER",
                1223 => "ERROR_CANCELLED",

                // Otherwise show hex
                _ => exitCode >= 0
                    ? $"0x{exitCode:X}"
                    : $"0x{code:X8}"
            };
        }
    }

    public sealed class SystemProcessExitMonitor : IDisposable
    {
        private static readonly Guid NtKernelLoggerGuid =
            new Guid("9E814AAD-3204-11D2-9A82-006008A86939");

        private const string NtKernelLoggerName = "NT Kernel Logger";
        private const uint EnableFlags = 0x00000001; // EVENT_TRACE_FLAG_PROCESS

        public event EventHandler<ProcessExitEventArgs>? ProcessExited;

        // Diagnostic hex-dump hook. Set ETWMON_DIAG=1 to enable output in Program.cs.
        public event EventHandler<string>? DiagnosticEvent;

        private ulong _sessionHandle = Etw.INVALID_PROCESSTRACE_HANDLE;
        private ulong _traceHandle = Etw.INVALID_PROCESSTRACE_HANDLE;
        private Thread? _processThread;
        private bool _disposed;

        // Pointer size read from TRACE_LOGFILE_HEADER.PointerSize after OpenTrace.
        // Authoritative source — not EVENT_HEADER flags, which can be unreliable in
        // real-time mode.
        private int _pointerSize = 8; // safe default for x64

        private readonly Etw.BufferCallback _bufferCallback;
        private readonly Etw.EventCallback _eventCallback;
        private readonly nint _bufferCallbackPtr;
        private readonly nint _eventCallbackPtr;
        private readonly string _loggerName = NtKernelLoggerName;

        public unsafe SystemProcessExitMonitor()
        {
            _bufferCallback = OnBuffer;
            _eventCallback = OnEvent;
            _bufferCallbackPtr = Marshal.GetFunctionPointerForDelegate(_bufferCallback);
            _eventCallbackPtr = Marshal.GetFunctionPointerForDelegate(_eventCallback);
        }

        // -------------------------------------------------------------------------
        // Start / Stop
        // -------------------------------------------------------------------------

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_processThread is not null)
                throw new InvalidOperationException("Monitor is already running.");
            StopExistingSession();
            StartNewSession();
            OpenAndProcess();
        }

        public void Stop()
        {
            if (_traceHandle != Etw.INVALID_PROCESSTRACE_HANDLE)
            {
                Etw.CloseTrace(_traceHandle);
                _traceHandle = Etw.INVALID_PROCESSTRACE_HANDLE;
            }
            _processThread?.Join(TimeSpan.FromSeconds(5));
            _processThread = null;
            CloseSession();
        }

        // -------------------------------------------------------------------------
        // ETW session management
        // -------------------------------------------------------------------------

        private unsafe void StopExistingSession()
        {
            int propSize = sizeof(Etw.EVENT_TRACE_PROPERTIES)
                         + 2 * (NtKernelLoggerName.Length + 1) * sizeof(char);
            byte* buf = stackalloc byte[propSize];
            Unsafe.InitBlockUnaligned(buf, 0, (uint)propSize);
            var props = (Etw.EVENT_TRACE_PROPERTIES*)buf;
            props->Wnode.BufferSize = (uint)propSize;
            props->Wnode.Guid = NtKernelLoggerGuid;
            props->Wnode.Flags = Etw.WNODE_FLAG_TRACED_GUID;
            props->LogFileNameOffset = 0;
            props->LoggerNameOffset = (uint)sizeof(Etw.EVENT_TRACE_PROPERTIES);
            Etw.ControlTrace(0, NtKernelLoggerName, props, Etw.EVENT_TRACE_CONTROL_STOP);
        }

        private unsafe void StartNewSession()
        {
            int propSize = sizeof(Etw.EVENT_TRACE_PROPERTIES)
                         + 2 * (NtKernelLoggerName.Length + 1) * sizeof(char);
            byte* buf = stackalloc byte[propSize];
            Unsafe.InitBlockUnaligned(buf, 0, (uint)propSize);
            var props = (Etw.EVENT_TRACE_PROPERTIES*)buf;
            props->Wnode.BufferSize = (uint)propSize;
            props->Wnode.Guid = NtKernelLoggerGuid;
            props->Wnode.Flags = Etw.WNODE_FLAG_TRACED_GUID;
            props->LogFileMode = Etw.EVENT_TRACE_REAL_TIME_MODE;
            props->EnableFlags = EnableFlags;
            props->BufferSize = 64;
            props->MinimumBuffers = 4;
            props->MaximumBuffers = 64;
            props->LogFileNameOffset = 0;
            props->LoggerNameOffset = (uint)sizeof(Etw.EVENT_TRACE_PROPERTIES);
            ulong handle = 0;
            int hr = Etw.StartTrace(out handle, NtKernelLoggerName, props);
            if (hr != 0)
                throw new InvalidOperationException(
                    $"StartTrace failed with error 0x{hr:X8}. Run as Administrator.");
            _sessionHandle = handle;
        }

        private unsafe void CloseSession()
        {
            if (_sessionHandle == 0 || _sessionHandle == Etw.INVALID_PROCESSTRACE_HANDLE)
                return;
            int propSize = sizeof(Etw.EVENT_TRACE_PROPERTIES)
                         + 2 * (NtKernelLoggerName.Length + 1) * sizeof(char);
            byte* buf = stackalloc byte[propSize];
            Unsafe.InitBlockUnaligned(buf, 0, (uint)propSize);
            var props = (Etw.EVENT_TRACE_PROPERTIES*)buf;
            props->Wnode.BufferSize = (uint)propSize;
            props->Wnode.Guid = NtKernelLoggerGuid;
            props->Wnode.Flags = Etw.WNODE_FLAG_TRACED_GUID;
            props->LoggerNameOffset = (uint)sizeof(Etw.EVENT_TRACE_PROPERTIES);
            Etw.ControlTrace(_sessionHandle, null, props, Etw.EVENT_TRACE_CONTROL_STOP);
            _sessionHandle = 0;
        }

        // -------------------------------------------------------------------------
        // OpenTrace + ProcessTrace
        // -------------------------------------------------------------------------

        private unsafe void OpenAndProcess()
        {
            Etw.EVENT_TRACE_LOGFILE_NATIVE logFile = default;
            fixed (char* namePtr = _loggerName)
            {
                logFile.LogFileName = nint.Zero;
                logFile.LoggerName = (nint)namePtr;
                logFile.ProcessTraceMode = Etw.PROCESS_TRACE_MODE_REAL_TIME |
                                           Etw.PROCESS_TRACE_MODE_EVENT_RECORD;
                logFile.BufferCallback = _bufferCallbackPtr;
                logFile.EventCallback = _eventCallbackPtr;
                _traceHandle = Etw.OpenTrace((nint)(&logFile));

                if (_traceHandle != Etw.INVALID_PROCESSTRACE_HANDLE)
                {
                    // Read the authoritative pointer size from TRACE_LOGFILE_HEADER.
                    // PointerSize lives in the second union of LogfileHeader; Microsoft
                    // documents it as "Default size of a pointer data type, in bytes."
                    uint ps = logFile.LogfileHeader.PointerSize;
                    if (ps == 4 || ps == 8)
                        _pointerSize = (int)ps;
                    // else: leave the default 8 — should never happen on a supported OS
                }
            }

            if (_traceHandle == Etw.INVALID_PROCESSTRACE_HANDLE)
                throw new InvalidOperationException(
                    $"OpenTrace failed. Win32 error: {Marshal.GetLastWin32Error()}");

            _processThread = new Thread(ProcessTraceThread) {
                IsBackground = true,
                Name = "ETW-ProcessTrace"
            };
            _processThread.Start();
        }

        private unsafe void ProcessTraceThread()
        {
            ulong h = _traceHandle;
            Etw.ProcessTrace(&h, 1, nint.Zero, nint.Zero);
        }

        // -------------------------------------------------------------------------
        // ETW Callbacks
        // -------------------------------------------------------------------------

        private unsafe uint OnBuffer(Etw.EVENT_TRACE_LOGFILE_NATIVE* logFile) => 1;

        private unsafe void OnEvent(Etw.EVENT_RECORD* record)
        {
            byte* data = (byte*)record->UserData;
            int dataLen = record->UserDataLength;

            Guid providerId = record->EventHeader.ProviderId;
            byte opcode = record->EventHeader.EventDescriptor.Opcode;
            byte version = record->EventHeader.EventDescriptor.Version;

            if (providerId != Etw.ProcessProviderGuid) return;

            // Emit diagnostic dump for all process events (Start + End + DCStart + DCEnd)
            if (DiagnosticEvent != null && (opcode == 1 || opcode == 2 || opcode == 3 || opcode == 4))
            {
                var sb = new StringBuilder();
                sb.Append($"opcode={opcode} ver={version} hdrFlags=0x{record->EventHeader.Flags:X4} ");
                sb.Append($"ptrSz={_pointerSize} dataLen={dataLen}  hex: ");
                int dumpLen = Math.Min(dataLen, 80);
                for (int i = 0; i < dumpLen; i++)
                {
                    if (i > 0 && i % 8 == 0) sb.Append(' ');
                    sb.Append($"{data[i]:X2} ");
                }
                DiagnosticEvent?.Invoke(this, sb.ToString());
            }

            if (opcode != 2) return; // only Process/End

            // -----------------------------------------------------------------------
            // MOF payload layout — determined by event version.
            //
            // Source: https://learn.microsoft.com/en-us/windows/win32/etw/process-typegroup1
            //         https://learn.microsoft.com/en-us/windows/win32/etw/process-v2-typegroup1
            //         https://github.com/jdu2600/Windows10EtwEvents
            //         PerfView KernelTraceEventParser GetKernelImageNameOffset():
            //           SkipSID(ver>=4 ? HostOffset(28,2) : ver>=3 ? HostOffset(24,2) : HostOffset(20,1))
            //
            //   HostOffset(n, k) = n + k * pointerSize
            //   where n = sum of all fields as if they were 4-byte (their MOF-declared size)
            //         k = number of pointer-qualified fields (their actual extra bytes on x64)
            //
            //  Version  Fields before SID                                     SID offset
            //  ───────  ──────────────────────────────────────────────────    ──────────
            //  V1       PageDirectoryBase[4], PID[4], PPID[4], Sess[4], Exit[4]   → 20 + 0*ptrSize = 20
            //  V2       UniqueProcessKey[ptr], PID[4], PPID[4], Sess[4], Exit[4]  → 20 + 1*ptrSize
            //  V3       UniqueProcessKey[ptr], PID[4], PPID[4], Sess[4], Exit[4],
            //              DirectoryTableBase[ptr]                                → 24 + 2*ptrSize
            //  V4       UniqueProcessKey[ptr], PID[4], PPID[4], Sess[4], Exit[4],
            //              DirectoryTableBase[ptr], Flags[4]                      → 28 + 2*ptrSize
            //
            // PID is always the 2nd field after the leading pointer field (V2+) or
            // the 2nd field after PageDirectoryBase (V1).
            //
            // Therefore:
            //   pidOffset   = V1: 4,   V2+: ptrSize
            //   sidOffset   = V1: 20,  V2: 20+ptrSize, V3: 24+2*ptrSize, V4: 28+2*ptrSize
            // -----------------------------------------------------------------------

            int ptrSize = _pointerSize;

            int pidOffset, sidOffset;
            if (version == 1)
            {
                pidOffset = 4;                         // after PageDirectoryBase (4 bytes, NOT a pointer)
                sidOffset = 20;                        // 4 + 4 + 4 + 4 + 4
            }
            else if (version == 2)
            {
                pidOffset = ptrSize;                   // after UniqueProcessKey (ptr)
                sidOffset = 20 + ptrSize;              // HostOffset(20, 1)
            }
            else if (version == 3)
            {
                pidOffset = ptrSize;
                sidOffset = 24 + 2 * ptrSize;          // HostOffset(24, 2)
            }
            else // version == 4 (or any future version — treat as V4)
            {
                pidOffset = ptrSize;
                // V4 adds an 8-byte field (CreateTime/PackageFullName pointer) after Flags
                sidOffset = 36 + 2 * ptrSize;          // HostOffset(28, 2) + 8
            }

            if (dataLen < sidOffset) return;

            int pid = ReadUInt32AsInt(data, pidOffset);
            int exitCode = ReadUInt32AsInt(data, pidOffset + 4 + 4 + 4); // PID + ParentId + SessionId

            // Skip the SID (variable-length).
            // When revision (first byte) is 0, no SID is present and the field
            // is just a 4-byte DWORD of zeros.  Otherwise it is a standard SID:
            //   Revision[1] SubAuthorityCount[1] IdentifierAuthority[6]
            //   SubAuthority[SubAuthorityCount * 4]
            int offset = sidOffset;
            if (offset + 4 <= dataLen)
            {
                byte revision = data[offset];
                if (revision == 0)
                {
                    offset += 4; // no SID — skip the 4-byte zero placeholder
                }
                else if (offset + 2 <= dataLen)
                {
                    byte subAuthCount = data[offset + 1]; // SID.SubAuthorityCount
                    int sidLen = 8 + subAuthCount * 4;
                    offset += (offset + sidLen <= dataLen) ? sidLen : (dataLen - offset);
                }
            }

            // Read ANSI null-terminated ImageFileName
            string imgName = string.Empty;
            if (offset < dataLen)
            {
                int ns = offset, ne = ns;
                while (ne < dataLen && data[ne] != 0) ne++;
                if (ne > ns)
                    imgName = Encoding.ASCII.GetString(data + ns, ne - ns);
            }

            int sl = imgName.LastIndexOf('\\');
            if (sl >= 0) imgName = imgName.Substring(sl + 1);

            ProcessExited?.Invoke(this, new ProcessExitEventArgs(pid, exitCode, imgName));
        }

        // Read 4 bytes at offset as unsigned, return as int (preserves bit pattern,
        // avoids sign-extension issues when PID > 0x7FFFFFFF which shouldn't happen
        // in practice, but avoids accidental negative values).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int ReadUInt32AsInt(byte* data, int offset)
            => (int)*(uint*)(data + offset);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            GC.SuppressFinalize(this);
        }

        ~SystemProcessExitMonitor() => Stop();
    }
}