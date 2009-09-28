using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Threading;

using ProxyMonitor.Configuration;
using ProxyMonitor.Properties;

namespace ProxyMonitor
{
    /// <summary>
    /// Monitors the current proxy server to use in the notification area (system tray).
    /// </summary>
    internal sealed class TrayMonitor : IDisposable
    {
        #region Fields

        /// <summary>
        /// Info required to set a proxy.  It's attached to menu items as the tag value.
        /// </summary>
        private class ProxyMenuInfo
        {
            public ProxyServerElement proxy;
            public ConnectionElement connection;

            public ProxyMenuInfo(ProxyServerElement p, ConnectionElement c)
            {
                proxy = p;
                connection = c;
            }
        };

        /// <summary>
        /// List of menu items that represent proxies that can be set, regardless of where they are in the menu hierarchy.
        /// </summary>
        /// <remarks>
        /// DEH: Before connections were added, all items were at the same level in the menu, which made it easy to iterate over them.  
        /// Now that the menu can be hierarchically structured with proxies under connections, that method is cumbersome.  So instead, 
        /// this list tracks all of the proxy menu items to make it easy to iterate over them.
        /// </remarks>
        private List<MenuItem> proxyMenuList = new List<MenuItem>();

        /// <summary>
        /// The notify icon to use.
        /// </summary>
        private NotifyIcon notifyIcon;

        /// <summary>
        /// The context menu to use.
        /// </summary>
        private ContextMenu contextMenu;

        /// <summary>
        /// The menu item containing the proxy servers.
        /// </summary>
        private MenuItem proxiesMenu;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Monitor"/> class.
        /// </summary>
        /// <param name="detectAndQuit">Determines if the application should detect the proxy and quit immediately.</param>
        public TrayMonitor(bool detectAndQuit)
        {
            ConnectionElementCollection connections = ProxyConfiguration.Instance.Connections;

            this.notifyIcon = new NotifyIcon();

            if (detectAndQuit)
            {
                // Exit the application when the balloon is closed or clicked.
                this.notifyIcon.BalloonTipClosed += delegate
                {
                    Exit();
                };
                this.notifyIcon.BalloonTipClicked += delegate
                {
                    Exit();
                };
            }

            contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("&About...", delegate
            {
                ShowAbout();
            });
            contextMenu.MenuItems.Add("-");
            proxiesMenu = contextMenu.MenuItems.Add("&Set Proxy");

            MenuItem proxyMenu = proxiesMenu.MenuItems.Add("None");
            proxyMenu.Tag = new ProxyMenuInfo(null, null);      // None = null proxy, null connection
            proxyMenu.Click += new EventHandler(ProxyMenu_Click);
            proxyMenuList.Add(proxyMenu);

            proxiesMenu.MenuItems.Add("-");

            // DEH: If no connections, then setup menu as before.  
            //      If connections, create menu options for each connection, with their proxies nested below them.
            if (connections.Count == 0)
            {
                foreach (ProxyServerElement configuredProxy in ProxyDetector.ConfiguredProxyServers)
                {
                    proxyMenu = proxiesMenu.MenuItems.Add(configuredProxy.Name);
                    proxyMenu.Tag = new ProxyMenuInfo(configuredProxy, null);
                    proxyMenu.Click += new EventHandler(ProxyMenu_Click);
                    proxyMenuList.Add(proxyMenu);
                }
            }
            else
            {
                // Loop through connections and create nested menu structure
                foreach (ConnectionElement connection in connections)
                {
                    MenuItem connectionMenu = proxiesMenu.MenuItems.Add(connection.Name);

                    foreach (ProxyServerElement configuredProxy in connection.ProxyServers)
                    {
                        proxyMenu = connectionMenu.MenuItems.Add(configuredProxy.Name);
                        proxyMenu.Tag = new ProxyMenuInfo(configuredProxy, connection);
                        proxyMenu.Click += new EventHandler(ProxyMenu_Click);
                        proxyMenuList.Add(proxyMenu);
                    }
                }
            }


            contextMenu.MenuItems.Add("&Detect Proxy", delegate
            {
                DetectProxy();
            });
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("E&xit", delegate
            {
                Exit();
            });

            this.notifyIcon.ContextMenu = contextMenu;
            this.notifyIcon.Icon = Resources.ProxyOff;
            this.notifyIcon.Visible = true;

            NetworkChange.NetworkAddressChanged += delegate
            {
                DetectProxy();
            };
        }

