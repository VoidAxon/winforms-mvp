using System;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for all presenters providing common functionality.
    /// This class should not be used directly - use WindowPresenterBase or ControlPresenterBase instead.
    /// </summary>
    public abstract class PresenterBase<TView> : IPresenter, IViewAttachable where TView : IViewBase
    {
        protected TView View { get; private set; }
        protected readonly ViewActionDispatcher _dispatcher;

        private IServiceProvider _serviceProvider;
        protected bool _initialized = false;

        protected PresenterBase()
        {
            _dispatcher = new ViewActionDispatcher();
        }

        /// <summary>
        /// Overrides the service provider for this presenter (tests / scoped composition).
        /// MUST be called before AttachView() or Initialize().
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when called after initialization</exception>
        internal void SetServiceProvider(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (_initialized)
                throw new InvalidOperationException(
                    "Cannot set the service provider after the presenter has been initialized. " +
                    "Call SetServiceProvider() before AttachView() or Initialize().");

            _serviceProvider = serviceProvider;
        }

        /// <summary>The service provider this presenter resolves framework services from.
        /// Defaults to <see cref="WinformsMVP.Services.ServiceLocator.Current"/>.</summary>
        protected IServiceProvider Services =>
            _serviceProvider ?? (_serviceProvider = WinformsMVP.Services.ServiceLocator.Current);

        /// <summary>Convenience property for accessing IMessageService.</summary>
        protected WinformsMVP.Services.IMessageService Messages =>
            Services.ResolveRequired<WinformsMVP.Services.IMessageService>();

        /// <summary>Convenience property for accessing IDialogProvider.</summary>
        protected WinformsMVP.Services.IDialogProvider Dialogs =>
            Services.ResolveRequired<WinformsMVP.Services.IDialogProvider>();

        /// <summary>Convenience property for accessing IFileService.</summary>
        protected WinformsMVP.Services.IFileService Files =>
            Services.ResolveRequired<WinformsMVP.Services.IFileService>();

        /// <summary>Convenience property for accessing IWindowNavigator.</summary>
        protected WinformsMVP.Services.IWindowNavigator Navigator =>
            Services.ResolveRequired<WinformsMVP.Services.IWindowNavigator>();

        private ILogger _logger;

        /// <summary>
        /// Logger for this presenter. Resolved from the service provider's
        /// <see cref="WinformsMVP.Logging.ILoggerFactory"/>.
        /// </summary>
        /// <example>
        /// <code>
        /// Logger.LogInformation("User {UserName} opened screen", userName);
        /// Logger.LogError(ex, "Failed to save data");
        /// </code>
        /// </example>
        protected ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    var loggerFactory = Services.ResolveRequired<WinformsMVP.Logging.ILoggerFactory>();
                    _logger = loggerFactory.CreateLogger(this.GetType());
                }
                return _logger;
            }
        }

        /// <summary>
        /// Non-generic attach entry used by WindowNavigator (see <see cref="IViewAttachable"/>).
        /// Casts the framework-supplied view to <typeparamref name="TView"/> and routes through
        /// the same <see cref="SetView"/> path as the typed <see cref="IViewAttacher{TView}"/>.
        /// </summary>
        void IViewAttachable.AttachView(IViewBase view) => SetView((TView)view);
        bool IViewAttachable.IsViewAttached => View != null;

        /// <summary>
        /// Sets the view for this presenter. Called by derived classes.
        /// </summary>
        protected void SetView(TView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            View = view;

            // Configure the dispatcher BEFORE OnViewAttached / RegisterViewActions so that:
            //  - Global middleware (from IDispatcherConfigurer) is registered first,
            //    ending up outermost in the pipeline.
            //  - Any local middleware that user code adds inside RegisterViewActions appends
            //    to the end of the list, ending up innermost.
            //  - The Logger is wired so subsequent dispatch failures during user code can be
            //    reported through the presenter's logger.
            // Sample presenters access the underlying _dispatcher field directly (rather than
            // the Dispatcher property), so we cannot rely on the property getter to lazy-init.
            EnsureDispatcherConfigured();

            OnViewAttached();
        }

        /// <summary>
        /// Called when the view is attached to this presenter.
        /// </summary>
        protected abstract void OnViewAttached();

        public Type ViewInterfaceType => typeof(TView);

        private bool _dispatcherInitialized;

        protected ViewActionDispatcher Dispatcher
        {
            get
            {
                // Defensive lazy-init for code paths that access Dispatcher before SetView
                // (e.g., unit tests that construct a presenter and dispatch without attaching a view).
                EnsureDispatcherConfigured();
                return _dispatcher;
            }
        }

        /// <summary>
        /// One-shot configuration of the underlying dispatcher: wires the logger and applies
        /// the optional global <see cref="WinformsMVP.Services.IDispatcherConfigurer"/>. Called
        /// from <see cref="SetView"/> so global middleware is in place before user code in
        /// <c>RegisterViewActions</c> registers local middleware or action handlers.
        /// </summary>
        /// <remarks>
        /// Order matters and is part of the contract:
        ///  <list type="number">
        ///   <item>Wire the logger first so any global middleware that itself logs during
        ///         configuration sees a non-null logger.</item>
        ///   <item>Apply global middleware (<c>IDispatcherConfigurer.Configure</c>) so it ends
        ///         up outermost in the pipeline.</item>
        ///   <item>Mark initialized <b>before</b> calling user callbacks, so reentrant
        ///         access from inside a configure callback doesn't loop.</item>
        /// </list>
        /// Local middleware that user code adds inside <c>RegisterViewActions</c> runs after
        /// this returns, so it appends to the end of the list and ends up innermost — wrapped
        /// by global middleware. See <see cref="ViewActionDispatcher"/>.
        /// </remarks>
        private void EnsureDispatcherConfigured()
        {
            if (_dispatcherInitialized) return;
            _dispatcherInitialized = true;  // set BEFORE invoking callback (re-entrancy guard)
            _dispatcher.Logger = Logger;
            Services.Resolve<WinformsMVP.Services.IDispatcherConfigurer>()?.Configure(_dispatcher);
        }

        /// <summary>
        /// Helper method to dispatch actions from view events.
        /// </summary>
        protected void OnViewActionTriggered(object sender, ActionRequestEventArgs e)
        {
            DispatchAction(e);
        }

        /// <summary>
        /// Dispatches an action request to the registered handler.
        /// </summary>
        protected void DispatchAction(ActionRequestEventArgs e)
        {
            if (e == null) return;

            var key = e.ActionKey;
            object payload = null;

            if (e is IActionRequestEventArgsWithValue valueProvider)
            {
                payload = valueProvider.GetValue();
            }

            Dispatcher.Dispatch(key, payload);
        }

        // IDisposable implementation
        private bool _isDisposed = false;
        private CompositeDisposable _disposables;

        /// <summary>
        /// Subscriptions tied to this presenter's lifetime. Register anything that must be released
        /// when the presenter is disposed — an <c>IEventAggregator.Subscribe</c> token, a
        /// <c>Cascade.Bind</c> unsubscriber, a <c>Disposable.Create(() =&gt; view.Event -= handler)</c>
        /// wrapper — via <c>.DisposeWith(Disposables)</c> at the creation line. The framework disposes
        /// the bag automatically right after <see cref="Cleanup"/>, so no Cleanup override is needed
        /// for these. Created lazily: presenters with no subscriptions allocate nothing.
        /// </summary>
        protected CompositeDisposable Disposables
        {
            get
            {
                if (_disposables == null)
                {
                    _disposables = new CompositeDisposable();
                    if (_isDisposed) _disposables.Dispose();   // post-dispose: keep the Add-disposes-immediately contract
                }
                return _disposables;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                // User teardown first (presenter state still intact), then the framework sweeps the
                // subscription bag. The sweep lives here — not in Cleanup — so an override that
                // forgets to call base cannot leak the bag.
                Cleanup();
                if (_disposables != null) _disposables.Dispose();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Override to clean up resources when the presenter is disposed.
        /// Subscriptions registered via <c>.DisposeWith(Disposables)</c> are released automatically
        /// after this method returns; no override is needed for those.
        /// </summary>
        protected virtual void Cleanup() { }
    }
}
