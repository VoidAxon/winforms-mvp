using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Logging;
using Kibinago.Events;
using Kibinago.Validation;
using Kibinago.Views;
using Kibinago.ViewActions;
using Kibinago.Services;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Base class for all presenters providing common functionality.
    /// This class should not be used directly - use WindowPresenterBase or ControlPresenterBase instead.
    /// </summary>
    public abstract class PresenterBase<TView> : IPresenter, IViewAttachable, IPresenterResourceOwner, IPresenterServicesBinding, IComposablePresenter where TView : IViewBase
    {
        /// <summary>The attached view; <c>null</c> until the view is attached.</summary>
        protected TView View { get; private set; }
        private readonly ViewActionDispatcher _dispatcher;

        private IServiceProvider _serviceProvider;
        private bool _initialized;

        // Cancelled first thing in Dispose: ends asynchronous action handlers and
        // OnInitializeAsync when the presenter (its window / control) goes away.
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        /// <summary>Creates the presenter and its action dispatcher.</summary>
        /// <remarks>Only the framework's presenter bases derive from this class: derive from
        /// <c>WindowPresenterBase</c> or <c>ControlPresenterBase</c>.</remarks>
        private protected PresenterBase()
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
            _servicesBound = true;
        }

        // True when services were given explicitly (BindServices / inherited from an owner), as opposed to
        // resolved lazily from ServiceLocator.Current.
        private bool _servicesBound;

        /// <summary>The service provider this presenter resolves framework services from.
        /// Defaults to <see cref="Kibinago.Services.ServiceLocator.Current"/>.</summary>
        protected IServiceProvider Services =>
            _serviceProvider ?? (_serviceProvider = Kibinago.Services.ServiceLocator.Current);

        /// <summary>Convenience property for accessing IMessageService.</summary>
        protected Kibinago.Services.IMessageService Messages =>
            Services.ResolveRequired<Kibinago.Services.IMessageService>();

        /// <summary>Convenience property for accessing IToastService (non-blocking notifications).</summary>
        protected Kibinago.Services.IToastService Toasts =>
            Services.ResolveRequired<Kibinago.Services.IToastService>();

        /// <summary>Convenience property for accessing IDialogProvider.</summary>
        protected Kibinago.Services.IDialogProvider Dialogs =>
            Services.ResolveRequired<Kibinago.Services.IDialogProvider>();

        /// <summary>Convenience property for accessing IWindowNavigator.</summary>
        protected Kibinago.Services.IWindowNavigator Navigator =>
            Services.ResolveRequired<Kibinago.Services.IWindowNavigator>();

        private ILogger _logger;

        /// <summary>
        /// Logger for this presenter. Resolved from the service provider's
        /// <see cref="Kibinago.Logging.ILoggerFactory"/>.
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
                    var loggerFactory = Services.ResolveRequired<Kibinago.Logging.ILoggerFactory>();
                    _logger = loggerFactory.CreateLogger(this.GetType());
                }
                return _logger;
            }
        }

        /// <summary>
        /// Creates a <see cref="Kibinago.Common.ChangeTracker{T}"/> for <paramref name="value"/>
        /// using this application's configuration: a <see cref="Kibinago.Common.ChangeTrackingPolicy{T}"/>
        /// registered for exactly <typeparamref name="T"/> if there is one, otherwise the registered
        /// <see cref="Kibinago.Common.IObjectCloner"/> / <see cref="Kibinago.Common.IObjectContentComparer"/>
        /// (built-in engines when none are registered). The choice is fixed when the tracker is created.
        /// </summary>
        protected Kibinago.Common.ChangeTracker<T> TrackChanges<T>(T value) where T : class
            => new Kibinago.Common.ChangeTracker<T>(value, ResolveTrackingPolicy<T>());

        /// <summary>
        /// Creates a <see cref="Kibinago.Common.ChangeTracker{T}"/> with an explicit
        /// <paramref name="policy"/>; nothing is resolved from the service provider.
        /// </summary>
        protected Kibinago.Common.ChangeTracker<T> TrackChanges<T>(T value, Kibinago.Common.ChangeTrackingPolicy<T> policy) where T : class
            => new Kibinago.Common.ChangeTracker<T>(value, policy);

        /// <summary>
        /// Creates a <see cref="Kibinago.Common.ChangeTracker{T}"/> that compares with
        /// <paramref name="comparer"/>; snapshots use the configured cloning (the registered policy's
        /// Clone for <typeparamref name="T"/>, else the registered <see cref="Kibinago.Common.IObjectCloner"/>).
        /// </summary>
        protected Kibinago.Common.ChangeTracker<T> TrackChanges<T>(T value, Func<T, T, bool> comparer) where T : class
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            return new Kibinago.Common.ChangeTracker<T>(
                value, new Kibinago.Common.ChangeTrackingPolicy<T>(ResolveTrackingPolicy<T>().Clone, comparer));
        }

        private Kibinago.Common.ChangeTrackingPolicy<T> ResolveTrackingPolicy<T>() where T : class
        {
            var registered = Services.GetService(typeof(Kibinago.Common.ChangeTrackingPolicy<T>))
                as Kibinago.Common.ChangeTrackingPolicy<T>;
            if (registered != null) return registered;

            var cloner = Services.Resolve<Kibinago.Common.IObjectCloner>()
                ?? Kibinago.Common.BuiltInObjectCloner.Instance;
            var comparer = Services.Resolve<Kibinago.Common.IObjectContentComparer>()
                ?? Kibinago.Common.BuiltInObjectContentComparer.Instance;
            return Kibinago.Common.ChangeTrackingPolicy<T>.From(cloner, comparer);
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
        /// Sets the view for this presenter. Called by the framework's presenter bases.
        /// </summary>
        private protected void SetView(TView view)
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
        /// Called when the view is attached to this presenter: subscribe to view events here
        /// (<c>SubscribeViewEvent</c>). Override only when there is something to do.
        /// </summary>
        protected virtual void OnViewAttached() { }

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
            // Each stage stops when the presenter was disposed before it (its control was disposed
            // meanwhile — even in OnViewAttached): nothing is registered, bound or started on a disposed
            // presenter.
            if (_isDisposed) return;
            registerViewActions();
            if (_isDisposed) return;

            // Bind after actions are registered. Views outside the ViewAction system simply
            // don't implement IActionableView.
            if (View is IActionableView actionable && actionable.ActionBinder != null)
            {
                _boundBinder = actionable.ActionBinder;
                _boundBinder.Bind(_dispatcher);
            }
            if (_isDisposed) return;

            onInitialize();
            if (_isDisposed) return;
            _initializationTask = RunInitializeAsync();
            // The binder computed control states at Bind, before the data OnInitialize (and the
            // synchronous part of OnInitializeAsync) loaded: refresh them now.
            if (!_isDisposed) _dispatcher.RaiseCanExecuteChanged();
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
            // Same as after OnInitialize: the loaded data may enable or disable actions.
            if (!_isDisposed) _dispatcher.RaiseCanExecuteChanged();
        }

        private void LogInitializeFailure(Exception ex)
            => Logger.LogError(ex, "{Presenter}.OnInitializeAsync failed", GetType().Name);

        /// <inheritdoc />
        public Type ViewInterfaceType => typeof(TView);

        private bool _dispatcherInitialized;

        /// <summary>
        /// The presenter's action dispatcher: register handlers on it in <c>RegisterViewActions</c>; the
        /// framework then binds it to the view's <c>ActionBinder</c>. Once the presenter is disposed it
        /// ignores dispatches.
        /// </summary>
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
        /// the optional global <see cref="Kibinago.ViewActions.IDispatcherConfigurer"/>. Called
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
            Services.Resolve<Kibinago.ViewActions.IDispatcherConfigurer>()?.Configure(_dispatcher);
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

        /// <summary>
        /// Disposes the presenter: stops its dispatcher, cancels <see cref="Lifetime"/> and unbinds the
        /// action binder, then runs <see cref="Cleanup"/>, disposes the child presenters (in reverse
        /// order) and finally <see cref="Disposables"/>. A repeated call does nothing. Every step runs
        /// even when an earlier one throws; one failure is rethrown, several as an
        /// <see cref="AggregateException"/>.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            // Mark first so a re-entrant Dispose (from inside Cleanup) is a no-op.
            _isDisposed = true;
            var failures = new List<Exception>();

            // An owner that released this presenter (handle disposed, or this presenter disposed
            // directly) forgets it before any teardown runs.
            Try(failures, () => _ownerLink?.Unlink());

            // Stop dispatching here and in every descendant first: a late click on a control that
            // outlives its presenter must not run a handler, and in-flight async handlers must not
            // refresh control states — also while children wait for their own cleanup below.
            ((IComposablePresenter)this).DeactivateTree();
            try { _lifetime.Cancel(); }
            catch (AggregateException ex) { Logger.LogError(ex, "A Lifetime cancellation callback threw"); }
            Try(failures, () =>
            {
                _boundBinder?.Unbind();
                _boundBinder = null;
            });

            // User teardown first (presenter state still intact), then the children in reverse order
            // (they may still use this presenter's resources), then the framework sweeps the
            // subscription bag. The sweep lives here — not in Cleanup — so an override that forgets to
            // call base, or throws, cannot leak the bag.
            Try(failures, Cleanup);
            if (_children != null)
            {
                for (int i = _children.Count - 1; i >= 0; i--)
                {
                    if (i >= _children.Count) continue;   // a child's teardown released others
                    var child = _children[i].Unlink();
                    if (child != null) Try(failures, child.Dispose);
                }
            }
            Try(failures, () => _disposables?.Dispose());
            GC.SuppressFinalize(this);

            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }

        private static void Try(List<Exception> failures, Action stage)
        {
            try { stage(); }
            catch (Exception ex) { failures.Add(ex); }
        }

        // ---- Child presenters (AttachChild) ----

        private List<ChildAttachment> _children;
        private ChildAttachment _ownerLink;

        bool IComposablePresenter.IsDisposed => _isDisposed;

        ChildAttachment IComposablePresenter.OwnerLink
        {
            get => _ownerLink;
            set => _ownerLink = value;
        }

        void IComposablePresenter.DeactivateTree()
        {
            _dispatcher.Deactivate();
            if (_children == null) return;
            foreach (var attachment in _children.ToArray())
                attachment.Child?.DeactivateTree();
        }

        void IComposablePresenter.InheritServices(IServiceProvider ownerServices)
        {
            if (_servicesBound) return;
            if (_serviceProvider != null && !ReferenceEquals(_serviceProvider, ownerServices))
                throw new InvalidOperationException(
                    GetType().Name + " already resolved framework services (for example Logger or Messages in its constructor), " +
                    "so it cannot use its owner's services. Move that code to OnViewAttached or OnInitialize.");
            _serviceProvider = ownerServices;
            _servicesBound = true;
        }

        /// <summary>
        /// Attaches <paramref name="child"/> to <paramref name="view"/> — a child view this presenter's view
        /// exposes through its interface — and initializes it; this presenter owns the child from now on.
        /// </summary>
        /// <returns>
        /// A handle that releases (disposes) the child for good. Ignore it for a fixed panel: this
        /// presenter releases its remaining children when it is disposed. Keep it for a panel that is
        /// removed while the window stays open, and dispose it before the view removes the control.
        /// Disposing it twice does nothing.
        /// </returns>
        /// <remarks>
        /// <para>Call it from <c>OnInitialize</c> (parameters are available then) or later, e.g. when the
        /// user adds a row. The child is attached and initialized before this method returns
        /// (<c>OnViewAttached</c> → <c>RegisterViewActions</c> → binder → <c>OnInitialize</c>; its
        /// <c>OnInitializeAsync</c> is started, not awaited).</para>
        /// <para>The child uses this presenter's framework services unless it was given its own (for
        /// example by <c>IPresenterFactory</c>). It takes no part in this window's close decision and keeps
        /// its own validation; the parent checks the shared input.</para>
        /// <para>When this presenter is disposed: its actions and its children's stop, its <c>Cleanup</c>
        /// runs, then the children are disposed in reverse order, then its <c>Disposables</c>. A child
        /// whose control is disposed while attached is released too. Views are never disposed here.</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> or <paramref name="view"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">This presenter, the child or the child's control is
        /// disposed (or being disposed).</exception>
        /// <exception cref="InvalidOperationException">This presenter has no view yet; the child is already
        /// attached, initialized or owned; another child of this presenter uses <paramref name="view"/>;
        /// or the child resolved other framework services in its constructor.</exception>
        protected IDisposable AttachChild<TChildView>(ControlPresenterBase<TChildView> child, TChildView view)
            where TChildView : IViewBase
            => AttachChildCore(child, view, () => ((IInitializable)child).Initialize());

        /// <summary>
        /// Attaches a child presenter that needs initialization parameters — typically the input model
        /// the window's panels share — and initializes it with <paramref name="parameters"/>. Same
        /// contract as <see cref="AttachChild{TChildView}(ControlPresenterBase{TChildView}, TChildView)"/>.
        /// </summary>
        protected IDisposable AttachChild<TChildView, TParam>(
            ControlPresenterBase<TChildView, TParam> child, TChildView view, TParam parameters)
            where TChildView : IViewBase
            => AttachChildCore(child, view, () => ((IInitializable<TParam>)child).Initialize(parameters));

        private IDisposable AttachChildCore<TChildView>(PresenterBase<TChildView> child, TChildView view, Action initialize)
            where TChildView : IViewBase
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (_isDisposed) throw new ObjectDisposedException(GetType().Name);
            if (View == null)
                throw new InvalidOperationException(
                    GetType().Name + " has no view attached yet; attach children from OnInitialize or later.");

            IComposablePresenter composable = child;
            IViewAttachable attachable = child;
            if (composable.IsDisposed) throw new ObjectDisposedException(child.GetType().Name);
            if (attachable.IsViewAttached || attachable.IsInitialized || composable.OwnerLink != null)
                throw new InvalidOperationException(
                    child.GetType().Name + " is already attached to a view; a presenter is attached once.");
            if (ViewOwnership.IsClaimed(view))
                throw new InvalidOperationException(
                    "Another live presenter already uses this view; each child needs its own view.");
            if (ViewDisposal.Observer.IsDisposed(view))
                throw new ObjectDisposedException(view.GetType().Name, "The child view is disposed.");
            composable.InheritServices(Services);

            // From here on this presenter owns the child: recorded before any child code runs.
            if (_children == null) _children = new List<ChildAttachment>();
            var attachment = new ChildAttachment(_children, child, view);
            _children.Add(attachment);
            composable.OwnerLink = attachment;

            bool viewDisposed = false;
            var observation = ViewDisposal.Observer.ObserveDisposal(view, () =>
            {
                viewDisposed = true;
                attachment.Dispose();
            });
            ((IPresenterResourceOwner)child).OwnUntilDisposed(observation);
            ((IPresenterResourceOwner)child).OwnUntilDisposed(ViewOwnership.Claim(view, child));

            try
            {
                ((IViewAttacher<TChildView>)child).AttachView(view);
                initialize();
            }
            catch (Exception failure)
            {
                try { attachment.Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }

            if (viewDisposed)
                throw new ObjectDisposedException(view.GetType().Name,
                    "The child view was disposed while its presenter was being attached.");
            return attachment;
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
