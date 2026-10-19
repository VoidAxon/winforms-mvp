using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Minimal registration surface for the built-in service provider. Resolution is the BCL
    /// <see cref="IServiceProvider"/>; this is the matching "register" half (Prism's
    /// <c>IContainerRegistry</c> role, kept deliberately small and net40-safe).
    /// </summary>
    public interface IServiceRegistry
    {
        /// <summary>Registers a ready-made singleton instance for <typeparamref name="TService"/>.</summary>
        void RegisterInstance<TService>(TService instance);

        /// <summary>
        /// Registers a factory for <typeparamref name="TService"/>, invoked lazily on first
        /// resolution; the result is cached (singleton). The factory receives the provider so it
        /// can resolve its own dependencies.
        /// </summary>
        void RegisterFactory<TService>(Func<IServiceProvider, TService> factory);

        /// <summary>Whether <paramref name="serviceType"/> has a registration.</summary>
        bool IsRegistered(Type serviceType);
    }
}
