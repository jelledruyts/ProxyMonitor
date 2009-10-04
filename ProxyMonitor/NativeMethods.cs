using System;
using System.Runtime.InteropServices;

namespace ProxyMonitor
{
    /// <summary>
    /// Contains members for Win32 interop.
    /// </summary>
    internal static class NativeMethods
    {
        #region Wininet.dll Enums

        private enum InternetOptionActions : int
        {
            INTERNET_OPTION_REFRESH = 37,
            INTERNET_OPTION_SETTINGS_CHANGED = 39,
            INTERNET_OPTION_PER_CONNECTION_OPTION = 75
        }

        private enum OptionType
        {
            INTERNET_PER_CONN_FLAGS = 1,
            INTERNET_PER_CONN_PROXY_SERVER = 2,
            INTERNET_PER_CONN_PROXY_BYPASS = 3,
            INTERNET_PER_CONN_AUTOCONFIG_URL = 4,
            INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5,
            INTERNET_PER_CONN_AUTOCONFIG_SECONDARY_URL = 6,
            INTERNET_PER_CONN_AUTOCONFIG_RELOAD_DELAY_MINS = 7,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_TIME = 8,
            INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_URL = 9
        }

        [Flags]
        private enum ProxyFlag
        {
            PROXY_TYPE_DIRECT = 0x00000001,
            PROXY_TYPE_PROXY = 0x00000002,
            PROXY_TYPE_AUTO_PROXY_URL = 0x00000004,
            PROXY_TYPE_AUTO_DETECT = 0x00000008
        };
        #endregion  // Wininet.dll Enums

        #region Wininet.dll Structs

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct InternetPerConnOptionList
        {
            /// <summary>
            /// size of the InternetPerConnOptionList struct
            /// </summary>
            public int dwSize;

            /// <summary>
            /// Connection name to set/query options.
            /// </summary>
            public IntPtr szConnection;

            /// <summary>
            /// Number of options to set/query
            /// </summary>
            public int dwOptionCount;

            /// <summary>
            /// On error, which option failed
            /// </summary>
            public int dwOptionError;
            public IntPtr options;
        }

        [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Auto)]
        private struct OptionData
        {
            [FieldOffset(0)]
            public int dwValue;

            [FieldOffset(0)]
            public IntPtr szValue;

            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct InternetPerConnOption
        {
            public OptionType option;
            public OptionData value;
        }

        #endregion

        #region Wininet.dll Methods

