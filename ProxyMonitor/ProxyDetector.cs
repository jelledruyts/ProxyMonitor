using ProxyMonitor.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace ProxyMonitor
{
    /// <summary>
    /// Detects and sets the current proxy server to use.
    /// </summary>
    internal static class ProxyDetector
    {
        #region Fields

        /// <summary>
        /// An object to lock on while detecting the proxy.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// The proxy server that was detected.
        /// </summary>
        private static ProxyServerElement detectedProxy;

        /// <summary>
        /// The wait handle that signals when a proxy server is detected.
        /// </summary>
        private static AutoResetEvent waitHandle;

        /// <summary>
        /// Represents the method that will check if a proxy is reachable.
        /// </summary>
        /// <param name="proxy">The proxy to check.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        private delegate void CheckProxyDelegate(ProxyServerElement proxy, int timeout);

        #endregion

        #region Detect Proxy Servers

        /// <summary>
        /// Detects the proxy servers for all connections and sets them as the connection's <see cref="ConnectionElement.SelectedProxyServer"/>.
        /// </summary>
        /// <returns>The proxy server that was detected for each connection.</returns>
        /// <returns><see langword="true"/> if the selected proxy was changed for any connection, <see langword="false"/> otherwise.</returns>
        public static bool DetectProxyServers()
        {
            // Loop through all connections, and detect the proxy for each.
            Logger.LogMessage("Detecting proxy servers.", TraceEventType.Verbose);
            bool hasAnyProxyChanged = false;
            foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
            {
                if (!connection.SkipAutoDetect)
                {
                    if (connection.DetectionDelay > 0)
                    {
                        Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Waiting {0} milliseconds before detecting proxy on connection \"{1}\"...", connection.DetectionDelay, connection.Name), TraceEventType.Verbose);
                        Thread.Sleep(connection.DetectionDelay);
                    }

                    bool hasCurrentProxyChanged = ProxyDetector.DetectProxy(connection);
                    if (hasCurrentProxyChanged)
                    {
                        hasAnyProxyChanged = true;
                    }
                }
            }
            return hasAnyProxyChanged;
        }

        /// <summary>
        /// Detects the proxy for a particular connection.
        /// </summary
        /// <param name="connection">The connection for which to detect the proxy server.</param>>
        /// <returns><see langword="true"/> if the selected proxy was changed for this connection, <see langword="false"/> otherwise.</returns>
        private static bool DetectProxy(ConnectionElement connection)
        {
            lock (lockObject)
            {
                waitHandle = new AutoResetEvent(false);

                // Determine the timeout value to use.
                int timeout = ProxyConfiguration.Instance.PingTimeout;
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Detecting proxy to use on connection \"{0}\" with timeout of {1} milliseconds...", connection.Name, timeout), TraceEventType.Verbose);

                ProxyDetector.detectedProxy = null;

                // Queue async operations to detect the proxy.
                List<IAsyncResult> results = new List<IAsyncResult>();
                foreach (ProxyServerElement configuredProxy in connection.ProxyServers)
                {
                    if (!configuredProxy.SkipAutoDetect)
                    {
                        CheckProxyDelegate proxyDetector = new CheckProxyDelegate(CheckProxy);
                        IAsyncResult result = proxyDetector.BeginInvoke(configuredProxy, timeout, null, proxyDetector);
                        results.Add(result);
                    }
                }

                // Wait for the results to come in.
                Stopwatch watch = new Stopwatch();
                watch.Start();
                bool keepWaiting = true;
                while (keepWaiting)
                {
                    // See if the entire operation timed out by now.
                    if (watch.ElapsedMilliseconds > timeout)
                    {
                        Logger.LogMessage("Proxy detection timed out, aborting.", TraceEventType.Verbose);
                        keepWaiting = false;
                        break;
                    }

                    // See if a proxy was found by checking the wait handle with a timeout.
                    bool signaled = waitHandle.WaitOne(250, false);
                    if (signaled)
                    {
                        // The wait handle was signaled, exit the loop.
                        keepWaiting = false;
                    }
                    else
                    {
                        // Wait for all of the async operations to complete.
                        keepWaiting = false;
                        foreach (IAsyncResult result in results)
                        {
                            if (!result.IsCompleted)
                            {
                                keepWaiting = true;
                                break;
                            }
                        }
                    }
                }

                if (ProxyDetector.detectedProxy == null)
                {
                    Logger.LogMessage("Proxy detection complete, no proxy server was found.", TraceEventType.Verbose);
                }
                else
                {
                    Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Proxy detection complete, proxy server \"{0}\" was found.", ProxyDetector.detectedProxy.Name), TraceEventType.Verbose);
                }

                bool hasCurrentProxyChanged = false;
                if (connection.SelectedProxyServer != ProxyDetector.detectedProxy)
                {
                    hasCurrentProxyChanged = true;
                    connection.SelectedProxyServer = ProxyDetector.detectedProxy;
                }

                ApplySelectedProxyServer(connection);

                return hasCurrentProxyChanged;
            }
        }

        /// <summary>
        /// Checks if the given proxy is reachable.
        /// </summary>
        /// <param name="proxy">The proxy to check.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        private static void CheckProxy(ProxyServerElement proxy, int timeout)
        {
            if (!string.IsNullOrEmpty(proxy.AutoConfigUrl))
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Checking \"{0}\": Downloading AutoConfigUrl.", proxy.Name), TraceEventType.Verbose);
                if (IsUrlReachable(proxy.AutoConfigUrl))
                {
                    Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": AutoConfigUrl is reachable.", proxy.Name), TraceEventType.Verbose);
                    ProxyDetector.detectedProxy = proxy;
                    waitHandle.Set();
                }
            }

            if (!string.IsNullOrEmpty(proxy.Host))
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Checking \"{0}\": Pinging Host.", proxy.Name), TraceEventType.Verbose);
                if (IsHostReachable(proxy.Host, timeout))
                {
                    Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": Host is reachable.", proxy.Name), TraceEventType.Verbose);
                    ProxyDetector.detectedProxy = proxy;
                    waitHandle.Set();
                }
            }

            Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": Proxy is not reachable.", proxy.Name), TraceEventType.Verbose);
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
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns><see langword="true"/> if the specified host is reachable; otherwise, <see langword="false"/>.</returns>
        private static bool IsHostReachable(string host, int timeout)
        {
            bool reachable = false;
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send(host, timeout);
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

        #region Apply Selected Proxy Server

        /// <summary>
        /// Sets the selected proxy server for the given connection.
        /// </summary>
        /// <param name="connection">The connection for which to set its <see cref="ConnectionElement.SelectedProxyServer"/>.</param>
        public static void ApplySelectedProxyServer(ConnectionElement connection)
        {
            ProxyServerElement proxy = connection.SelectedProxyServer;
            string proxyServer = "";
            string proxyOverride = "";
            string proxyAutoConfigURL = "";
            string connectionName = (connection.IsLanConnection ? null : connection.Name);
            bool autoDetectSettings = false;

            if (proxy != null)
            {
                if (!string.IsNullOrEmpty(proxy.Host))
                {
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
                autoDetectSettings = proxy.AutoDetectSettings;
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Setting proxy server \"{0}\" for connection \"{1}\": Server=\"{2}\", Override=\"{3}\", AutoConfigURL=\"{4}\", AutoDetectSettings={5}", proxy.Name, connection.Name, proxyServer, proxyOverride, proxyAutoConfigURL, autoDetectSettings), TraceEventType.Information);
            }
            else
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "Disabling proxy server for connection \"{0}\"", connection.Name), TraceEventType.Information);
            }

            NativeMethods.SetProxyInfo(connectionName, proxyServer, proxyOverride, proxyAutoConfigURL, autoDetectSettings);

            if (proxy != null && !string.IsNullOrEmpty(proxy.Command))
            {
                Logger.LogMessage("Executing command: " + proxy.Command, TraceEventType.Information);
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