        #endregion

        #region Exit

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void Exit()
        {
            this.notifyIcon.Dispose();
            Application.Exit();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Shows the about screen.
        /// </summary>
        internal static void ShowAbout()
        {
            StringBuilder aboutText = new StringBuilder();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            aboutText.AppendLine("Proxy Monitor v" + version.ToString(2));
            aboutText.AppendLine("http://proxymonitor.codeplex.com").AppendLine();
            aboutText.AppendLine("Monitors the network and detects the proxy server to use.").AppendLine();
            aboutText.AppendLine("Usage: ProxyMonitor.exe [/detect]");
            aboutText.AppendLine("     /detect: auto-detect the proxy and exit immediately");
            MessageBox.Show(aboutText.ToString(), "About Proxy Monitor...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Handles the Click event of the ProxyMenu control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
        void ProxyMenu_Click(object sender, EventArgs e)
        {
            MenuItem selectedProxyMenu = (MenuItem)sender;
            ProxyMenuInfo pmi = (ProxyMenuInfo)selectedProxyMenu.Tag;
            ProxyDetector.SetProxyServer(pmi.proxy, pmi.connection);
            ShowSelectedProxy(pmi.proxy, pmi.connection);
        }

        #endregion

        #region Detect Proxy

        /// <summary>
        /// Detects the proxy.
        /// </summary>
        public void DetectProxy()
        {
            ConnectionElementCollection connections = ProxyConfiguration.Instance.Connections;
            ProxyServerElement found_proxy;

            if (connections.Count == 0)
            {
                // No connections, so detect using pre-connection method.

                found_proxy = ProxyDetector.DetectProxyForConnection((ConnectionElement)null);
                ShowSelectedProxy(found_proxy, (ConnectionElement)null);
            }
            else
            {
                // Loop through all connections, and detect the proxy for each
                foreach (ConnectionElement connection in connections)
                {
                    try
                    {
                        int detectionDelay = Int32.Parse(connection.DetectionDelay);

                        if (detectionDelay > 0)
                        {
                            Thread.Sleep(detectionDelay);
                        }
                    }
                    catch (FormatException)
                    {
                        // Ignore.  A FormatException is caused by a detection delay value that is not a valid integer
                    }

                    found_proxy = ProxyDetector.DetectProxyForConnection(connection);
                    ShowSelectedProxy(found_proxy, connection);
                }
            }
        }


        /// <summary>
        /// Shows the selected proxy.
        /// </summary>
        /// <param name="proxy">The proxy.</param>
        /// <param name="connection">The connection, or null if no connection specified in config</param>
        private void ShowSelectedProxy(ProxyServerElement proxy, ConnectionElement connection)
        {
            // Set the tray icon and a message.
            string message = null;
            if (proxy == null)
            {
                if (connection == null)
                {
                    message = "No proxy set";
                }
                else
                {
                    message = connection.Name + ": No proxy set";
                }
                this.notifyIcon.Icon = Resources.ProxyOff;
            }
            else
            {
                if (connection == null)
                {
                    message = "Current proxy: " + proxy.Name;
                }
                else
                {
                    message = connection.Name + " proxy: " + proxy.Name;
                }
                this.notifyIcon.Icon = Resources.ProxyOn;
            }

            // Select the corresponding menu item.
            foreach (MenuItem proxyMenu in proxyMenuList)
            {
                ProxyMenuInfo pmi = (ProxyMenuInfo)proxyMenu.Tag;
                if (pmi.connection == connection)
                {
                    proxyMenu.Checked = (pmi.proxy == proxy);
                }
            }

            this.notifyIcon.Text = message;

            // Show a balloon message if allowed.
            if (!ProxyConfiguration.Instance.DisableNotifications)
            {
                this.notifyIcon.ShowBalloonTip(10, "Proxy Monitor", message, ToolTipIcon.Info);
                Thread.Sleep(2000);  // This ensures that the ballon has a chance to be read before the next proxy is detected
            }
        }


        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // DEH: TODO I think I need to update this to dispose of the nested menu structures...
            if (this.contextMenu != null)
            {
                this.contextMenu.Dispose();
                this.contextMenu = null;
            }
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Dispose();
                this.notifyIcon = null;
            }
        }

        #endregion
    }
}