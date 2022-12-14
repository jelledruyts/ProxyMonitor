using System.Configuration;

namespace ProxyMonitor.Configuration
{
    /// <summary>
    /// Defines a collection of <see cref="ProxyServerElement"/> instances.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1010:CollectionsShouldImplementGenericInterface")]
    public class ProxyServerElementCollection : ConfigurationElementCollection
    {
        #region Constants

        private const string ProxyServerElementName = "proxyServer";

        #endregion

        #region Properties

        /// <summary>
        /// Gets the data type of the collection object.
        /// </summary>
        /// <remarks>
        /// This returns a <see cref="ConfigurationElementCollectionType.BasicMapAlternate"/>
        /// so that the items can be defined as such, without the &lt;add .../&gt; constructs.
        /// </remarks>
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMapAlternate;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Indicates whether the specified <see cref="ConfigurationElement"/> exists in the <see cref="ConfigurationElementCollection"/>.
        /// </summary>
        /// <param name="elementName">The name of the element to verify.</param>
        /// <returns><c>true</c> if the element exists in the collection; otherwise, <c>false</c>. The default is <c>false</c>.</returns>
        protected override bool IsElementName(string elementName)
        {
            return (elementName == ProxyServerElementName);
        }

        /// <summary>
        /// Gets the element key for a specified configuration element when overridden in a derived class.
        /// </summary>
        /// <param name="element">The <see cref="ConfigurationElement"/> to return the key for.</param>
        /// <returns>An <see cref="object"/> that acts as the key for the specified ConfigurationElement.</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ProxyServerElement)element).Name;
        }

        /// <summary>
        /// Creates a new <see cref="ProxyServerElement"/>.
        /// </summary>
        /// <returns>A new <see cref="ProxyServerElement"/>.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new ProxyServerElement();
        }

        #endregion
    }
}