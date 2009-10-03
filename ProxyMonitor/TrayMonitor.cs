using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
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
        /// List of menu items that represent proxies that can be set, regardless of where they are in the menu hierarchy.
        /// </summary>
        /// <remarks>
        /// Before connections were added, all items were at the same level in the menu, which made it easy to iterate over them.  
        /// Since the menu can be hierarchically structured with proxies under connections, that method is cumbersome.  So instead, 
        /// this list tracks all of the proxy menu items to make it easy to iterate over them.
        /// </remarks>
        private IList<MenuItem> proxyMenuList = new List<MenuItem>();

        /// <summary>
        /// The notify icon to use.
        /// </summary>
        private NotifyIcon notifyIcon;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Monitor"/> class.
        /// </summary>
        /// <param name="detectAndQuit">Determines if the application should detect the proxy and quit immediately.</param>
        public TrayMonitor(bool detectAndQuit)
        {
            this.notifyIcon = new NotifyIcon();

            if (detectAndQuit)
            {
                // Exit the application when the balloon is closed or clicked.
                this.notifyIcon.BalloonTipClosed += ExitRequested;
                this.notifyIcon.BalloonTipClicked += ExitRequested;
            }

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add("&About...", delegate { ShowAbout(); });
            contextMenu.MenuItems.Add("-");
            MenuItem proxiesMenu = contextMenu.MenuItems.Add("&Set Proxy");

            // Loop through connections and create the menu structure.
            // Nest the menus in a separate menu only if there are multiple connections.
            bool nestedMenus = ProxyConfiguration.Instance.AllConnections.Count > 1;
            foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
            {
                CreateProxyServerMenuItems(proxiesMenu, connection, nestedMenus);
            }

            contextMenu.MenuItems.Add("&Detect Proxy", DetectProxyServersRequested);
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("E&xit", ExitRequested);

            this.notifyIcon.ContextMenu = contextMenu;
            this.notifyIcon.Icon = Resources.ProxyOff;
            this.notifyIcon.Visible = true;

            NetworkChange.NetworkAddressChanged += DetectProxyServersRequested;
        }

        #endregion

        #region Exit

        /// <summary>
        /// Called when application exit is requested.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void ExitRequested(object sender, EventArgs e)
        {
            Exit();
        }

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

        private void CreateProxyServerMenuItems(MenuItem parentMenu, ConnectionElement connection, bool createConnectionMenu)
        {
            // Create a container menu if needed.
            MenuItem rootMenu = parentMenu;
            if (createConnectionMenu)
            {
                rootMenu = parentMenu.MenuItems.Add(connection.Name);
            }

            // Always add a menu item to clear the proxy.
            ProxyServerMenuItem proxyMenu = new ProxyServerMenuItem(connection, null);
            proxyMenu.Click += new EventHandler(ProxyMenu_Click);
            proxyMenu.Checked = true;
            proxyMenuList.Add(proxyMenu);
            rootMenu.MenuItems.Add(proxyMenu);

            // Loop through regular proxy servers and create menu structure.
            if (connection.ProxyServers.Count > 0)
            {
                rootMenu.MenuItems.Add("-");
            }
            foreach (ProxyServerElement configuredProxy in connection.ProxyServers)
            {
                proxyMenu = new ProxyServerMenuItem(connection, configuredProxy);
                proxyMenu.Click += new EventHandler(ProxyMenu_Click);
                proxyMenuList.Add(proxyMenu);
                rootMenu.MenuItems.Add(proxyMenu);
            }
        }

        /// <summary>
        /// Handles the Click event of the ProxyMenu control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
        private void ProxyMenu_Click(object sender, EventArgs e)
        {
            // Retrieve the connection and proxy server of the menu that was clicked.
            ProxyServerMenuItem selectedProxyMenu = (ProxyServerMenuItem)sender;
            ConnectionElement connection = selectedProxyMenu.Connection;
            ProxyServerElement proxyServer = selectedProxyMenu.ProxyServer;

            // Set the connection's selected proxy server and apply.
            connection.SelectedProxyServer = proxyServer;
            ProxyDetector.ApplySelectedProxyServer(connection);

            // Update the menu items and show a balloon.
            ShowSelectedProxies();
        }

        #endregion

        #region Detect Proxy

        /// <summary>
        /// Called when a proxy detection is requested.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DetectProxyServersRequested(object sender, EventArgs e)
        {
            DetectProxyServers();
        }

        /// <summary>
        /// Detects the proxy servers.
        /// </summary>
        internal void DetectProxyServers()
        {
            this.notifyIcon.Text = "Detecting Proxy...";
            ProxyDetector.DetectProxyServers();
            ShowSelectedProxies();
        }

        /// <summary>
        /// Shows the selected proxy servers.
        /// </summary>
        private void ShowSelectedProxies()
        {
            // Select the corresponding menu items.
            foreach (ProxyServerMenuItem proxyMenu in proxyMenuList)
            {
                foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
                {
                    if (proxyMenu.Connection == connection)
                    {
                        proxyMenu.Checked = (proxyMenu.ProxyServer == connection.SelectedProxyServer);
                    }
                }
            }

            // Create a message to show which proxy is selected for each connection.
            bool isAnyProxyEnabled = false;
            StringBuilder message = new StringBuilder();
            foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
            {
                string proxyServerName = "No proxy set";
                if (connection.SelectedProxyServer != null)
                {
                    isAnyProxyEnabled = true;
                    proxyServerName = connection.SelectedProxyServer.Name;
                }
                message.AppendFormat("{0}: {1}", connection.Name, proxyServerName).AppendLine();
            }

            // Set up the notify icon and show a balloon message if allowed.
            this.notifyIcon.Icon = (isAnyProxyEnabled ? Resources.ProxyOn : Resources.ProxyOff);
            string notificationMessage = message.ToString().Trim();
            if (notificationMessage.Length > 63)
            {
                // The text of a notification icon must be less than 64 characters.
                notificationMessage = notificationMessage.Substring(0, 60) + "...";
            }
            this.notifyIcon.Text = notificationMessage;
            if (!ProxyConfiguration.Instance.DisableNotifications)
            {
                this.notifyIcon.ShowBalloonTip(10, "Proxy Monitor", message.ToString(), ToolTipIcon.Info);
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Dispose();
                this.notifyIcon = null;
            }
        }

        #endregion

        #region ProxyServerMenuItem Class

        /// <summary>
        /// Represents a menu item that contains a connection and a proxy server that can be selected.
        /// </summary>
        private class ProxyServerMenuItem : MenuItem
        {
            /// <summary>
            /// Gets the connection.
            /// </summary>
            public ConnectionElement Connection { get; private set; }

            /// <summary>
            /// The proxy server that can be selected for the connection.
            /// </summary>
            public ProxyServerElement ProxyServer { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ProxyServerMenuItem"/> class.
            /// </summary>
            /// <param name="connection">The connection.</param>
            /// <param name="proxyServer">The proxy server that can be selected for the connection.</param>
            public ProxyServerMenuItem(ConnectionElement connection, ProxyServerElement proxyServer)
            {
                this.Connection = connection;
                this.ProxyServer = proxyServer;
                this.Text = (this.ProxyServer == null ? "None" : this.ProxyServer.Name);
            }
        }

        #endregion
    }
}