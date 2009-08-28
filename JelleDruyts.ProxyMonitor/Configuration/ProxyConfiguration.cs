using System.Configuration;

namespace JelleDruyts.ProxyMonitor.Configuration
{
    /// <summary>
    /// The configuration settings for the proxy detector.
    /// </summary>
    public class ProxyConfiguration : ConfigurationSection
    {
        #region Constants

        private const string ConfigurationSectionName = "proxyConfiguration";
        private const string ProxyServersPropertyName = "proxyServers";
        private const string PingTimeoutPropertyName = "pingTimeout";
        private const string DisableNotificationsPropertyName = "disableNotifications";

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

        #endregion

        #region Methods

        /// <summary>
        /// Gets the configuration settings instance.
        /// </summary>
        public static ProxyConfiguration Instance
        {
            get
            {
                return ConfigurationManager.GetSection(ConfigurationSectionName) as ProxyConfiguration;
            }
        }

        /// <summary>
        /// Gets the configured proxy servers.
        /// </summary>
        [ConfigurationProperty(ProxyServersPropertyName, IsDefaultCollection = true, IsRequired = true)]
        public ProxyServerElementCollection ProxyServers
        {
            get
            {
                return (ProxyServerElementCollection)base[ProxyServersPropertyName];
            }
        }

        #endregion
    }
}