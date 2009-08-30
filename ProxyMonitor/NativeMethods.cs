using System;
using System.Runtime.InteropServices;

namespace ProxyMonitor
{
    /// <summary>
    /// Contains members for Win32 interop.
    /// </summary>
    internal static class NativeMethods
    {
        public static int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public static int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);
    }
}