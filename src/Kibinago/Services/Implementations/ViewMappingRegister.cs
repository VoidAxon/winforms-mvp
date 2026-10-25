using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// Standard implementation for managing mappings between View interfaces and implementation Forms.
    /// Supports both type-based mappings and factory methods.
    /// </summary>
    public class ViewMappingRegister : IViewMappingRegister
    {
        private readonly Dictionary<Type, Type> _typeMappings = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, Func<object>> _factoryMappings = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// Registers a mapping between a View interface and an implementation Form (type-based).
        /// </summary>
        public void Register<TViewInterface, TViewImplementation>(bool allowOverride = false)
            where TViewImplementation : Form, TViewInterface
        {
            var interfaceType = typeof(TViewInterface);
            var implementationType = typeof(TViewImplementation);

            // Check for existing registration
            if (!allowOverride && (_typeMappings.ContainsKey(interfaceType) || _factoryMappings.ContainsKey(interfaceType)))
            {
                throw new InvalidOperationException(
                    $"View interface {interfaceType.Name} is already registered. " +
                    $"To override, specify allowOverride = true.");
            }

            // Remove factory mapping (type-based takes priority)
            _factoryMappings.Remove(interfaceType);

            // Register type-based mapping
            _typeMappings[interfaceType] = implementationType;
        }

        /// <summary>
        /// Registers a mapping between a View interface and an implementation Form (factory method).
        /// </summary>
        public void Register<TViewInterface>(Func<TViewInterface> factory, bool allowOverride = false)
            where TViewInterface : class
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            var interfaceType = typeof(TViewInterface);

            // Check for existing registration
            if (!allowOverride && (_typeMappings.ContainsKey(interfaceType) || _factoryMappings.ContainsKey(interfaceType)))
            {
                throw new InvalidOperationException(
                    $"View interface {interfaceType.Name} is already registered. " +
                    $"To override, specify allowOverride = true.");
            }

            // Remove type-based mapping (factory takes priority)
            _typeMappings.Remove(interfaceType);

            // Register factory mapping (type-erased, stored as object)
            _factoryMappings[interfaceType] = () => factory();
        }

        /// <summary>
        /// Gets the implementation type corresponding to a View interface.
        /// Returns null if registered via factory method.
        /// </summary>
        public Type GetViewImplementationType(Type viewInterfaceType)
        {
            // Check type-based mapping
            if (_typeMappings.TryGetValue(viewInterfaceType, out Type implementationType))
            {
                return implementationType;
            }

            // For factory mapping, return null (no type information)
            if (_factoryMappings.ContainsKey(viewInterfaceType))
            {
                return null;  // Factory doesn't expose type
            }

            throw new KeyNotFoundException(
                $"Implementation type not found for View interface {viewInterfaceType.Name}. " +
                $"Verify that it is registered via Register<{viewInterfaceType.Name}, TImplementation>() at application startup.");
        }

        /// <summary>
        /// Creates an implementation instance corresponding to a View interface.
        /// </summary>
        public object CreateInstance(Type viewInterfaceType)
        {
            // Prioritize factory mapping
            if (_factoryMappings.TryGetValue(viewInterfaceType, out Func<object> factory))
            {
                return factory();
            }

            // Type-based mapping
            if (_typeMappings.TryGetValue(viewInterfaceType, out Type implementationType))
            {
                try
                {
                    return Activator.CreateInstance(implementationType);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create instance of View implementation type {implementationType.Name}. " +
                        $"Verify that a parameterless constructor exists. " +
                        $"If parameters are required, use Register<{viewInterfaceType.Name}>(factory).",
                        ex);
                }
            }

            throw new KeyNotFoundException(
                $"Implementation not found for View interface {viewInterfaceType.Name}. " +
                $"Verify that it is registered via Register() at application startup.");
        }

        /// <summary>
        /// Checks whether a specified View interface is registered.
        /// </summary>
        public bool IsRegistered(Type viewInterfaceType)
        {
            return _typeMappings.ContainsKey(viewInterfaceType) ||
                   _factoryMappings.ContainsKey(viewInterfaceType);
        }
    }
}
