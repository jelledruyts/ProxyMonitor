using System;
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
            proxyMenu.Click += new EventHandler(ProxyMenu_Click);
            proxiesMenu.MenuItems.Add("-");
            foreach (ProxyServerElement configuredProxy in ProxyDetector.ConfiguredProxyServers)
            {
                proxyMenu = proxiesMenu.MenuItems.Add(configuredProxy.Name);
                proxyMenu.Tag = configuredProxy;
                proxyMenu.Click += new EventHandler(ProxyMenu_Click);
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
        private void ProxyMenu_Click(object sender, EventArgs e)
        {
            MenuItem selectedProxyMenu = (MenuItem)sender;
            ProxyServerElement proxy = (ProxyServerElement)selectedProxyMenu.Tag;
            ProxyDetector.SetProxyServer(proxy);
            ShowSelectedProxy(proxy);
        }

        #endregion

        #region Detect Proxy

        /// <summary>
        /// Detects the proxy.
        /// </summary>
        public void DetectProxy()
        {
            this.notifyIcon.Text = "Detecting proxy...";
            ProxyServerElement proxy = ProxyDetector.DetectProxyServer();
            ShowSelectedProxy(proxy);
        }

        /// <summary>
        /// Shows the selected proxy.
        /// </summary>
        /// <param name="proxy">The proxy.</param>
        private void ShowSelectedProxy(ProxyServerElement proxy)
        {
            // Set the tray icon and a message.
            string message = null;
            if (proxy == null)
            {
                message = "No proxy set";
                this.notifyIcon.Icon = Resources.ProxyOff;
            }
            else
            {
                message = "Current proxy: " + proxy.Name;
                this.notifyIcon.Icon = Resources.ProxyOn;
            }

            // Select the corresponding menu item.
            foreach (MenuItem proxyMenu in this.proxiesMenu.MenuItems)
            {
                proxyMenu.Checked = (proxyMenu.Tag == proxy);
            }

            this.notifyIcon.Text = message;

            // Show a balloon message if allowed.
            if (!ProxyConfiguration.Instance.DisableNotifications)
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