using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using ProxyMonitor.Configuration;

namespace ProxyMonitor
{
    /// <summary>
    /// Detects and sets the current proxy server to use.
    /// </summary>
    internal static class ProxyDetector
    {
        #region Fields & Properties

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
        /// <param name="proxy">The proxy to check.</param>
        private delegate void CheckProxyDelegate(ProxyServerElement proxy);

        /// <summary>
        /// The wait handle that signals when a proxy server is detected.
        /// </summary>
        private static AutoResetEvent waitHandle;

        #endregion

        #region Detect Proxy Servers

        /// <summary>
        /// Detects the proxy servers for all connections and sets them as the connection's <see cref="ConnectionElement.SelectedProxyServer"/>.
        /// </summary>
        /// <returns>The proxy server that was detected for each connection.</returns>
        public static void DetectProxyServers()
        {
            // Loop through all connections, and detect the proxy for each.
            foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
            {
                if (!connection.SkipAutoDetect)
                {
                    if (connection.DetectionDelay > 0)
                    {
                        Log(string.Format(CultureInfo.CurrentCulture, "Waiting {0} milliseconds before detecting proxy on connection \"{1}\"...", connection.DetectionDelay, connection.Name));
                        Thread.Sleep(connection.DetectionDelay);
                    }

                    ProxyDetector.DetectProxy(connection);
                }
            }
        }

        /// <summary>
        /// Detects the proxy for a particular connection.
        /// </summary>
        private static void DetectProxy(ConnectionElement connection)
        {
            lock (lockObject)
            {
                Log(string.Format(CultureInfo.CurrentCulture, "Detecting proxy to use on connection \"{0}\"...", connection.Name));
                detectedProxy = null;
                waitHandle = new AutoResetEvent(false);

                // Queue async operations to detect the proxy.
                List<IAsyncResult> results = new List<IAsyncResult>();
                foreach (ProxyServerElement configuredProxy in connection.ProxyServers)
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
                        Log("Wait handle was signaled");
                        keepWaiting = false;
                    }
                    else
                    {
                        // Wait for all of the async operations to complete.
                        Log("Wait handle was not signaled");
                        keepWaiting = false;
                        foreach (IAsyncResult result in results)
                        {
                            Log(" - Async operation completed? " + result.IsCompleted);
                            if (!result.IsCompleted)
                            {
                                keepWaiting = true;
                                break;
                            }
                        }
                    }
                }

                connection.SelectedProxyServer = detectedProxy;
                ApplySelectedProxyServer(connection);
            }
        }

        /// <summary>
        /// Checks if the given proxy is reachable.
        /// </summary>
        /// <param name="proxy">The proxy to check.</param>
        private static void CheckProxy(ProxyServerElement proxy)
        {
            if (!string.IsNullOrEmpty(proxy.AutoConfigUrl))
            {
                Log(string.Format(CultureInfo.CurrentCulture, "Checking \"{0}\": Downloading AutoConfigUrl.", proxy.Name));
                if (IsUrlReachable(proxy.AutoConfigUrl))
                {
                    Log(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": AutoConfigUrl is reachable.", proxy.Name));
                    detectedProxy = proxy;
                    waitHandle.Set();
                    return;
                }
            }

            if (!string.IsNullOrEmpty(proxy.Host))
            {
                Log(string.Format(CultureInfo.CurrentCulture, "Checking \"{0}\": Pinging Host.", proxy.Name));
                if (IsHostReachable(proxy.Host, ProxyConfiguration.Instance.PingTimeout))
                {
                    Log(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": Host is reachable.", proxy.Name));
                    detectedProxy = proxy;
                    waitHandle.Set();
                    return;
                }
            }

            Log(string.Format(CultureInfo.CurrentCulture, "Checked \"{0}\": Proxy is not reachable.", proxy.Name));
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
                Log(string.Format(CultureInfo.CurrentCulture, "Setting proxy server \"{0}\" for connection \"{1}\": Server=\"{2}\", Override=\"{3}\", AutoConfigURL=\"{4}\"", proxy.Name, connection.Name, proxyServer, proxyOverride, proxyAutoConfigURL));
            }
            else
            {
                Log(string.Format(CultureInfo.CurrentCulture, "Disabling proxy server for connection \"{0}\"", connection.Name));
            }

            NativeMethods.SetProxyInfo(connectionName, proxyServer, proxyOverride, proxyAutoConfigURL);

            if (proxy != null && !string.IsNullOrEmpty(proxy.Command))
            {
                Log("Executing command: " + proxy.Command);
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

        #region Log

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private static void Log(string message)
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "{0} - {1}", DateTime.Now.ToLongTimeString(), message));
        }

        #endregion
    }
}