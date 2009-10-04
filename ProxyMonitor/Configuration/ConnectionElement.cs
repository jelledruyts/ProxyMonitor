using System.Configuration;

namespace ProxyMonitor.Configuration
{
    /// <summary>
    /// Defines a network connection for which proxies can be set.
    /// </summary>
    public class ConnectionElement : ConfigurationElement
    {
        #region Constants

        private const string NamePropertyName = "name";
        private const string DetectionDelayPropertyName = "detectionDelay";
        private const string ProxyServersPropertyName = "proxyServers";
        private const string SkipAutoDetectPropertyName = "skipAutoDetect";

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the name of the connection.
        /// </summary>
        [ConfigurationProperty(NamePropertyName, IsRequired = true)]
        public string Name
        {
            get
            {
                return (string)base[NamePropertyName];
            }
            set
            {
                base[NamePropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the detection delay in milliseconds of this connection.  This is the amount of time to wait prior to detecting proxies for this connection.
        /// </summary>
        /// <remarks>
        /// This is a mechanism used to ensure that the proxy detection starts only _after_ some other event also triggered by the connection becoming
        /// active.  For example, my IT dept auto-runs a proxy-setting script after a VPN connection is established.  Unfortunately, the 
        /// proxy setting doesn't work for me.  By setting the detectionDelay, I can ensure that the detection only occurs _after_ the automated
        /// script has run its course.
        /// </remarks>
        [ConfigurationProperty(DetectionDelayPropertyName, IsRequired = false, DefaultValue = 0)]
        public int DetectionDelay
        {
            get
            {
                return (int)base[DetectionDelayPropertyName];
            }
            set
            {
                base[DetectionDelayPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the proxy servers defined for this connection.
        /// </summary>
        [ConfigurationProperty(ProxyServersPropertyName, IsDefaultCollection = true, IsRequired = true)]
        public ProxyServerElementCollection ProxyServers
        {
            get
            {
                return (ProxyServerElementCollection)base[ProxyServersPropertyName];
            }
            private set
            {
                base[ProxyServersPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that determines if this proxy server should not be auto-detected.
        /// </summary>
        [ConfigurationProperty(SkipAutoDetectPropertyName, IsRequired = false, DefaultValue = false)]
        public bool SkipAutoDetect
        {
            get
            {
                return (bool)base[SkipAutoDetectPropertyName];
            }
            set
            {
                base[SkipAutoDetectPropertyName] = value;
            }
        }

        #endregion

        #region Convenience Properties For LAN Connection

        /// <summary>
        /// Gets or sets a value that determines whether this instance represents the LAN connection.
        /// </summary>
        public bool IsLanConnection { get; private set; }

        #endregion

        #region Convenience Properties For Selected Proxy

        /// <summary>
        /// Gets or sets the currently selected proxy server for this connection.
        /// </summary>
        public ProxyServerElement SelectedProxyServer { get; set; }

        #endregion

        #region constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionElement"/> class.
        /// </summary>
        /// <remarks>
        /// This default constructor is necessary for the configuration system.
        /// </remarks>
        public ConnectionElement()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionElement"/> class.
        /// </summary>
        /// <param name="name">The name of the connection.</param>
        /// <param name="detectionDelay">The detection delay in milliseconds of this connection.</param>
        /// <param name="proxyServers">The proxy servers defined for this connection.</param>
        /// <param name="isLanConnection">Determines whether this instance represents the LAN connection.</param>
        public ConnectionElement(string name, int detectionDelay, ProxyServerElementCollection proxyServers, bool isLanConnection)
        {
            this.Name = name;
            this.DetectionDelay = detectionDelay;
            this.ProxyServers = proxyServers;
            this.IsLanConnection = isLanConnection;
        }

        #endregion
    }
}