using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Static ambient access to the current <see cref="IServiceProvider"/> — the framework's
    /// service-resolution root (the Prism <c>ContainerLocator</c> role). The framework internals
    /// and the presenter convenience accessors resolve through <see cref="Current"/>; business
    /// code should prefer constructor injection rather than locating through this.
    /// </summary>
    /// <remarks>
    /// In this phase the default provider is empty. Phase 2 registers the framework built-ins
    /// (message service, dialogs, navigator, logger factory) here.
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _current;

        /// <summary>
        /// The ambient provider. Defaults to an empty <see cref="DefaultServiceProvider"/>.
        /// Assign a configured provider (the built-in registry, or a real DI container's
        /// <see cref="IServiceProvider"/>) at application startup.
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_current == null)
                {
                    lock (_lock)
                    {
                        if (_current == null) _current = new DefaultServiceProvider();
                    }
                }
                return _current;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                lock (_lock) { _current = value; }
            }
        }

        /// <summary>
        /// Builds a fresh <see cref="DefaultServiceProvider"/>, lets <paramref name="register"/>
        /// populate it, and installs it as <see cref="Current"/>.
        /// </summary>
        public static void Configure(Action<IServiceRegistry> register)
        {
            var provider = new DefaultServiceProvider();
            register?.Invoke(provider);
            Current = provider;
        }

        /// <summary>Resets to an empty default provider (primarily for tests).</summary>
        public static void Reset()
        {
            lock (_lock) { _current = null; }
        }
    }
}
