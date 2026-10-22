using System;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.Common.Events;
using WinformsMVP.Common.Validation.Core;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for all presenters providing common functionality.
    /// This class should not be used directly - use WindowPresenterBase or ControlPresenterBase instead.
    /// </summary>
    public abstract class PresenterBase<TView> : IPresenter, IViewAttachable, IPresenterResourceOwner, IPresenterServicesBinding where TView : IViewBase
    {
        protected TView View { get; private set; }
        private readonly ViewActionDispatcher _dispatcher;

        private IServiceProvider _serviceProvider;
        private bool _initialized;

        // Cancelled first thing in Dispose: ends asynchronous action handlers and
        // OnInitializeAsync when the presenter (its window / control) goes away.
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        protected PresenterBase()
        {
            _dispatcher = new ViewActionDispatcher { Lifetime = _lifetime.Token };
        }

        /// <summary>
        /// Cancelled when the presenter is disposed (its window closed / control disposed). Pass it
        /// to asynchronous work that should stop with the presenter. Asynchronous action handlers
        /// and <see cref="OnInitializeAsync"/> receive it automatically.
        /// </summary>
        protected CancellationToken Lifetime => _lifetime.Token;

        void IPresenterServicesBinding.BindServices(IServiceProvider services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (View != null || _initialized)
                throw new InvalidOperationException(
                    GetType().Name + " already has a view; its services must be given before the view is attached.");
            if (_serviceProvider != null)
                throw new InvalidOperationException(
                    GetType().Name + " already resolved framework services (for example Logger or Messages in its constructor), " +
                    "so it cannot be given other services. Move that code to OnViewAttached or OnInitialize.");

            _serviceProvider = services;
        }

        /// <summary>The service provider this presenter resolves framework services from.
        /// Defaults to <see cref="WinformsMVP.Services.ServiceLocator.Current"/>.</summary>
        protected IServiceProvider Services =>
            _serviceProvider ?? (_serviceProvider = WinformsMVP.Services.ServiceLocator.Current);

        /// <summary>Convenience property for accessing IMessageService.</summary>
        protected WinformsMVP.Services.IMessageService Messages =>
            Services.ResolveRequired<WinformsMVP.Services.IMessageService>();

        /// <summary>Convenience property for accessing IToastService (non-blocking notifications).</summary>
        protected WinformsMVP.Services.IToastService Toasts =>
            Services.ResolveRequired<WinformsMVP.Services.IToastService>();

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
        /// Creates a <see cref="WinformsMVP.Common.ChangeTracker{T}"/> for <paramref name="value"/>
        /// using this application's configuration: a <see cref="WinformsMVP.Common.ChangeTrackingPolicy{T}"/>
        /// registered for exactly <typeparamref name="T"/> if there is one, otherwise the registered
        /// <see cref="WinformsMVP.Common.IObjectCloner"/> / <see cref="WinformsMVP.Common.IObjectContentComparer"/>
        /// (built-in engines when none are registered). The choice is fixed when the tracker is created.
        /// </summary>
        protected WinformsMVP.Common.ChangeTracker<T> TrackChanges<T>(T value) where T : class
            => new WinformsMVP.Common.ChangeTracker<T>(value, ResolveTrackingPolicy<T>());

        /// <summary>
        /// Creates a <see cref="WinformsMVP.Common.ChangeTracker{T}"/> with an explicit
        /// <paramref name="policy"/>; nothing is resolved from the service provider.
        /// </summary>
        protected WinformsMVP.Common.ChangeTracker<T> TrackChanges<T>(T value, WinformsMVP.Common.ChangeTrackingPolicy<T> policy) where T : class
            => new WinformsMVP.Common.ChangeTracker<T>(value, policy);

        /// <summary>
        /// Creates a <see cref="WinformsMVP.Common.ChangeTracker{T}"/> that compares with
        /// <paramref name="comparer"/>; snapshots use the configured cloning (the registered policy's
        /// Clone for <typeparamref name="T"/>, else the registered <see cref="WinformsMVP.Common.IObjectCloner"/>).
        /// </summary>
        protected WinformsMVP.Common.ChangeTracker<T> TrackChanges<T>(T value, Func<T, T, bool> comparer) where T : class
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            return new WinformsMVP.Common.ChangeTracker<T>(
                value, new WinformsMVP.Common.ChangeTrackingPolicy<T>(ResolveTrackingPolicy<T>().Clone, comparer));
        }

        private WinformsMVP.Common.ChangeTrackingPolicy<T> ResolveTrackingPolicy<T>() where T : class
        {
            var registered = Services.GetService(typeof(WinformsMVP.Common.ChangeTrackingPolicy<T>))
                as WinformsMVP.Common.ChangeTrackingPolicy<T>;
            if (registered != null) return registered;

            var cloner = Services.Resolve<WinformsMVP.Common.IObjectCloner>()
                ?? WinformsMVP.Common.BuiltInObjectCloner.Instance;
            var comparer = Services.Resolve<WinformsMVP.Common.IObjectContentComparer>()
                ?? WinformsMVP.Common.BuiltInObjectContentComparer.Instance;
            return WinformsMVP.Common.ChangeTrackingPolicy<T>.From(cloner, comparer);
        }

        /// <summary>
        /// Non-generic attach entry used by WindowNavigator (see <see cref="IViewAttachable"/>).
        /// Casts the framework-supplied view to <typeparamref name="TView"/> and routes through
        /// the same <see cref="SetView"/> path as the typed <see cref="IViewAttacher{TView}"/>.
        /// </summary>
        void IPresenterResourceOwner.OwnUntilDisposed(IDisposable resource) => Disposables.Add(resource);

        void IViewAttachable.AttachView(IViewBase view) => SetView((TView)view);
        bool IViewAttachable.IsViewAttached => View != null;
        bool IViewAttachable.IsInitialized => _initialized;
        Task IViewAttachable.InitializationTask => _initializationTask;
        IViewBase IViewAttachable.AttachedView => View;

        /// <summary>
        /// Sets the view for this presenter. Called by derived classes.
        /// </summary>
        protected void SetView(TView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (View != null)
                throw new InvalidOperationException(
                    GetType().Name + " already has a view attached. A presenter is attached to exactly one view.");

            View = view;

            // Configure the dispatcher BEFORE OnViewAttached / RegisterViewActions so that:
            //  - Global middleware (from IDispatcherConfigurer) is registered first,
            //    ending up outermost in the pipeline.
            //  - Any local middleware that user code adds inside RegisterViewActions appends
            //    to the end of the list, ending up innermost.
            //  - The Logger is wired so subsequent dispatch failures during user code can be
            //    reported through the presenter's logger.
            EnsureDispatcherConfigured();
            ConnectValidation();

            OnViewAttached();
        }

        private ValidationState _validation;
        private bool _validationConnected;

        /// <summary>
        /// The presenter's validation errors. Fill it with <c>Validation.Validate(model)</c> as the
        /// user edits and <c>Validation.Check(model)</c> on submit; add business rules with
        /// <c>SetError</c>. When the view implements <see cref="IValidatableView"/>, every change is
        /// shown through its <see cref="IValidatableView.ValidationErrors"/> and action
        /// availability (<c>canExecute</c>) is re-evaluated.
        /// </summary>
        protected ValidationState Validation
        {
            get
            {
                if (_validation == null)
                {
                    _validation = new ValidationState();
                    ConnectValidation();
                }
                return _validation;
            }
        }

        // Runs when the view is attached and when Validation is first used, whichever is later.
        private void ConnectValidation()
        {
            if (_validationConnected || _validation == null || View == null) return;
            _validationConnected = true;

            var display = (View as IValidatableView)?.ValidationErrors;
            EventHandler onChanged = (s, e) =>
            {
                display?.ShowErrors(_validation.Errors, _validation.RevealAll);
                _dispatcher.RaiseCanExecuteChanged();
            };
            EventHandler onReset = (s, e) => display?.Reset();
            _validation.ErrorsChanged += onChanged;
            _validation.WasReset += onReset;
            Disposables.Add(Disposable.Create(() =>
            {
                _validation.ErrorsChanged -= onChanged;
                _validation.WasReset -= onReset;
            }));
            display?.ShowErrors(_validation.Errors, _validation.RevealAll);
        }

        /// <summary>
        /// Called when the view is attached to this presenter.
        /// </summary>
        protected abstract void OnViewAttached();

        /// <summary>
        /// The single Initialize sequence behind every presenter base: guard, register actions,
        /// auto-bind the view's <see cref="IActionableView.ActionBinder"/>, then the user hook.
        /// </summary>
        private protected void InitializeCore(Action registerViewActions, Action onInitialize)
        {
            if (_initialized)
                throw new InvalidOperationException("Presenter has already been initialized");
            if (View == null)
                throw new InvalidOperationException(
                    GetType().Name + " has no view attached. Attach a view (AttachView / Connect) before Initialize.");

            _initialized = true;
            registerViewActions();

            // Bind after actions are registered. Views outside the ViewAction system simply
            // don't implement IActionableView.
            if (View is IActionableView actionable && actionable.ActionBinder != null)
            {
                _boundBinder = actionable.ActionBinder;
                _boundBinder.Bind(_dispatcher);
            }

            onInitialize();
            _initializationTask = RunInitializeAsync();
        }

        private Task _initializationTask = Task.CompletedTask;

        // The binder the framework bound in InitializeCore; unbound when this presenter is disposed.
        private IViewActionBinder _boundBinder;

        /// <summary>
        /// Asynchronous part of initialization (e.g. loading data), started right after
        /// <c>OnInitialize</c>; the window is shown without waiting for it. The token is
        /// <see cref="Lifetime"/>. A failure is logged — handle expected errors (show a message,
        /// fill an error state) inside the override. Cancellation caused by the presenter being
        /// disposed is silent.
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private Task RunInitializeAsync()
        {
            Task pending;
            try
            {
                pending = OnInitializeAsync(_lifetime.Token) ?? Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogInitializeFailure(ex);
                return Task.CompletedTask;
            }
            if (pending.Status == TaskStatus.RanToCompletion) return pending;
            return ObserveInitializeAsync(pending);
        }

        private async Task ObserveInitializeAsync(Task pending)
        {
            try
            {
                await pending;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                // The presenter went away while loading: nothing to report.
            }
            catch (Exception ex)
            {
                LogInitializeFailure(ex);
            }
        }

        private void LogInitializeFailure(Exception ex)
            => Logger.LogError(ex, "{Presenter}.OnInitializeAsync failed", GetType().Name);

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
            if (_isDisposed) return;
            // Mark first so a re-entrant Dispose (from inside Cleanup) is a no-op.
            _isDisposed = true;
            // Stop dispatching first: a late click on a control that outlives this presenter must not
            // run a handler, and in-flight async handlers must not refresh control states.
            _dispatcher.Deactivate();
            try { _lifetime.Cancel(); }
            catch (AggregateException ex) { Logger.LogError(ex, "A Lifetime cancellation callback threw"); }
            try
            {
                _boundBinder?.Unbind();
                _boundBinder = null;
            }
            catch (Exception ex) { Logger.LogError(ex, "Unbinding the view's action binder threw"); }
            try
            {
                // User teardown first (presenter state still intact), then the framework sweeps the
                // subscription bag. The sweep lives here — not in Cleanup — and in a finally, so an
                // override that forgets to call base, or throws, cannot leak the bag.
                Cleanup();
            }
            finally
            {
                _disposables?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Subscribes to a view event and unsubscribes when this presenter is disposed — one line
        /// instead of a <c>+=</c> plus a matching <c>-=</c> in a <c>Disposable.Create</c>:
        /// <code>
        /// SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h,
        ///                    () => Dispatcher.RaiseCanExecuteChanged());
        /// </code>
        /// Once disposal begins the handler is no longer called, even by an event raised while the
        /// view's controls are torn down (which happens after the presenter's <c>Cleanup</c>). The
        /// returned token unsubscribes earlier; disposing it again is a no-op. The handler runs
        /// synchronously on the raising thread, and its exceptions propagate to the raiser.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null (nothing is attached).</exception>
        /// <exception cref="ObjectDisposedException">The presenter is already disposed (nothing is attached).</exception>
        protected IDisposable SubscribeViewEvent(Action<EventHandler> add, Action<EventHandler> remove, Action handler)
        {
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

            var subscription = new ViewEventSubscription<EventHandler>(remove);
            subscription.Attach(add, (sender, e) => { if (subscription.IsActive && !_isDisposed) handler(); });
            return subscription.DisposeWith(Disposables);
        }

        /// <summary>
        /// <see cref="SubscribeViewEvent(Action{EventHandler}, Action{EventHandler}, Action)"/> for an
        /// <see cref="EventHandler{TEventArgs}"/> event; the handler receives the event arguments.
        /// </summary>
        protected IDisposable SubscribeViewEvent<TEventArgs>(Action<EventHandler<TEventArgs>> add,
            Action<EventHandler<TEventArgs>> remove, Action<TEventArgs> handler)
        {
            if (add == null) throw new ArgumentNullException(nameof(add));
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

            var subscription = new ViewEventSubscription<EventHandler<TEventArgs>>(remove);
            subscription.Attach(add, (sender, e) => { if (subscription.IsActive && !_isDisposed) handler(e); });
            return subscription.DisposeWith(Disposables);
        }

        /// <summary>Removes the exact delegate it attached, once.</summary>
        private sealed class ViewEventSubscription<THandler> : IDisposable where THandler : Delegate
        {
            private Action<THandler> _remove;
            private THandler _handler;

            public ViewEventSubscription(Action<THandler> remove) { _remove = remove; }

            public bool IsActive { get; private set; }

            public void Attach(Action<THandler> add, THandler handler)
            {
                _handler = handler;
                add(handler);
                IsActive = true;
            }

            public void Dispose()
            {
                if (!IsActive) return;
                IsActive = false;   // before remove: an event raised meanwhile is ignored
                var remove = _remove;
                _remove = null;
                remove(_handler);
                _handler = null;
            }
        }

        /// <summary>
        /// Override to clean up resources when the presenter is disposed.
        /// Subscriptions registered via <c>.DisposeWith(Disposables)</c> are released automatically
        /// after this method returns; no override is needed for those.
        /// </summary>
        protected virtual void Cleanup() { }
    }
}
