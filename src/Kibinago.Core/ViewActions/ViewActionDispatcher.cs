using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Events;
using Kibinago.Logging;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// Dispatches view actions to registered handlers.
    /// Supports optional CanExecute predicates to control action availability.
    /// Automatically triggers ActionExecuted event after executing actions to enable automatic UI updates.
    ///
    /// <para>
    /// <b>Error handling:</b> Exceptions thrown by <c>CanExecute</c> predicates or action handlers are
    /// caught and logged through <see cref="Logger"/>. Payload type mismatches between <c>Dispatch</c>
    /// and the registered <c>Action&lt;T&gt;</c> handler are detected eagerly and logged as warnings,
    /// instead of failing silently.
    /// </para>
    ///
    /// <para>
    /// <b>Middleware:</b> Cross-cutting concerns (audit, performance, authorization, custom error
    /// strategies) can be layered on top via <see cref="Use(IDispatchMiddleware)"/>. When no
    /// middleware is registered, <see cref="Dispatch"/> takes a fast path that allocates no
    /// <see cref="DispatchContext"/> and incurs no additional indirection — simple users pay
    /// zero overhead. Pipeline compilation is cached and invalidated only when middleware is
    /// added.
    /// </para>
    /// </summary>
    public class ViewActionDispatcher
    {
        private class ActionHandlerEntry
        {
            public IViewActionHandler Handler { get; set; }
            public Func<bool> CanExecute { get; set; }

            /// <summary>
            /// Expected payload type for parameterized handlers. <c>null</c> for parameterless handlers.
            /// </summary>
            public Type PayloadType { get; set; }

            /// <summary>True while an asynchronous handler for this action is running.</summary>
            public bool IsRunning { get; set; }
        }

        /// <summary>
        /// Cancelled when the owner (the presenter) is disposed; passed to asynchronous handlers.
        /// </summary>
        internal CancellationToken Lifetime { get; set; }

        private bool _inactive;

        /// <summary>False once the owning presenter is being disposed: nothing is dispatched any more.</summary>
        internal bool IsActive => !_inactive;

        /// <summary>
        /// Stops this dispatcher for good: <see cref="CanDispatch"/> returns false, <see cref="Dispatch"/>
        /// and <see cref="DispatchAsync"/> run nothing (no middleware, no validation errors), and
        /// <see cref="CanExecuteChanged"/> and <see cref="ActionExecuted"/> are no longer raised — also not when an asynchronous handler
        /// that was already running completes. Called when the owning presenter is disposed, so a late
        /// click on a control that outlives its presenter does nothing.
        /// </summary>
        internal void Deactivate() => _inactive = true;

        private void NotifyCanExecuteChanged()
        {
            if (_inactive) return;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private readonly Dictionary<ViewAction, ActionHandlerEntry> _handlers = new Dictionary<ViewAction, ActionHandlerEntry>();
        private ILogger _logger = NullLogger.Instance;

        // Middleware pipeline state.
        // _middlewares is null until the first Use() call — this is the fast-path sentinel.
        // _compiledPipeline is built lazily on first Dispatch and invalidated when middleware is added.
        private List<IDispatchMiddleware> _middlewares;
        private DispatchDelegate _compiledPipeline;

        /// <summary>
        /// Logger used to report handler/predicate failures and payload type mismatches.
        /// Defaults to <see cref="NullLogger.Instance"/>; set to an <see cref="ILogger"/> from
        /// <c>ILoggerFactory.CreateLogger</c> to surface dispatch failures.
        /// </summary>
        /// <remarks>
        /// <see cref="Kibinago.Presenters.PresenterBase{TView}"/> wires this property to the
        /// presenter's logger on first access of its <c>Dispatcher</c> property, so user code typically
        /// does not need to set it.
        /// </remarks>
        public ILogger Logger
        {
            get => _logger;
            set => _logger = value ?? NullLogger.Instance;
        }

        private DispatchValidationMode _validationMode = DispatchValidationMode.Lenient;

        /// <summary>
        /// Controls whether dispatch-time misconfigurations (no registered handler, or a payload
        /// type mismatch) are logged-and-ignored (<see cref="DispatchValidationMode.Lenient"/>,
        /// the default) or throw (<see cref="DispatchValidationMode.Strict"/>). See
        /// <see cref="DispatchValidationMode"/> for the recommended Debug-build usage.
        /// </summary>
        /// <remarks>
        /// This does not affect handler/<c>CanExecute</c> exception handling (always caught and
        /// logged) nor disabled actions (<c>CanExecute</c> false is never an error). Configure it
        /// once via <see cref="Kibinago.ViewActions.IDispatcherConfigurer"/> so it applies before any dispatch.
        /// </remarks>
        public DispatchValidationMode ValidationMode
        {
            get => _validationMode;
            set => _validationMode = value;
        }

        /// <summary>
        /// Raised after an action has been successfully executed.
        /// Subscribe to this event to automatically refresh UI state (e.g., UpdateCanExecuteStates).
        /// </summary>
        public event EventHandler<ActionExecutedEventArgs> ActionExecuted;

        /// <summary>
        /// Raised when CanExecute state may have changed.
        /// Similar to WPF's ICommand.CanExecuteChanged.
        /// Call RaiseCanExecuteChanged() when application state changes outside of action execution.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Registers an action handler without parameters.
        /// </summary>
        /// <param name="actionKey">The action key</param>
        /// <param name="handler">The handler to execute</param>
        /// <param name="canExecute">Optional predicate to determine if the action can execute. Defaults to always true.</param>
        public void Register(ViewAction actionKey, Action handler, Func<bool> canExecute = null)
        {
            RequireKey(actionKey);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[actionKey] = new ActionHandlerEntry
            {
                Handler = new ViewActionHandler(handler),
                CanExecute = canExecute ?? (() => true),
                PayloadType = null
            };
        }

        /// <summary>
        /// Registers an action handler with a typed parameter.
        /// </summary>
        /// <typeparam name="T">The type of the parameter</typeparam>
        /// <param name="actionKey">The action key</param>
        /// <param name="handler">The handler to execute</param>
        /// <param name="canExecute">Optional predicate to determine if the action can execute. Defaults to always true.</param>
        public void Register<T>(ViewAction actionKey, Action<T> handler, Func<bool> canExecute = null)
        {
            RequireKey(actionKey);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[actionKey] = new ActionHandlerEntry
            {
                Handler = new ViewActionHandler<T>(handler),
                CanExecute = canExecute ?? (() => true),
                PayloadType = typeof(T)
            };
        }

        /// <summary>
        /// Registers an asynchronous action handler. While it runs, the action reports
        /// <see cref="CanDispatch"/> = false (bound controls are disabled) and repeated triggers are
        /// ignored. A failure is logged like a synchronous handler's; cancellation caused by the
        /// owner being disposed is silent.
        /// </summary>
        public void Register(ViewAction actionKey, Func<Task> handler, Func<bool> canExecute = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Register(actionKey, _ => handler(), canExecute);
        }

        /// <summary>
        /// Registers an asynchronous action handler that receives a token cancelled when the owner
        /// (the presenter, i.e. its window) is disposed. See <see cref="Register(ViewAction, Func{Task}, Func{bool})"/>.
        /// </summary>
        public void Register(ViewAction actionKey, Func<CancellationToken, Task> handler, Func<bool> canExecute = null)
        {
            RequireKey(actionKey);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[actionKey] = new ActionHandlerEntry
            {
                Handler = new AsyncViewActionHandler(handler),
                CanExecute = canExecute ?? (() => true),
                PayloadType = null
            };
        }

        /// <summary>Registers an asynchronous handler with a typed parameter.</summary>
        public void Register<T>(ViewAction actionKey, Func<T, Task> handler, Func<bool> canExecute = null)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Register<T>(actionKey, (value, _) => handler(value), canExecute);
        }

        /// <summary>Registers an asynchronous handler with a typed parameter and a cancellation token.</summary>
        public void Register<T>(ViewAction actionKey, Func<T, CancellationToken, Task> handler, Func<bool> canExecute = null)
        {
            RequireKey(actionKey);
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[actionKey] = new ActionHandlerEntry
            {
                Handler = new AsyncViewActionHandler<T>(handler),
                CanExecute = canExecute ?? (() => true),
                PayloadType = typeof(T)
            };
        }

        /// <summary>Registers a handler for a typed action; the payload type comes from the key.</summary>
        public void Register<T>(ViewAction<T> actionKey, Action<T> handler, Func<bool> canExecute = null)
            => Register<T>(actionKey.Key, handler, canExecute);

        /// <summary>Registers an async handler for a typed action; the payload type comes from the key.</summary>
        public void Register<T>(ViewAction<T> actionKey, Func<T, Task> handler, Func<bool> canExecute = null)
            => Register<T>(actionKey.Key, handler, canExecute);

        /// <summary>Registers an async, cancellable handler for a typed action; the payload type comes from the key.</summary>
        public void Register<T>(ViewAction<T> actionKey, Func<T, CancellationToken, Task> handler, Func<bool> canExecute = null)
            => Register<T>(actionKey.Key, handler, canExecute);

        /// <summary>Whether the typed action is registered and its canExecute allows it now.</summary>
        public bool CanDispatch<T>(ViewAction<T> actionKey) => CanDispatch(actionKey.Key);

        /// <summary>Dispatches a typed action with its payload.</summary>
        public void Dispatch<T>(ViewAction<T> actionKey, T payload) => Dispatch(actionKey.Key, payload);

        /// <summary>Dispatches a typed action; the task completes when the handler finishes (see <see cref="DispatchAsync(ViewAction, object)"/>).</summary>
        public Task DispatchAsync<T>(ViewAction<T> actionKey, T payload) => DispatchAsync(actionKey.Key, payload);

        /// <summary>
        /// Adds a middleware to the dispatch pipeline. Middleware runs in registration order
        /// (first registered = outermost). The registered handler is always the innermost step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Once any middleware is registered, dispatches take a "slow path" that allocates a
        /// <see cref="DispatchContext"/> per call. Dispatchers with no middleware retain a
        /// fully zero-overhead "fast path".
        /// </para>
        /// <para>
        /// Middleware registration does not propagate to dispatches that have already completed;
        /// the cached pipeline is rebuilt lazily on the next <see cref="Dispatch"/>.
        /// </para>
        /// <para>
        /// Middleware only observes dispatches that pass the <c>CanExecute</c> and payload-type
        /// preconditions. Dispatches that are rejected at the precondition stage (no handler
        /// registered, <c>CanExecute</c> returns false, payload type mismatch) never enter the
        /// pipeline.
        /// </para>
        /// </remarks>
        /// <returns>This dispatcher, to allow chaining.</returns>
        public ViewActionDispatcher Use(IDispatchMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));

            if (_middlewares == null)
            {
                _middlewares = new List<IDispatchMiddleware>();
            }
            _middlewares.Add(middleware);
            _compiledPipeline = null;  // invalidate cache; rebuilt on next Dispatch
            return this;
        }

        /// <summary>
        /// Adds an inline middleware: <c>Use(async (ctx, next) =&gt; { ...; await next(ctx); ... })</c>, or
        /// <c>(ctx, next) =&gt; next(ctx)</c> for a synchronous pass-through. Convenience overload of
        /// <see cref="Use(IDispatchMiddleware)"/>.
        /// </summary>
        /// <param name="middleware">The middleware; returns a task that completes when it has finished.</param>
        /// <returns>This dispatcher, to allow chaining.</returns>
        public ViewActionDispatcher Use(Func<DispatchContext, DispatchDelegate, Task> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            return Use(new InlineMiddleware(middleware));
        }

        private static void RequireKey(ViewAction actionKey)
        {
            if (actionKey.Name == null)
                throw new ArgumentException(
                    "default(ViewAction) is not a valid action key; create keys with ViewAction.Create(...).",
                    nameof(actionKey));
        }

        /// <summary>
        /// Checks if the specified action can be executed.
        /// </summary>
        /// <param name="actionKey">The action key to check</param>
        /// <returns>True if the action can execute; otherwise false</returns>
        public bool CanDispatch(ViewAction actionKey)
        {
            if (_inactive) return false;
            if (actionKey.Name != null && _handlers.TryGetValue(actionKey, out var entry))
            {
                return SafeCanExecute(actionKey, entry);
            }
            return false;
        }

        /// <summary>
        /// Dispatches the specified action with an optional payload.
        /// Only executes if CanDispatch returns true.
        /// Raises ActionExecuted event after successful execution (for an asynchronous handler,
        /// when it completes). Returns immediately; use <see cref="DispatchAsync"/> to await an
        /// asynchronous handler.
        /// </summary>
        /// <remarks>
        /// A handler exception is logged and then propagates like an ordinary WinForms event handler's,
        /// so <c>Application.ThreadException</c> sees it: a synchronous failure is rethrown from this
        /// call (after the action is re-enabled); a failure after an <c>await</c> is posted to the
        /// <see cref="SynchronizationContext"/> current at dispatch, like <c>async void</c> (the thread
        /// pool when there is none). Cancellation stays silent, and a middleware that catches the
        /// exception has handled it.
        /// </remarks>
        /// <param name="actionKey">The action key to dispatch</param>
        /// <param name="payload">Optional payload to pass to the handler</param>
        public void Dispatch(ViewAction actionKey, object payload = null)
        {
            DispatchCore(actionKey, payload, awaitable: false);
        }

        /// <summary>
        /// Dispatches like <see cref="Dispatch"/> and returns a task that completes when the handler
        /// has finished (immediately for synchronous handlers or when nothing runs). A handler
        /// exception faults the task instead of being thrown or posted (it is still logged).
        /// Mainly for tests and for code that must sequence after an action.
        /// </summary>
        public Task DispatchAsync(ViewAction actionKey, object payload = null)
        {
            return DispatchCore(actionKey, payload, awaitable: true);
        }

        /// <summary>How a failure leaves the dispatcher: a faulted task (DispatchAsync), or thrown /
        /// posted (Dispatch).</summary>
        private sealed class Delivery
        {
            public Delivery(bool awaitable, SynchronizationContext context)
            {
                Awaitable = awaitable;
                Context = context;
            }

            public bool Awaitable { get; }
            public SynchronizationContext Context { get; }

            /// <summary>The failure of a dispatch that is still on the caller's stack.</summary>
            public Task FailNow(ExceptionDispatchInfo failure)
            {
                if (Awaitable) return Faulted(failure);
                failure.Throw();
                return null;   // unreachable
            }

            /// <summary>The failure of a dispatch whose caller has returned (after an await).</summary>
            public void FailLater(ExceptionDispatchInfo failure)
            {
                // Like async void: rethrown in the message loop, where Application.ThreadException sees it.
                if (Context != null)
                {
                    try
                    {
                        Context.Post(state => ((ExceptionDispatchInfo)state).Throw(), failure);
                        return;
                    }
                    catch (Exception postFailure)
                    {
                        failure = ExceptionDispatchInfo.Capture(new AggregateException(failure.SourceException, postFailure));
                    }
                }
                ThreadPool.QueueUserWorkItem(state => ((ExceptionDispatchInfo)state).Throw(), failure);
            }

            public static Task Faulted(ExceptionDispatchInfo failure)
            {
                var tcs = new TaskCompletionSource<bool>();
                tcs.SetException(failure.SourceException);
                return tcs.Task;
            }
        }

        private Task DispatchCore(ViewAction actionKey, object payload, bool awaitable)
        {
            if (_inactive) return Task.CompletedTask;

            if (actionKey.Name == null)
            {
                // default(ViewAction): never registered, nothing to run.
                return Task.CompletedTask;
            }

            if (!_handlers.TryGetValue(actionKey, out var entry))
            {
                if (_validationMode == DispatchValidationMode.Strict)
                {
                    throw new InvalidOperationException(
                        $"ViewActionDispatcher: no handler registered for action '{actionKey}'. " +
                        "Register it in RegisterViewActions, or check the ViewAction key for a typo. " +
                        "(Thrown because DispatchValidationMode.Strict is enabled — typically Debug builds only.)");
                }

                _logger.LogDebug(
                    "ViewActionDispatcher: no handler registered for action {ActionKey}",
                    actionKey);
                return Task.CompletedTask;
            }

            if (!SafeCanExecute(actionKey, entry))
            {
                return Task.CompletedTask;
            }

            if (!IsPayloadCompatible(entry, payload))
            {
                if (_validationMode == DispatchValidationMode.Strict)
                {
                    throw new InvalidOperationException(
                        $"ViewActionDispatcher: payload type mismatch dispatching '{actionKey}'. " +
                        $"Expected {entry.PayloadType?.FullName ?? "<none>"}, " +
                        $"got {payload?.GetType().FullName ?? "<null>"}. " +
                        "(Thrown because DispatchValidationMode.Strict is enabled — typically Debug builds only.)");
                }

                _logger.LogWarning(
                    "ViewActionDispatcher: payload type mismatch dispatching {ActionKey}. " +
                    "Expected {ExpectedType}, got {ActualType}. Handler will not run.",
                    actionKey,
                    entry.PayloadType?.FullName ?? "<none>",
                    payload?.GetType().FullName ?? "<null>");
                return Task.CompletedTask;
            }

            // Marked before the handler starts so a re-entrant trigger during its synchronous part
            // is ignored too; cleared when it completes.
            if (entry.Handler.IsAsync) entry.IsRunning = true;
            var delivery = new Delivery(awaitable, SynchronizationContext.Current);

            // Fast path: no middleware registered. Zero allocation, zero indirection.
            // Behavior here MUST stay equivalent to the slow path with no middleware —
            // any change to error handling, ActionExecuted timing, etc. must be mirrored
            // in DispatchThroughPipeline.
            if (_middlewares == null)
            {
                Task pending;
                if (!TryExecuteHandlerDirect(actionKey, entry, payload, out pending, out var failure))
                {
                    entry.IsRunning = false;
                    // The handler threw part-way and may have changed state: re-query CanExecute.
                    NotifyCanExecuteChanged();
                    return failure == null ? Task.CompletedTask : delivery.FailNow(failure);
                }
                return Complete(actionKey, entry, pending, context: null, delivery);
            }

            return DispatchThroughPipeline(actionKey, entry, payload, delivery);
        }

        /// <param name="actionKey">The action being dispatched.</param>
        /// <param name="entry">The registered handler.</param>
        /// <param name="payload">The payload passed to the handler.</param>
        /// <param name="pending">The handler's task (null for a synchronous handler); null when it failed.</param>
        /// <param name="failure">The handler's exception to propagate; null for a cancellation.</param>
        private bool TryExecuteHandlerDirect(ViewAction actionKey, ActionHandlerEntry entry, object payload, out Task pending,
            out ExceptionDispatchInfo failure)
        {
            try
            {
                pending = entry.Handler.Execute(payload, Lifetime);
                failure = null;
                return true;
            }
            catch (OperationCanceledException) when (Lifetime.IsCancellationRequested)
            {
                pending = null;
                failure = null;
                _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed", actionKey);
                return false;
            }
            catch (Exception ex)
            {
                pending = null;
                LogHandlerFailure(actionKey, ex);
                failure = ExceptionDispatchInfo.Capture(ex);
                return false;
            }
        }

        private void LogHandlerFailure(ViewAction actionKey, Exception ex)
        {
            _logger.LogError(
                ex,
                "ViewActionDispatcher: handler for {ActionKey} threw {ExceptionType}",
                actionKey,
                ex.GetType().Name);
        }

        /// <summary>
        /// Finishes a dispatch whose handler started successfully (fast path), or a pipeline run (slow
        /// path). A synchronous handler (null task) or a task that already completed finishes inline;
        /// otherwise the action stays "running" (CanDispatch = false) until the task completes, on the
        /// dispatching thread's context.
        /// </summary>
        /// <param name="actionKey">The dispatched action.</param>
        /// <param name="entry">The registered handler, kept "running" until <paramref name="pending"/> completes.</param>
        /// <param name="pending">The handler's task (null for a synchronous handler), or the pipeline's outcome.</param>
        /// <param name="context">The middleware pipeline's context; null on the fast path.</param>
        /// <param name="delivery">How a failure leaves the dispatcher (faulted task, or thrown / posted).</param>
        private Task Complete(ViewAction actionKey, ActionHandlerEntry entry, Task pending, DispatchContext context,
            Delivery delivery)
        {
            if (pending == null)
            {
                if (!_inactive) ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionKey));
                return Task.CompletedTask;
            }

            if (pending.IsCompleted)
            {
                var failure = Finish(actionKey, entry, pending, context, ranAsync: false);
                return failure == null ? Task.CompletedTask : delivery.FailNow(failure);
            }

            entry.IsRunning = true;      // also a synchronous handler behind a middleware that awaits
            NotifyCanExecuteChanged();   // the action is now running: disable it
            var tracking = AwaitHandler(actionKey, entry, pending, context);
            if (delivery.Awaitable) return tracking;

            // Dispatch's caller has returned: a later failure goes to the dispatching context.
            tracking.ContinueWith(t => delivery.FailLater(ExceptionDispatchInfo.Capture(t.Exception.InnerException)),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return tracking;
        }

        /// <summary>
        /// Completes when the handler has finished and been finished; faults with its failure. Built on
        /// a TaskCompletionSource so that a failure which is an OperationCanceledException still faults
        /// the task instead of cancelling it.
        /// </summary>
        private Task AwaitHandler(ViewAction actionKey, ActionHandlerEntry entry, Task pending, DispatchContext context)
        {
            var result = new TaskCompletionSource<bool>();
            // FinishWhenDone never throws (it returns what failed), so the bridge always completes.
            FinishWhenDone(actionKey, entry, pending, context).ContinueWith(t =>
            {
                var (handlerFailure, subscriberFailure) = t.Status == TaskStatus.RanToCompletion ? t.Result : (null, null);
                var failure = subscriberFailure ?? handlerFailure;
                if (failure != null) result.SetException(failure.SourceException);
                else result.SetResult(true);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return result.Task;
        }

        private async Task<(ExceptionDispatchInfo Handler, ExceptionDispatchInfo Subscriber)> FinishWhenDone(
            ViewAction actionKey, ActionHandlerEntry entry, Task pending, DispatchContext context)
        {
            try
            {
                await pending;   // resumes on the dispatching (UI) thread
            }
            catch
            {
                // Observed and reported in Finish.
            }
            try
            {
                return (Finish(actionKey, entry, pending, context, ranAsync: true), null);
            }
            catch (Exception ex)
            {
                // An ActionExecuted / CanExecuteChanged subscriber threw (even an OperationCanceledException):
                // a failure of this dispatch, never a cancelled or pending task.
                return (null, ExceptionDispatchInfo.Capture(ex));
            }
        }

        /// <summary>Restores the action after its handler ended; returns the failure to propagate
        /// (null for success and cancellation).</summary>
        /// <param name="actionKey">The dispatched action.</param>
        /// <param name="entry">The registered handler.</param>
        /// <param name="completed">The handler's task (fast path) or the pipeline's outcome.</param>
        /// <param name="context">The pipeline's context; null on the fast path.</param>
        /// <param name="ranAsync">The action was disabled while it ran and must be refreshed.</param>
        private ExceptionDispatchInfo Finish(ViewAction actionKey, ActionHandlerEntry entry, Task completed, DispatchContext context,
            bool ranAsync)
        {
            entry.IsRunning = false;

            if (completed.Status == TaskStatus.RanToCompletion)
            {
                // Through middleware, success means the handler ran and did not fail: not short-circuited,
                // not a failure a middleware caught (that one refreshes CanExecute: state may have changed).
                if (context == null || (context.HandlerExecuted && !context.HandlerFailed))
                {
                    if (!_inactive) ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionKey));
                }
                else if (ranAsync || context.HandlerFailed)
                {
                    NotifyCanExecuteChanged();
                }
                return null;
            }

            ExceptionDispatchInfo failure = null;

            if (completed.IsCanceled && Lifetime.IsCancellationRequested)
            {
                _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed", actionKey);
            }
            else if (completed.IsCanceled)
            {
                _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled", actionKey);
            }
            else
            {
                var error = completed.Exception?.InnerExceptions.Count == 1
                    ? completed.Exception.InnerException
                    : completed.Exception;
                if (error is OperationCanceledException && Lifetime.IsCancellationRequested)
                {
                    _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed", actionKey);
                }
                else
                {
                    LogHandlerFailure(actionKey, error);
                    if (context != null && context.Exception == null) context.Exception = error;
                    failure = ExceptionDispatchInfo.Capture(error);
                }
            }

            NotifyCanExecuteChanged();
            return failure;
        }

        private Task DispatchThroughPipeline(ViewAction actionKey, ActionHandlerEntry entry, object payload, Delivery delivery)
        {
            // Compile once per dispatcher; invalidated by Use() calls.
            var pipeline = _compiledPipeline ?? (_compiledPipeline = CompilePipeline());
            var context = new DispatchContext(actionKey, payload, entry.PayloadType, entry.Handler, Lifetime);

            // The pipeline's outcome finishes exactly like a handler's task on the fast path: only a
            // failure that escaped every middleware propagates (one that caught it has handled it;
            // ctx.Exception stays a record).
            return Complete(actionKey, entry, RunPipeline(pipeline, context), context, delivery);
        }

        /// <summary>
        /// Runs the pipeline and returns its outcome; never throws. When a middleware returned without
        /// awaiting an asynchronous handler, the outcome also waits for the handler, whose result then
        /// counts (nobody observed it) unless the pipeline itself failed.
        /// </summary>
        private static Task RunPipeline(DispatchDelegate pipeline, DispatchContext context)
        {
            Task outcome;
            try
            {
                outcome = pipeline(context) ?? Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Thrown synchronously: by a middleware, or by the handler and not caught.
                outcome = Faulted(ex);
            }

            if (outcome.IsCompleted && (context.HandlerTask == null || context.HandlerTask.IsCompleted))
                return Settle(outcome, context);

            var result = new TaskCompletionSource<bool>();
            outcome.ContinueWith(o =>
            {
                var handler = context.HandlerTask;
                if (handler == null || handler.IsCompleted)
                {
                    Transfer(Settle(o, context), result);
                    return;
                }
                handler.ContinueWith(h => Transfer(Settle(o.Status == TaskStatus.RanToCompletion ? h : o, context), result),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return result.Task;
        }

        /// <summary>
        /// An async middleware that lets an OperationCanceledException failure pass turns it into a
        /// cancelled task (C# async semantics). Unless it was the disposal cancellation, the handler
        /// failed (it is on the context): report that, as without middleware.
        /// </summary>
        private static Task Settle(Task outcome, DispatchContext context)
        {
            return outcome.IsCanceled && context.Exception is OperationCanceledException failure
                ? Faulted(failure)
                : outcome;
        }

        private DispatchDelegate CompilePipeline()
        {
            // Terminal step: read the handler off the context and invoke it.
            // Reading from context (not capturing the per-dispatch entry) lets the compiled
            // pipeline be cached and reused across all actions registered on this dispatcher.
            // The failure is recorded as it leaves the handler, so outer middleware (audit, perf)
            // already sees ctx.Exception in its finally block while unwinding.
            DispatchDelegate next = ctx =>
            {
                Task pending;
                try
                {
                    pending = ctx.Handler.Execute(ctx.Payload, ctx.CancellationToken);
                }
                catch (Exception ex)
                {
                    ctx.Exception = ex;
                    ctx.HandlerFailed = true;
                    throw;
                }
                ctx.HandlerExecuted = true;
                if (pending == null || pending.Status == TaskStatus.RanToCompletion) return Task.CompletedTask;
                if (pending.IsFaulted)
                {
                    // Already failed: thrown here like a synchronous failure, so it propagates even through a
                    // middleware that does not await next (an awaiting one catches it the same way).
                    var error = pending.Exception.InnerExceptions.Count == 1 ? pending.Exception.InnerException : pending.Exception;
                    ctx.HandlerFailed = true;
                    if (!(error is OperationCanceledException && ctx.CancellationToken.IsCancellationRequested))
                        ctx.Exception = error;
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
                return ctx.HandlerTask = ObserveHandler(ctx, pending);
            };

            // Build the chain inside-out so that the first-registered middleware ends up
            // outermost. This means global middleware (applied via IDispatcherConfigurer registered
            // in the service provider, which runs before user code) wraps local middleware (added inside RegisterViewActions).
            var middlewares = _middlewares;
            for (int i = middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = middlewares[i];
                var capturedNext = next;
                next = ctx => middleware.InvokeAsync(ctx, capturedNext);
            }

            return next;
        }

        /// <summary>
        /// The handler's task as the pipeline sees it: records a failure on the context before an
        /// awaiting middleware resumes. Bridged so a failure that is an OperationCanceledException stays
        /// a failure.
        /// </summary>
        private static Task ObserveHandler(DispatchContext ctx, Task pending)
        {
            var result = new TaskCompletionSource<bool>();
            pending.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var error = t.Exception.InnerExceptions.Count == 1 ? t.Exception.InnerException : t.Exception;
                    ctx.HandlerFailed = true;
                    if (!(error is OperationCanceledException && ctx.CancellationToken.IsCancellationRequested))
                        ctx.Exception = error;
                    result.SetException(t.Exception.InnerExceptions);
                }
                else if (t.IsCanceled)
                {
                    ctx.HandlerFailed = true;
                    result.SetCanceled();
                }
                else
                {
                    result.SetResult(true);
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return result.Task;
        }

        private static Task Faulted(Exception error)
        {
            var result = new TaskCompletionSource<bool>();
            result.SetException(error);
            return result.Task;
        }

        private static void Transfer(Task from, TaskCompletionSource<bool> to)
        {
            if (from.IsFaulted) to.SetException(from.Exception.InnerExceptions);
            else if (from.IsCanceled) to.SetCanceled();
            else to.SetResult(true);
        }

        private bool SafeCanExecute(ViewAction actionKey, ActionHandlerEntry entry)
        {
            if (entry.IsRunning)
                return false;   // an asynchronous run is in progress: disabled, repeats ignored

            try
            {
                return entry.CanExecute();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ViewActionDispatcher: CanExecute for {ActionKey} threw {ExceptionType}. " +
                    "Treating action as disabled.",
                    actionKey,
                    ex.GetType().Name);
                return false;
            }
        }

        private static bool IsPayloadCompatible(ActionHandlerEntry entry, object payload)
        {
            // Parameterless handler: ignore whatever payload is supplied.
            if (entry.PayloadType == null)
            {
                return true;
            }

            // Reference / nullable types accept null payload.
            if (payload == null)
            {
                return !entry.PayloadType.IsValueType
                    || Nullable.GetUnderlyingType(entry.PayloadType) != null;
            }

            return entry.PayloadType.IsInstanceOfType(payload);
        }

        /// <summary>
        /// Gets all registered action keys (useful for debugging).
        /// </summary>
        public IEnumerable<ViewAction> RegisteredActions => _handlers.Keys;

        /// <summary>
        /// Number of middleware currently registered. Exposed for tests; users typically
        /// don't need to query this.
        /// </summary>
        internal int MiddlewareCount => _middlewares?.Count ?? 0;

        /// <summary>
        /// Raises the CanExecuteChanged event to notify subscribers that CanExecute state may have changed.
        /// Call this method when application state changes outside of action execution.
        /// Similar to WPF's ICommand.RaiseCanExecuteChanged().
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            NotifyCanExecuteChanged();
        }
    }
}
