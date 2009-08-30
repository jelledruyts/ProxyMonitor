using System.Configuration;

namespace ProxyMonitor.Configuration
{
    /// <summary>
    /// Defines a proxy server.
    /// </summary>
    public class ProxyServerElement : ConfigurationElement
    {
        #region Constants

        private const string NamePropertyName = "name";
        private const string HostPropertyName = "host";
        private const string PortPropertyName = "port";
        private const string AutoConfigUrlPropertyName = "autoConfigUrl";
        private const string BypassForLocalAddressesPropertyName = "bypassForLocalAddresses";
        private const string BypassListPropertyName = "bypassList";
        private const string CommandPropertyName = "command";
        private const string SkipAutoDetectPropertyName = "skipAutoDetect";
        
        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the name of this proxy server.
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
        /// Gets or sets the host of this proxy server.
        /// </summary>
        [ConfigurationProperty(HostPropertyName, IsRequired = false)]
		public string Host
        {
            get 
            {
                return (string)base[HostPropertyName]; 
            }
            set 
            {
                base[HostPropertyName] = value; 
            }
        }

        /// <summary>
        /// Gets or sets the port of this proxy server.
        /// </summary>
        [ConfigurationProperty(PortPropertyName, IsRequired = false, DefaultValue=80)]
        public int Port
        {
            get
            {
                return (int)base[PortPropertyName];
            }
            set
            {
                base[PortPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the automatic configuration url of this proxy server.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings")]
        [ConfigurationProperty(AutoConfigUrlPropertyName, IsRequired = false, DefaultValue = "")]
        public string AutoConfigUrl
        {
            get
            {
                return (string)base[AutoConfigUrlPropertyName];
            }
            set
            {
                base[AutoConfigUrlPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets a value that determines if this proxy server should be bypassed for local addresses.
        /// </summary>
        [ConfigurationProperty(BypassForLocalAddressesPropertyName, IsRequired = false, DefaultValue = true)]
        public bool BypassForLocalAddresses
        {
            get
            {
                return (bool)base[BypassForLocalAddressesPropertyName];
            }
            set
            {
                base[BypassForLocalAddressesPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the semicolon-separated proxy bypass list.
        /// </summary>
        [ConfigurationProperty(BypassListPropertyName, IsRequired = false)]
        public string BypassList
        {
            get
            {
                return (string)base[BypassListPropertyName];
            }
            set
            {
                base[BypassListPropertyName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the command to be executed if the proxy is set.
        /// </summary>
        [ConfigurationProperty(CommandPropertyName, IsRequired = false)]
        public string Command
        {
            get
            {
                return (string)base[CommandPropertyName];
            }
            set
            {
                base[CommandPropertyName] = value;
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
    }
}