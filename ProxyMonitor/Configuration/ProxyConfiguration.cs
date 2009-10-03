using System.Configuration;
using System.Collections.Generic;

namespace ProxyMonitor.Configuration
{
    /// <summary>
    /// The configuration settings for the proxy detector.
    /// </summary>
    public class ProxyConfiguration : ConfigurationSection
    {
        #region Constants

        private const string ConfigurationSectionName = "proxyConfiguration";
        private const string ProxyServersPropertyName = "proxyServers";
        private const string ConnectionsPropertyName = "connections";
        private const string PingTimeoutPropertyName = "pingTimeout";
        private const string DisableNotificationsPropertyName = "disableNotifications";

        #endregion

        #region Fields

        /// <summary>
        /// Contains the cached LAN pseudo-connection.
        /// </summary>
        private ConnectionElement lanConnection;

        /// <summary>
        /// Contains all connections, including the LAN pseudo-connection if it exists.
        /// </summary>
        private ICollection<ConnectionElement> allConnections;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the number of milliseconds for the ping to timeout.
        /// </summary>
        [ConfigurationProperty(PingTimeoutPropertyName, IsRequired = false, DefaultValue = 1000)]
        public int PingTimeout
        {
            get
            {
                return (int)base[PingTimeoutPropertyName];
            }
            set
            {
                base[PingTimeoutPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that determines if notifications are enabled.
        /// </summary>
        [ConfigurationProperty(DisableNotificationsPropertyName, IsRequired = false, DefaultValue = false)]
        public bool DisableNotifications
        {
            get
            {
                return (bool)base[DisableNotificationsPropertyName];
            }
            set
            {
                base[DisableNotificationsPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets the configured proxy servers.
        /// </summary>
        [ConfigurationProperty(ProxyServersPropertyName, IsDefaultCollection = false, IsRequired = true)]
        public ProxyServerElementCollection ProxyServers
        {
            get
            {
                return (ProxyServerElementCollection)base[ProxyServersPropertyName];
            }
        }

        /// <summary>
        /// Gets the configured connections.
        /// </summary>
        [ConfigurationProperty(ConnectionsPropertyName, IsDefaultCollection = false, IsRequired = true)]
        public ConnectionElementCollection Connections
        {
            get
            {
                return (ConnectionElementCollection)base[ConnectionsPropertyName];
            }
        }

        #endregion

        #region Convenience Properties For LAN Pseudo-Connection

        /// <summary>
        /// Gets the LAN connection, which is the pseudo-connection holding all the root proxy servers.
        /// </summary>
        public ConnectionElement LanConnection
        {
            get
            {
                if (this.lanConnection == null && this.ProxyServers.Count > 0)
                {
                    this.lanConnection = new ConnectionElement("LAN", 0, this.ProxyServers, true);
                }
                return this.lanConnection;
            }
        }

        /// <summary>
        /// Gets all connections, including the LAN pseudo-connection if it exists.
        /// </summary>
        public ICollection<ConnectionElement> AllConnections
        {
            get
            {
                if (this.allConnections == null)
                {
                    this.allConnections = new List<ConnectionElement>();
                    if (this.LanConnection != null)
                    {
                        this.allConnections.Add(this.LanConnection);
                    }
                    foreach (ConnectionElement connection in this.Connections)
                    {
                        this.allConnections.Add(connection);
                    }
                }
                return this.allConnections;
            }
        }

        #endregion

        #region Static Singleton

        private static ProxyConfiguration instance = ConfigurationManager.GetSection(ConfigurationSectionName) as ProxyConfiguration;

        /// <summary>
        /// Gets the configuration settings instance.
        /// </summary>
        public static ProxyConfiguration Instance
        {
            get
            {
                return instance;
            }
        }

        #endregion
    }
}