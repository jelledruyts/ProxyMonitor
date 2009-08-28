using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using JelleDruyts.ProxyMonitor.Configuration;
using Microsoft.Win32;

namespace JelleDruyts.ProxyMonitor
{
    /// <summary>
    /// Detects and sets the current proxy server to use.
    /// </summary>
    internal static class ProxyDetector
    {
        #region Fields & Properties

        /// <summary>
        /// Gets the configured proxy servers.
        /// </summary>
        /// <value>The configured proxy servers.</value>
        public static ProxyServerElementCollection ConfiguredProxyServers
        {
            get
            {
                return ProxyConfiguration.Instance.ProxyServers;
            }
        }

        /// <summary>
        /// Stores the detected proxy.
        /// </summary>
        private static ProxyServerElement detectedProxy;

        /// <summary>
        /// An object to lock on while detecting the proxy.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// Represents the method that will check if a proxy is reachable.
        /// </summary>
        /// <param name="proxy">The </param>
        private delegate void CheckProxyDelegate(ProxyServerElement proxy);

        /// <summary>
        /// The wait handle that signals when a proxy server is detected.
        /// </summary>
        private static AutoResetEvent waitHandle;

        #endregion

        #region Detect Proxy Server

        /// <summary>
        /// Detects the proxy.
        /// </summary>
        /// <returns>The proxy that was detected.</returns>
        public static ProxyServerElement DetectProxyServer()
        {
            lock (lockObject)
            {
                Trace.WriteLine("Detecting proxy to use...");
                detectedProxy = null;
                waitHandle = new AutoResetEvent(false);

                // Queue async operations to detect the proxy.
                List<IAsyncResult> results = new List<IAsyncResult>();
                foreach (ProxyServerElement configuredProxy in ConfiguredProxyServers)
                {
                    if (!configuredProxy.SkipAutoDetect)
                    {
                        CheckProxyDelegate proxyDetector = new CheckProxyDelegate(CheckProxy);
                        IAsyncResult result = proxyDetector.BeginInvoke(configuredProxy, null, null);
                        results.Add(result);
                    }
                }

                // Wait for the results to come in.
                bool keepWaiting = true;
                while (keepWaiting)
                {
                    // See if a proxy was found by checking the wait handle with a timeout.
                    bool signaled = waitHandle.WaitOne(250, false);
                    if (signaled)
                    {
                        // The wait handle was signaled, exit the loop.
                        Trace.WriteLine("Wait handle was signaled");
                        keepWaiting = false;
                    }
                    else
                    {
                        // Wait for all of the async operations to complete.
                        Trace.WriteLine("Wait handle was not signaled");
                        keepWaiting = false;
                        foreach (IAsyncResult result in results)
                        {
                            Trace.WriteLine(" - Async operation completed? " + result.IsCompleted);
                            if (!result.IsCompleted)
                            {
                                keepWaiting = true;
                                break;
                            }
                        }
                    }
                }

                SetProxyServer(detectedProxy);
            }
            return detectedProxy;
        }

        /// <summary>
        /// Checks if the given proxy is reachable.
        /// </summary>
        /// <param name="proxy">The proxy to check.</param>
        private static void CheckProxy(ProxyServerElement proxy)
        {
            if (!string.IsNullOrEmpty(proxy.AutoConfigUrl))
            {
                if (IsUrlReachable(proxy.AutoConfigUrl))
                {
                    Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Checked {0}: AutoConfigUrl is reachable.", proxy.Name));
                    detectedProxy = proxy;
                    waitHandle.Set();
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(proxy.Host))
            {
                if (IsHostReachable(proxy.Host, ProxyConfiguration.Instance.PingTimeout))
                {
                    Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Checked {0}: Host is reachable.", proxy.Name));
                    detectedProxy = proxy;
                    waitHandle.Set();
                    return;
                }
            }

            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Checked {0}: Proxy is not reachable.", proxy.Name));
        }

        /// <summary>
        /// Determines whether the specified url is reachable.
        /// </summary>
        /// <param name="utl">The url to attempt to download.</param>
        /// <returns><see langword="true"/> if the specified url is reachable; otherwise, <see langword="false"/>.</returns>
        private static bool IsUrlReachable(string url)
        {
            bool reachable = false;
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Proxy = null;
                    client.UseDefaultCredentials = false;
                    client.DownloadString(url);
                    reachable = true;
                }
            }
            // Ignore exceptions that can arise from the WebClient.DownloadString method.
            catch (WebException) { }
            catch (NotSupportedException) { }
            return reachable;
        }

        /// <summary>
        /// Determines whether the specified host is reachable.
        /// </summary>
        /// <param name="host">The host to check.</param>
        /// <param name="pingTimeout">The ping timeout in milliseconds.</param>
        /// <returns><see langword="true"/> if the specified host is reachable; otherwise, <see langword="false"/>.</returns>
        private static bool IsHostReachable(string host, int pingTimeout)
        {
            bool reachable = false;
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send(host, pingTimeout);
                if (reply.Status == IPStatus.Success)
                {
                    reachable = true;
                }
            }
            // Ignore exceptions that can arise from the Ping.Send method.
            catch (ArgumentNullException) { }
            catch (ArgumentOutOfRangeException) { }
            catch (InvalidOperationException) { }
            catch (SocketException) { }

            return reachable;
        }

        #endregion

        #region Set Proxy Server

        /// <summary>
        /// Sets the proxy server.
        /// </summary>
        /// <param name="proxy">The proxy server to set.</param>
        public static void SetProxyServer(ProxyServerElement proxy)
        {
            int proxyEnable = 0;
            string proxyServer = "";
            string proxyOverride = "";
            string proxyAutoConfigURL = "";

            if (proxy != null)
            {
                if (!string.IsNullOrEmpty(proxy.Host))
                {
                    proxyEnable = 1;
                    proxyServer = proxy.Host + ":" + proxy.Port;
                }
                if (!string.IsNullOrEmpty(proxy.AutoConfigUrl))
                {
                    proxyAutoConfigURL = proxy.AutoConfigUrl;
                }
                proxyOverride = (proxy.BypassForLocalAddresses ? "<local>" : "127.0.0.1");
                if (!string.IsNullOrEmpty(proxy.BypassList))
                {
                    proxyOverride = proxy.BypassList + ";" + proxyOverride;
                }
                Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Setting proxy server {0}: Enable={1}, Server={2}, Override={3}, AutoConfigURL={4}", proxy.Name, proxyEnable, proxyServer, proxyOverride, proxyAutoConfigURL));
            }
            else
            {
                Trace.WriteLine("Disabling proxy server");
            }

            using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
            {
                regKey.SetValue("ProxyEnable", proxyEnable, RegistryValueKind.DWord);
                regKey.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                regKey.SetValue("ProxyOverride", proxyOverride, RegistryValueKind.String);
                regKey.SetValue("AutoConfigURL", proxyAutoConfigURL, RegistryValueKind.String);
            }

            // Notify other applications that the internet settings have changed.
            NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            NativeMethods.InternetSetOption(IntPtr.Zero, NativeMethods.INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);

            if (proxy != null && !string.IsNullOrEmpty(proxy.Command))
            {
                Trace.WriteLine("Executing command: " + proxy.Command);
                try
                {
                    Process.Start(proxy.Command);
                }
                catch (Exception exc)
                {
                    throw new ArgumentException("Error while executing the configured command: " + exc.Message, exc);
                }
            }
        }

        #endregion
    }
}