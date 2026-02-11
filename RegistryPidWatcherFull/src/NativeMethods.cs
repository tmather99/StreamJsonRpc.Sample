using System.Runtime.InteropServices;

namespace RegistryPidWatcher;

internal static class NativeMethods
{
    public const int KEY_NOTIFY = 0x0010;
    public const int REG_NOTIFY_CHANGE_NAME = 0x00000001;
    public const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
    public static readonly UIntPtr HKEY_LOCAL_MACHINE = (UIntPtr)0x80000002u;
    public static readonly UIntPtr HKEY_CURRENT_USER = (UIntPtr)0x80000001u;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, int ulOptions, int samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, int dwNotifyFilter, IntPtr hEvent, bool fAsynchronous);

    [DllImport("advapi32.dll")]
    public static extern int RegCloseKey(IntPtr hKey);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll")]
    public static extern bool ResetEvent(IntPtr hEvent);
}