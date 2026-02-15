namespace RegistryPidWatcher;

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
// allows the generated interop layer to use pointers internally.
internal static partial class NativeMethods
{
    public const int KEY_NOTIFY = 0x0010;
    public const int REG_NOTIFY_CHANGE_NAME = 0x00000001;
    public const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;

    public static readonly UIntPtr HKEY_LOCAL_MACHINE = 0x80000002u;
    public static readonly UIntPtr HKEY_CURRENT_USER = 0x80000001u;

    // -----------------------------
    // Registry APIs
    // -----------------------------

    [LibraryImport("advapi32.dll",
        EntryPoint = "RegOpenKeyExW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    public static partial int RegOpenKeyEx(
        UIntPtr hKey,
        string subKey,
        int ulOptions,
        int samDesired,
        out IntPtr phkResult);

    [LibraryImport("advapi32.dll",
        EntryPoint = "RegNotifyChangeKeyValue",
        SetLastError = true)]
    public static partial int RegNotifyChangeKeyValue(
        IntPtr hKey,
        [MarshalAs(UnmanagedType.Bool)] bool bWatchSubtree,
        int dwNotifyFilter,
        IntPtr hEvent,
        [MarshalAs(UnmanagedType.Bool)] bool fAsynchronous);

    [LibraryImport("advapi32.dll",
        EntryPoint = "RegCloseKey",
        SetLastError = true)]
    public static partial int RegCloseKey(IntPtr hKey);

    // -----------------------------
    // Event APIs
    // -----------------------------

    [LibraryImport("kernel32.dll",
        EntryPoint = "CreateEventW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateEvent(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName);

    [LibraryImport("kernel32.dll",
        EntryPoint = "ResetEvent",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ResetEvent(IntPtr hEvent);
}