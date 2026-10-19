using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.Logging;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Configuration
{
    /// <summary>
    /// The one place to customize the framework, passed to <c>MvpApplication.Configure(framework: ...)</c>
    /// or <c>services.AddWinformsMVP(viewRegistry, mvp =&gt; ...)</c>. Type <c>mvp.</c> to see everything
    /// that can be replaced; anything not set keeps the built-in default.
    /// </summary>
    /// <remarks>
    /// The builder only produces service registrations. Precedence: a service registered directly
    /// (in the <c>services:</c> block, or in M.E.DI) wins over a choice made here, which wins over the
    /// built-in default. A directly registered options object (e.g. <see cref="ToastHostOptions"/>)
    /// replaces the one built here as a whole — values are not merged. Once the configuration has
    /// been applied the builder is frozen; changing it afterwards throws.
    /// </remarks>
    public sealed class WinformsMvpBuilder
    {
        private readonly List<FrameworkRegistration> _choices = new List<FrameworkRegistration>();
        private bool _built;

        public WinformsMvpBuilder()
        {
            Messages = new MessagesBuilder(this);
            Toasts = new ToastsBuilder(this);
            Dialogs = new DialogsBuilder(this);
            ChangeTracking = new ChangeTrackingBuilder(this);
            Events = new EventsBuilder(this);
            Logging = new LoggingBuilder(this);
            Actions = new ActionsBuilder(this);
        }

        /// <summary>Message boxes: default caption, replacement dialog backend.</summary>
        public MessagesBuilder Messages { get; }

        /// <summary>Toasts: position, size, font, duration, stacking, style, renderer, or a whole replacement service.</summary>
        public ToastsBuilder Toasts { get; }

        /// <summary>File / folder / print dialogs: default titles, filter, error messages.</summary>
        public DialogsBuilder Dialogs { get; }

        /// <summary>Change tracking: deep-copy and compare engines, per-type policies.</summary>
        public ChangeTrackingBuilder ChangeTracking { get; }

        /// <summary>The event aggregator.</summary>
        public EventsBuilder Events { get; }

        /// <summary>The logger factory.</summary>
        public LoggingBuilder Logging { get; }

        /// <summary>Global view-action dispatcher configuration (middleware, strict validation).</summary>
        public ActionsBuilder Actions { get; }

        internal void EnsureNotBuilt()
        {
            if (_built)
                throw new InvalidOperationException(
                    "The framework configuration has already been applied; change it inside the configure callback.");
        }

        internal void SetInstance(Type serviceType, object instance)
        {
            EnsureNotBuilt();
            _choices.Add(FrameworkRegistration.ForInstance(serviceType, instance));
        }

        internal void SetFactory(Type serviceType, Func<IServiceProvider, object> factory)
        {
            EnsureNotBuilt();
            _choices.Add(FrameworkRegistration.ForFactory(serviceType, factory));
        }

        /// <summary>
        /// The complete list of framework registrations: the built-in defaults with this builder's
        /// choices applied over them (one entry per service type). Freezes the builder.
        /// </summary>
        internal IReadOnlyList<FrameworkRegistration> BuildRegistrations()
        {
            _built = true;

            var order = new List<Type>();
            var byType = new Dictionary<Type, FrameworkRegistration>();
            void Put(FrameworkRegistration r)
            {
                if (!byType.ContainsKey(r.ServiceType)) order.Add(r.ServiceType);
                byType[r.ServiceType] = r;
            }

            Put(FrameworkRegistration.ForInstance(typeof(MessageDialogOptions), Messages.Options.Copy()));
            Put(FrameworkRegistration.ForInstance(typeof(IMessageDialogBackend), new NativeMessageDialogBackend()));
            Put(FrameworkRegistration.ForFactory(typeof(IMessageService), sp => new MessageService(
                sp.ResolveRequired<IMessageDialogBackend>(), sp.ResolveRequired<MessageDialogOptions>())));

            Put(FrameworkRegistration.ForInstance(typeof(ToastHostOptions), Toasts.Options.Copy()));
            Put(FrameworkRegistration.ForFactory(typeof(WinFormsToastService), sp => new WinFormsToastService(
                sp.ResolveRequired<ToastHostOptions>(), sp.Resolve<ToastRenderer>(), sp.Resolve<ILoggerFactory>())));
            Put(FrameworkRegistration.ForFactory(typeof(IWinFormsToastService), sp => sp.ResolveRequired<WinFormsToastService>()));
            Put(FrameworkRegistration.ForFactory(typeof(IToastService), sp => sp.ResolveRequired<IWinFormsToastService>()));

            Put(FrameworkRegistration.ForInstance(typeof(DialogProviderOptions), Dialogs.Options.Copy()));
            Put(FrameworkRegistration.ForFactory(typeof(DialogProvider), sp => new DialogProvider(sp.ResolveRequired<DialogProviderOptions>())));
            Put(FrameworkRegistration.ForFactory(typeof(IDialogProvider), sp => sp.ResolveRequired<DialogProvider>()));
            Put(FrameworkRegistration.ForFactory(typeof(IWinFormsDialogProvider), sp => sp.ResolveRequired<DialogProvider>()));

            Put(FrameworkRegistration.ForInstance(typeof(IObjectCloner), BuiltInObjectCloner.Instance));
            Put(FrameworkRegistration.ForInstance(typeof(IObjectContentComparer), BuiltInObjectContentComparer.Instance));

            Put(FrameworkRegistration.ForFactory(typeof(IEventAggregator), sp => new EventAggregator(sp.ResolveRequired<ILoggerFactory>())));
            Put(FrameworkRegistration.ForInstance(typeof(ILoggerFactory), NullLoggerFactory.Instance));

            var configurer = Actions.BuildConfigurer();
            if (configurer != null) Put(FrameworkRegistration.ForInstance(typeof(IDispatcherConfigurer), configurer));

            foreach (var choice in _choices) Put(choice);

            var result = new List<FrameworkRegistration>(order.Count);
            foreach (var type in order) result.Add(byType[type]);
            return result;
        }

        /// <summary>Writes <see cref="BuildRegistrations"/> into <paramref name="provider"/> (later registrations win).</summary>
        internal void Apply(DefaultServiceProvider provider)
        {
            foreach (var r in BuildRegistrations())
            {
                if (r.Factory != null) provider.RegisterFactory(r.ServiceType, r.Factory);
                else provider.Register(r.ServiceType, r.Instance);
            }
        }
    }
}
