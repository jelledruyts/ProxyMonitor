using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

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

        #endregion

        #region Properties

        /// <summary>
        /// Name of the connection..  Value set to "LAN" if empty in the configuration file.
        /// </summary>
        [ConfigurationProperty(NamePropertyName, IsRequired = false)]
		public string Name
        {
            get
            {
                return String.IsNullOrEmpty((String)base[NamePropertyName]) ? "LAN" : (String)base[NamePropertyName];
            }
            set 
            {
                base[NamePropertyName] = value; 
            }
        }

        /// <summary>
        /// Detection delay property of this connection.  This is the amount of time to wait prior to detecting proxies for this connection.
        /// </summary>
        /// <remarks>
        /// This is a mechanism used to ensure that the proxy detection starts only _after_ some other event also triggered by the connection becoming
        /// active.  For example, my IT dept auto-runs a proxy-setting script after a VPN connection is established.  Unfortunately, the 
        /// proxy setting doesn't work for me.  By setting the detectionDelay, I can ensure that the detection only occurs _after_ the automated
        /// script has run its course.
        /// </remarks>
        [ConfigurationProperty(DetectionDelayPropertyName, IsRequired = false, DefaultValue = "0")]
        public string DetectionDelay
        {
            get
            {
                return (string)base[DetectionDelayPropertyName];
            }
            set
            {
                base[DetectionDelayPropertyName] = value;
            }
        }


        /// <summary>
        /// Container for the proxy servers defined for this connection.
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