using System;

namespace Kibinago.Services
{
    /// <summary>
    /// Minimal registration surface for the built-in service provider. Resolution is the BCL
    /// <see cref="IServiceProvider"/>; this is the matching "register" half (Prism's
    /// <c>IContainerRegistry</c> role, kept deliberately small).
    /// </summary>
    public interface IServiceRegistry
    {
        /// <summary>Registers a ready-made singleton instance for <typeparamref name="TService"/>.</summary>
        void RegisterInstance<TService>(TService instance);

        /// <summary>
        /// Registers a singleton for <typeparamref name="TService"/> created by <paramref name="factory"/>
        /// on first resolution; every later resolution returns that same instance (the factory runs
        /// once). The factory receives the provider so it can resolve its own dependencies.
        /// </summary>
        void RegisterSingleton<TService>(Func<IServiceProvider, TService> factory);

        /// <summary>Whether <paramref name="serviceType"/> has a registration.</summary>
        bool IsRegistered(Type serviceType);
    }
}