        [DllImport("wininet.dll", SetLastError = true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

        [DllImport("wininet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetQueryOption(IntPtr hInternet, InternetOptionActions dwOption, ref InternetPerConnOptionList lpOptionList, ref int lpdwBufferLength);

        [DllImport("wininet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(IntPtr hInternet, InternetOptionActions dwOption, ref InternetPerConnOptionList lpOptionList, int lpdwBufferLength);

        [DllImport("wininet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(IntPtr hInternet, InternetOptionActions dwOption, IntPtr lpbuf, int lpdwBufferLength);

        #endregion

        #region Public Methods

        /// <summary>
        /// Fetches the proxy information for a connection.
        /// </summary>
        /// <param name="sConnection">IN: The name of the connection, or null or empty for LAN.</param>
        /// <param name="sProxyServer">OUT: The proxy server</param>
        /// <param name="sProxyExceptions">OUT: The proxy exception list</param>
        /// <param name="sAutoConfigURL">OUT: The autoconfig URL</param>
        /// <returns>True if all is well.</returns>
        static public bool GetProxyInfo(String sConnection, out String sProxyServer, out String sProxyExceptions, out String sAutoConfigURL)
        {
            InternetPerConnOptionList request;
            InternetPerConnOption[] option_array = new InternetPerConnOption[4];
            bool success = false;
            IntPtr p;               // Utility pointer for iterating over unmanaged array
            IntPtr buf;             // Pointer to unmanaged option array
            int request_len;        // Holds length of request structure for InternetQueryOption call
            ProxyFlag flags;        // Holds proxy flags while we use them

            // Set default values to our output params in case we need to bail out
            sProxyServer = String.Empty;
            sProxyExceptions = String.Empty;
            sAutoConfigURL = String.Empty;

            // Fill out the option array with the stuff we're looking for
            option_array[0].option = OptionType.INTERNET_PER_CONN_FLAGS;
            option_array[1].option = OptionType.INTERNET_PER_CONN_PROXY_SERVER;
            option_array[2].option = OptionType.INTERNET_PER_CONN_PROXY_BYPASS;
            option_array[3].option = OptionType.INTERNET_PER_CONN_AUTOCONFIG_URL;

            // Marshal option_array[] to an unmanaged version 
            buf = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(InternetPerConnOption)) * option_array.Length);
            if (buf == IntPtr.Zero) { return false; }

            p = buf;
            for (int i = 0; i < option_array.Length; i++)
            {
                Marshal.StructureToPtr(option_array[i], p, false);
                p = (IntPtr)((int)p + Marshal.SizeOf(option_array[i]));
            }

            // Set up the request structure
            request.szConnection = String.IsNullOrEmpty(sConnection) ? IntPtr.Zero : Marshal.StringToCoTaskMemAnsi(sConnection);
            request.dwOptionCount = option_array.Length;
            request.options = buf;
            request.dwSize = Marshal.SizeOf(typeof(InternetPerConnOptionList));
            request.dwOptionError = 0;

            request_len = request.dwSize;

            // Call the wininet.dll function to fetch the proxy info.
            if (!NativeMethods.InternetQueryOption(IntPtr.Zero, InternetOptionActions.INTERNET_OPTION_PER_CONNECTION_OPTION, ref request, ref request_len))
            {
                int e = Marshal.GetLastWin32Error();  // Not used, but lets us peek at error value when we're debugging.

                success = false;
            }
            else
            {

                // Unmarshal the now-filled-out unmanaged option array
                p = buf;
                for (int i = 0; i < option_array.Length; i++)
                {
                    option_array[i] = (InternetPerConnOption)Marshal.PtrToStructure(p, typeof(InternetPerConnOption));
                    p = (IntPtr)((int)p + Marshal.SizeOf(option_array[i]));
                }

                // Unmarshal the string values we're looking for.

                flags = (ProxyFlag)option_array[0].value.dwValue;

                if ((flags & ProxyFlag.PROXY_TYPE_PROXY) == ProxyFlag.PROXY_TYPE_PROXY)
                {
                    // Unmarshal and return a proxy server & exceptions
                    sProxyServer = option_array[1].value.szValue == IntPtr.Zero ? String.Empty : Marshal.PtrToStringAnsi(option_array[1].value.szValue);
                    sProxyExceptions = option_array[2].value.szValue == IntPtr.Zero ? String.Empty : Marshal.PtrToStringAnsi(option_array[2].value.szValue);
                }

                if ((flags & ProxyFlag.PROXY_TYPE_AUTO_PROXY_URL) == ProxyFlag.PROXY_TYPE_AUTO_PROXY_URL)
                {
                    // Unmarshal and return the Auto Config URL
                    sAutoConfigURL = option_array[3].value.szValue == IntPtr.Zero ? String.Empty : Marshal.PtrToStringAnsi(option_array[3].value.szValue);
                }

                success = true;
            }


            // Free any strings that InternetQueryOption allocated.  Note that we're starting at index 1 (index 0 is the flags)
            for (int i = 1; i < option_array.Length; i++)
            {
                if (option_array[i].value.szValue != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(option_array[i].value.szValue);
                }
            }

            if (request.szConnection != IntPtr.Zero) { Marshal.FreeCoTaskMem(request.szConnection); }
            Marshal.FreeCoTaskMem(buf);     // Free unmanaged option array

            return success;
        }


        /// <summary>
        /// Sets the proxy info for a connection
        /// </summary>
        /// <param name="sConnection">IN: The name of the connection, or null or empty for LAN.</param>
        /// <param name="sProxyServer">IN: Proxy server</param>
        /// <param name="sProxyExceptions">IN: Proxy exception list</param>
        /// <param name="sAutoConfigURL">IN: Autoconfig URL</param>
        /// <returns>True if all is well</returns>
        static public bool SetProxyInfo(String sConnection, String sProxyServer, String sProxyExceptions, String sAutoConfigURL)
        {
            InternetPerConnOptionList request;
            InternetPerConnOption[] option_array = new InternetPerConnOption[4];
            bool success = false;
            IntPtr p;                                       // Utility pointer for iterating over unmanaged array
            IntPtr buf;                                     // Pointer to unmanaged option array
            int request_len;                                // Holds length of request structure for InternetQueryOption call
            ProxyFlag flags;                                // Used for manipulating the proxy flag values

            // Fill out the option array with the stuff we need to set.
            option_array[0].option = OptionType.INTERNET_PER_CONN_FLAGS;
            flags = ProxyFlag.PROXY_TYPE_DIRECT;
            if (!String.IsNullOrEmpty(sProxyServer)) { flags |= ProxyFlag.PROXY_TYPE_PROXY; }
            if (!String.IsNullOrEmpty(sAutoConfigURL)) { flags |= ProxyFlag.PROXY_TYPE_AUTO_PROXY_URL; }
            option_array[0].value.dwValue = (int)flags;

            option_array[1].option = OptionType.INTERNET_PER_CONN_PROXY_SERVER;
            option_array[1].value.szValue = String.IsNullOrEmpty(sProxyServer) ? IntPtr.Zero : Marshal.StringToCoTaskMemAnsi(sProxyServer);

            option_array[2].option = OptionType.INTERNET_PER_CONN_PROXY_BYPASS;
            option_array[2].value.szValue = String.IsNullOrEmpty(sProxyExceptions) ? IntPtr.Zero : Marshal.StringToCoTaskMemAnsi(sProxyExceptions);

            option_array[3].option = OptionType.INTERNET_PER_CONN_AUTOCONFIG_URL;
            option_array[3].value.szValue = String.IsNullOrEmpty(sAutoConfigURL) ? IntPtr.Zero : Marshal.StringToCoTaskMemAnsi(sAutoConfigURL);


            // Marshal option_array[] to an unmanaged version 
            buf = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(InternetPerConnOption)) * option_array.Length);
            if (buf == IntPtr.Zero)
            {
                success = false;
            }
            else
            {
                p = buf;
                for (int i = 0; i < option_array.Length; i++)
                {
                    Marshal.StructureToPtr(option_array[i], p, false);
                    p = (IntPtr)((int)p + Marshal.SizeOf(option_array[i]));
                }

                // Set up the request structure
                request.szConnection = (string.IsNullOrEmpty(sConnection) ? IntPtr.Zero : Marshal.StringToCoTaskMemAnsi(sConnection));
                request.dwOptionCount = option_array.Length;
                request.options = buf;
                request.dwSize = Marshal.SizeOf(typeof(InternetPerConnOptionList));
                request.dwOptionError = 0;

                request_len = request.dwSize;


                // Call the wininet.dll function to set the proxy info
                if (!NativeMethods.InternetSetOption(IntPtr.Zero, InternetOptionActions.INTERNET_OPTION_PER_CONNECTION_OPTION, ref request, request_len))
                {
                    // int e = Marshal.GetLastWin32Error();  // Not used, but lets us peek at error value when we're debugging.

                    success = false;
                }
                else
                {

                    // int e = Marshal.GetLastWin32Error();  // Not used, but lets us peek at error value when we're debugging.

                    // Notify the system/other programs that settings are changed
                    NativeMethods.InternetSetOption(IntPtr.Zero, InternetOptionActions.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                    NativeMethods.InternetSetOption(IntPtr.Zero, InternetOptionActions.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

                    success = true;
                }

                if (request.szConnection != IntPtr.Zero) { Marshal.FreeCoTaskMem(request.szConnection); }
                Marshal.FreeCoTaskMem(buf);
            } // buf != NULL 

            // Free the unmanaged strings that we allocated.  Note that we're starting at index 1 (index 0 is the flags)
            for (int i = 1; i < option_array.Length; i++)
            {
                if (option_array[i].value.szValue != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(option_array[i].value.szValue);
                }
            }

            return success;
        }

        #endregion // Public Methods
    }
}