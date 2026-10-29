using System;
using System.Reflection;
using Kibinago.Logging;
using Kibinago.Services;

namespace Kibinago.Services
{
    /// <summary>
    /// Static ambient access to the current <see cref="IServiceProvider"/> — the framework's
    /// service-resolution root (the Prism <c>ContainerLocator</c> role). The framework internals
    /// and the presenter convenience accessors resolve through <see cref="Current"/>; business
    /// code should prefer constructor injection rather than locating through this.
    /// </summary>
    /// <remarks>
    /// The default provider is pre-seeded with the framework built-ins: <see cref="IMessageService"/>,
    /// <see cref="IDialogProvider"/>, <see cref="ILoggerFactory"/>,
    /// <see cref="IWindowNavigator"/>, and <c>IViewMappingRegister</c>. The WinForms-backed
    /// services come from the Kibinago assembly, which this core assembly discovers at runtime;
    /// without it only the platform-neutral built-ins are registered. Call
    /// <see cref="Configure"/> at startup to add or override any of these before presenters start
    /// resolving them. <c>ActionDispatcherOptions</c> and <c>IActionObserver</c> are absent by default (defaults, no observer).
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _current;
        private static bool _ownsCurrent;   // true when the framework built _current (lazy default or Configure)

        /// <summary>
        /// The ambient provider. Defaults to a <see cref="DefaultServiceProvider"/> pre-seeded
        /// with the framework built-in services. Assign a configured provider (the built-in
        /// registry, or a real DI container's <see cref="IServiceProvider"/>) at application
        /// startup to add or replace services.
        /// </summary>
        /// <remarks>
        /// Assigning never disposes the previous provider: an assigned provider belongs to whoever
        /// built it. Providers the framework built itself (the lazy default, or one built by
        /// <see cref="Configure"/>) are disposed when they are replaced or <see cref="Reset"/>.
        /// </remarks>
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
                            _ownsCurrent = true;
                        }
                    }
                }
                return _current;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                Install(value, owned: false);
            }
        }

        /// <summary>
        /// Builds a fresh <see cref="DefaultServiceProvider"/>, seeds it with the framework
        /// built-ins, then lets <paramref name="register"/> add or override registrations
        /// (last-registration-wins), and installs it as <see cref="Current"/>. A provider the
        /// framework built earlier (the lazy default or a previous <c>Configure</c>) is disposed.
        /// </summary>
        public static void Configure(Action<IServiceRegistry> register)
        {
            var provider = new DefaultServiceProvider();
            RegisterBuiltIns(provider);
            register?.Invoke(provider);   // caller adds or overrides (last-wins)
            Install(provider, owned: true);
        }

        /// <summary>
        /// Resets to a null default so the next access re-seeds the built-ins (primarily for tests).
        /// A provider the framework built is disposed.
        /// </summary>
        public static void Reset()
        {
            Install(null, owned: false);
        }

        /// <summary>
        /// Swaps the ambient provider. When <paramref name="owned"/> is true the framework built
        /// <paramref name="provider"/> and disposes it when it is replaced. The previous provider is
        /// disposed (outside the lock) only if the framework owned it.
        /// </summary>
        internal static void Install(IServiceProvider provider, bool owned)
        {
            IServiceProvider previous;
            bool previousOwned;
            lock (_lock)
            {
                previous = _current;
                previousOwned = _ownsCurrent;
                _current = provider;
                _ownsCurrent = provider != null && owned;
            }
            if (previousOwned && !ReferenceEquals(previous, provider))
                (previous as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Registers the framework's built-in services into <paramref name="provider"/> with
        /// "first caller wins" intent: call this first, then let callers override via last-wins.
        /// <c>ActionDispatcherOptions</c> and <c>IActionObserver</c> are not registered by default
        /// (null =&gt; default options, no observer).
        /// </summary>
        internal static void RegisterBuiltIns(DefaultServiceProvider provider)
        {
            provider.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            provider.RegisterInstance<IBusyIndicator>(NullBusyIndicator.Instance);
            provider.RegisterInstance<Kibinago.Common.IObjectCloner>(Kibinago.Common.BuiltInObjectCloner.Instance);
            provider.RegisterInstance<Kibinago.Common.IObjectContentComparer>(Kibinago.Common.BuiltInObjectContentComparer.Instance);
            provider.RegisterSingleton<Kibinago.Events.IEventAggregator>(sp =>
                new Kibinago.Events.EventAggregator(sp.ResolveRequired<ILoggerFactory>()));
            PlatformRegistration.Value?.Invoke(provider);
        }

        // The WinForms package contributes its built-ins (view mapping, message/dialog services,
        // navigator) through an internal static Register(IServiceRegistry) on a well-known type.
        // Looked up once; null when the Kibinago assembly is not deployed (e.g. pure presenter tests).
        private const string PlatformRegistrationType =
            "Kibinago.Services.WinFormsPlatformServices, Kibinago";

        private static readonly Lazy<Action<IServiceRegistry>> PlatformRegistration =
            new Lazy<Action<IServiceRegistry>>(() =>
            {
                var type = Type.GetType(PlatformRegistrationType, throwOnError: false);
                var method = type?.GetMethod("Register",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(IServiceRegistry) }, null);
                return method == null
                    ? null
                    : (Action<IServiceRegistry>)Delegate.CreateDelegate(typeof(Action<IServiceRegistry>), method);
            });
    }
}
