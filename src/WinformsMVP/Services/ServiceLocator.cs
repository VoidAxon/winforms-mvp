using System;
using WinformsMVP.Logging;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Static ambient access to the current <see cref="IServiceProvider"/> — the framework's
    /// service-resolution root (the Prism <c>ContainerLocator</c> role). The framework internals
    /// and the presenter convenience accessors resolve through <see cref="Current"/>; business
    /// code should prefer constructor injection rather than locating through this.
    /// </summary>
    /// <remarks>
    /// The default provider is pre-seeded with the framework built-ins: <see cref="IMessageService"/>,
    /// <see cref="IDialogProvider"/>, <see cref="IFileService"/>, <see cref="ILoggerFactory"/>,
    /// <see cref="IWindowNavigator"/>, and <see cref="IViewMappingRegister"/>. Call
    /// <see cref="Configure"/> at startup to add or override any of these before presenters start
    /// resolving them. <c>IDispatcherConfigurer</c> is intentionally absent by default.
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _current;

        /// <summary>
        /// The ambient provider. Defaults to a <see cref="DefaultServiceProvider"/> pre-seeded
        /// with the framework built-in services. Assign a configured provider (the built-in
        /// registry, or a real DI container's <see cref="IServiceProvider"/>) at application
        /// startup to add or replace services.
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_current == null)
                {
                    lock (_lock)
                    {
                        if (_current == null)
                        {
                            var provider = new DefaultServiceProvider();
                            RegisterBuiltIns(provider);
                            _current = provider;
                        }
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
        /// Builds a fresh <see cref="DefaultServiceProvider"/>, seeds it with the framework
        /// built-ins, then lets <paramref name="register"/> add or override registrations
        /// (last-registration-wins), and installs it as <see cref="Current"/>.
        /// </summary>
        public static void Configure(Action<IServiceRegistry> register)
        {
            var provider = new DefaultServiceProvider();
            RegisterBuiltIns(provider);
            register?.Invoke(provider);   // caller adds or overrides (last-wins)
            Current = provider;
        }

        /// <summary>Resets to a null default so the next access re-seeds the built-ins (primarily for tests).</summary>
        public static void Reset()
        {
            lock (_lock) { _current = null; }
        }

        /// <summary>
        /// Registers the framework's built-in services into <paramref name="provider"/> with
        /// "first caller wins" intent: call this first, then let callers override via last-wins.
        /// <see cref="IWindowNavigator"/> is built lazily from the registered
        /// <see cref="IViewMappingRegister"/> and uses the same provider for view-resolution
        /// fallback. <c>IDispatcherConfigurer</c> is intentionally NOT registered by default
        /// (null =&gt; no global configuration).
        /// </summary>
        private static void RegisterBuiltIns(DefaultServiceProvider provider)
        {
            provider.RegisterInstance<IViewMappingRegister>(new ViewMappingRegister());
            provider.RegisterInstance<IMessageService>(new MessageService());
            provider.RegisterInstance<IDialogProvider>(new DialogProvider());
            provider.RegisterInstance<IFileService>(new FileService());
            provider.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            provider.RegisterFactory<IWindowNavigator>(sp =>
                new WindowNavigator(sp.Resolve<IViewMappingRegister>().WithServiceProvider(sp)));
        }
    }
}
