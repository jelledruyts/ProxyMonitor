using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
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

        /// <summary>
        /// Used to execute the proxy server detection on a background thread.
        /// </summary>
        private BackgroundWorker backgroundWorker;

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

            contextMenu.MenuItems.Add("&Detect Proxy", DetectProxyServersRequestedByUser);
            contextMenu.MenuItems.Add("-");
            contextMenu.MenuItems.Add("E&xit", ExitRequested);

            this.notifyIcon.ContextMenu = contextMenu;
            this.notifyIcon.Icon = Resources.ProxyOff;
            this.notifyIcon.Visible = true;

            this.backgroundWorker = new BackgroundWorker();
            this.backgroundWorker.DoWork += new DoWorkEventHandler(worker_DoWork);
            this.backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);

            NetworkChange.NetworkAddressChanged += DetectProxyServersAfterNetworkChanged;
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
            aboutText.AppendLine("Proxy Monitor v" + Program.ApplicationVersion.ToString(2));
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

            if (proxyServer == null)
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "User manually disabled proxy server for connection \"{0}\".", connection.Name), TraceEventType.Information);
            }
            else
            {
                Logger.LogMessage(string.Format(CultureInfo.CurrentCulture, "User manually selected proxy server \"{0}\" for connection \"{1}\".", proxyServer.Name, connection.Name), TraceEventType.Information);
            }

            // Set the connection's selected proxy server and apply.
            connection.SelectedProxyServer = proxyServer;
            ProxyDetector.ApplySelectedProxyServer(connection);

            // Update the menu items and show a balloon.
            ShowSelectedProxies(true);
        }

        #endregion

        #region Detect Proxy

        /// <summary>
        /// Called when a proxy detection is requested by the user.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DetectProxyServersRequestedByUser(object sender, EventArgs e)
        {
            Logger.LogMessage("User requested to detect proxy servers.", TraceEventType.Information);
            DetectProxyServers(true);
        }

        /// <summary>
        /// Called when a proxy detection is triggered by a network change event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DetectProxyServersAfterNetworkChanged(object sender, EventArgs e)
        {
            Logger.LogMessage("Network address changed.", TraceEventType.Information);
            DetectProxyServers(false);
        }

        /// <summary>
        /// Detects the proxy servers.
        /// </summary>
        /// <returns><see langword="true"/> if the detection was requested by the user, <see langword="false"/> otherwise.</returns>
        internal void DetectProxyServers(bool userRequested)
        {
            if (!this.backgroundWorker.IsBusy)
            {
                this.notifyIcon.Text = "Detecting Proxy...";
                this.backgroundWorker.RunWorkerAsync(userRequested);
            }
        }

        /// <summary>
        /// Handles the DoWork event of the worker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Show a message if the user requested the detection or if a proxy has changed.
            bool userRequested = (bool)e.Argument;
            bool hasAnyProxyChanged = ProxyDetector.DetectProxyServers();
            bool showBalloonMessage = userRequested || hasAnyProxyChanged;
            e.Result = showBalloonMessage;
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event of the worker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            bool showBalloonMessage = (bool)e.Result;
            ShowSelectedProxies(showBalloonMessage);
        }

        #endregion

        #region ShowSelectedProxies

        /// <summary>
        /// Shows the selected proxy servers.
        /// </summary>
        private void ShowSelectedProxies(bool showBalloonMessage)
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
            StringBuilder messageBuilder = new StringBuilder();
            foreach (ConnectionElement connection in ProxyConfiguration.Instance.AllConnections)
            {
                string proxyServerName = "No proxy set";
                if (connection.SelectedProxyServer != null)
                {
                    isAnyProxyEnabled = true;
                    proxyServerName = connection.SelectedProxyServer.Name;
                }
                messageBuilder.AppendFormat("{0}: {1}", connection.Name, proxyServerName).AppendLine();
            }
            string message = messageBuilder.ToString();

            // Set up the notify icon and show a balloon message if allowed.
            this.notifyIcon.Icon = (isAnyProxyEnabled ? Resources.ProxyOn : Resources.ProxyOff);
            string notificationMessage = message.Trim();
            if (notificationMessage.Length > 63)
            {
                // The text of a notification icon must be less than 64 characters.
                notificationMessage = notificationMessage.Substring(0, 60) + "...";
            }
            this.notifyIcon.Text = notificationMessage;
            if (showBalloonMessage && !ProxyConfiguration.Instance.DisableNotifications)
            {
                this.notifyIcon.ShowBalloonTip(10, "Proxy Monitor", message, ToolTipIcon.Info);
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
            if (this.backgroundWorker != null)
            {
                this.backgroundWorker.Dispose();
                this.backgroundWorker = null;
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