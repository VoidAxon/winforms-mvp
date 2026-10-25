using System;
using System.Windows.Forms;

namespace Kibinago.Services
{
    /// <summary>
    /// Manages the mapping between View interfaces and their Form implementations.
    /// Used by WindowNavigator to automatically create View instances.
    /// </summary>
    public interface IViewMappingRegister
    {
        /// <summary>
        /// Gets the implementation type corresponding to a View interface.
        /// </summary>
        /// <param name="viewInterfaceType">View interface type</param>
        /// <returns>Implementation Form type</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when no corresponding implementation type is found</exception>
        Type GetViewImplementationType(Type viewInterfaceType);

        /// <summary>
        /// Registers a mapping between a View interface and its Form implementation (type-based).
        /// </summary>
        /// <typeparam name="TViewInterface">View interface type</typeparam>
        /// <typeparam name="TViewImplementation">Implementation Form type</typeparam>
        /// <param name="allowOverride">Whether to override existing mappings (default: false)</param>
        /// <exception cref="InvalidOperationException">Thrown when already registered (when allowOverride = false)</exception>
        void Register<TViewInterface, TViewImplementation>(bool allowOverride = false)
            where TViewImplementation : Form, TViewInterface;

        /// <summary>
        /// Registers a mapping between a View interface and its Form implementation (factory method).
        /// Use this for Views that require constructor parameters or special initialization.
        /// </summary>
        /// <typeparam name="TViewInterface">View interface type</typeparam>
        /// <param name="factory">Factory method to create View instances</param>
        /// <param name="allowOverride">Whether to override existing mappings (default: false)</param>
        /// <exception cref="InvalidOperationException">Thrown when already registered (when allowOverride = false)</exception>
        void Register<TViewInterface>(Func<TViewInterface> factory, bool allowOverride = false)
            where TViewInterface : class;

        /// <summary>
        /// Creates an implementation instance corresponding to a View interface.
        /// </summary>
        /// <param name="viewInterfaceType">View interface type</param>
        /// <returns>Created View instance</returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">Thrown when no corresponding implementation type is found</exception>
        object CreateInstance(Type viewInterfaceType);

        /// <summary>
        /// Checks whether a specified View interface is registered.
        /// </summary>
        /// <param name="viewInterfaceType">View interface type</param>
        /// <returns>True if registered</returns>
        bool IsRegistered(Type viewInterfaceType);
    }
}
