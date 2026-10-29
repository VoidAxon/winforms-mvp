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
    /// Supports optional CanExecute predicates to control action availability; raises
    /// <see cref="CanExecuteChanged"/> after every handler finishes so bound controls refresh.
    ///
    /// <para>
    /// <b>Error handling:</b> a handler exception is logged and propagates like an ordinary WinForms event
    /// handler's (see <see cref="Dispatch"/>); a <c>CanExecute</c> exception is logged and the action treated
    /// as disabled. Payload type mismatches and unregistered keys are logged and ignored, or throw with
    /// <see cref="DispatchValidationMode.Strict"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Observation:</b> an optional <see cref="Observer"/> receives one <see cref="ActionExecution"/> per
    /// handler that ran — timing, payload, outcome — without being able to change it.
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
        /// and <see cref="DispatchAsync"/> run nothing (no validation errors), and
        /// <see cref="CanExecuteChanged"/> is no longer raised — also not when an asynchronous handler
        /// that was already running completes (its observer is still told). Called when the owning presenter is disposed, so a late
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
        /// once for the application via <see cref="ActionDispatcherOptions"/> (<c>mvp.Actions.StrictValidation()</c>).
        /// </remarks>
        public DispatchValidationMode ValidationMode
        {
            get => _validationMode;
            set => _validationMode = value;
        }

        /// <summary>
        /// Told about every handler that ran, when it has finished (see <see cref="IActionObserver"/>);
        /// <c>null</c> (the default) for none. A presenter's dispatcher takes the application's observer.
        /// </summary>
        public IActionObserver Observer { get; set; }

        /// <summary>The presenter type reported in <see cref="ActionExecution.PresenterType"/>; set by the presenter.</summary>
        internal Type OwnerType { get; set; }

        /// <summary>
        /// Raised when CanExecute state may have changed: after every handler finishes (and when an
        /// asynchronous one starts), and on <see cref="RaiseCanExecuteChanged"/>.
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
        /// Raises CanExecuteChanged after the handler finishes (for an asynchronous handler, when it
        /// completes). Returns immediately; use <see cref="DispatchAsync"/> to await an asynchronous handler.
        /// </summary>
        /// <remarks>
        /// A handler exception is logged and then propagates like an ordinary WinForms event handler's,
        /// so <c>Application.ThreadException</c> sees it: a synchronous failure is rethrown from this
        /// call (after the action is re-enabled); a failure after an <c>await</c> is posted to the
        /// <see cref="SynchronizationContext"/> current at dispatch, like <c>async void</c> (the thread
        /// pool when there is none). Cancellation stays silent.
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
            var run = new Run(actionKey, entry, payload, Observer);

            if (!TryExecuteHandler(run, out var pending, out var failure, out var cancellation))
            {
                // Threw synchronously (or was cancelled): the handler may have changed state, so
                // CanExecute is re-queried.
                End(run, failure == null ? ActionOutcome.Canceled : ActionOutcome.Failed,
                    (Exception)cancellation ?? failure?.SourceException);
                return failure == null ? Task.CompletedTask : delivery.FailNow(failure);
            }

            if (pending == null)
            {
                End(run, ActionOutcome.Succeeded, null);
                return Task.CompletedTask;
            }

            if (pending.IsCompleted)
            {
                var completedFailure = Finish(run, pending);
                return completedFailure == null ? Task.CompletedTask : delivery.FailNow(completedFailure);
            }

            entry.IsRunning = true;
            NotifyCanExecuteChanged();   // the action is now running: disable it
            var tracking = AwaitHandler(run, pending);
            if (delivery.Awaitable) return tracking;

            // Dispatch's caller has returned: a later failure goes to the dispatching context.
            tracking.ContinueWith(t => delivery.FailLater(ExceptionDispatchInfo.Capture(t.Exception.InnerException)),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return tracking;
        }

        /// <summary>One handler run: what to report to the observer when it ends.</summary>
        private sealed class Run
        {
            // Timed only when someone is told: without an observer a dispatch reads no clock.
            private readonly System.Diagnostics.Stopwatch _watch;

            public Run(ViewAction action, ActionHandlerEntry entry, object payload, IActionObserver observer)
            {
                Action = action;
                Entry = entry;
                Payload = payload;
                Observer = observer;   // the one in place when it started, even if replaced meanwhile
                if (observer != null)
                {
                    StartedAt = DateTimeOffset.UtcNow;
                    _watch = System.Diagnostics.Stopwatch.StartNew();
                }
            }

            public ViewAction Action { get; }
            public ActionHandlerEntry Entry { get; }
            public object Payload { get; }
            public IActionObserver Observer { get; }
            public DateTimeOffset StartedAt { get; }

            public TimeSpan Stop()
            {
                if (_watch == null) return TimeSpan.Zero;
                _watch.Stop();
                return _watch.Elapsed;
            }
        }

        /// <param name="run">The run.</param>
        /// <param name="pending">The handler's task (null for a synchronous handler); null when it failed.</param>
        /// <param name="failure">The handler's exception to propagate; null for a cancellation.</param>
        /// <param name="cancellation">The cancellation exception when the disposal cancelled it.</param>
        private bool TryExecuteHandler(Run run, out Task pending, out ExceptionDispatchInfo failure,
            out OperationCanceledException cancellation)
        {
            cancellation = null;
            try
            {
                pending = run.Entry.Handler.Execute(run.Payload, Lifetime);
                failure = null;
                return true;
            }
            catch (OperationCanceledException ex) when (Lifetime.IsCancellationRequested)
            {
                pending = null;
                failure = null;
                cancellation = ex;
                _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed", run.Action);
                return false;
            }
            catch (Exception ex)
            {
                pending = null;
                LogHandlerFailure(run.Action, ex);
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
        /// Completes when the handler has finished and been finished; faults with its failure. Built on
        /// a TaskCompletionSource so that a failure which is an OperationCanceledException still faults
        /// the task instead of cancelling it.
        /// </summary>
        private Task AwaitHandler(Run run, Task pending)
        {
            var result = new TaskCompletionSource<bool>();
            // FinishWhenDone never throws (it returns what failed), so the bridge always completes.
            FinishWhenDone(run, pending).ContinueWith(t =>
            {
                var (handlerFailure, subscriberFailure) = t.Status == TaskStatus.RanToCompletion ? t.Result : (null, null);
                var failure = subscriberFailure ?? handlerFailure;
                if (failure != null) result.SetException(failure.SourceException);
                else result.SetResult(true);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return result.Task;
        }

        private async Task<(ExceptionDispatchInfo Handler, ExceptionDispatchInfo Subscriber)> FinishWhenDone(Run run, Task pending)
        {
            try
            {
                await pending;   // resumes on the dispatching (UI) thread
            }
            catch
            {
                // Observed and reported in Finish.
            }
            ExceptionDispatchInfo handlerFailure = null;
            try
            {
                handlerFailure = Finish(run, pending);
                return (handlerFailure, null);
            }
            catch (Exception ex)
            {
                // A CanExecuteChanged subscriber threw (even an OperationCanceledException): a failure of
                // this dispatch, never a cancelled or pending task.
                return (handlerFailure, ExceptionDispatchInfo.Capture(ex));
            }
        }

        /// <summary>Ends a run whose handler returned a task that has completed; returns the failure to
        /// propagate (null for success and cancellation).</summary>
        private ExceptionDispatchInfo Finish(Run run, Task completed)
        {
            if (completed.Status == TaskStatus.RanToCompletion)
            {
                End(run, ActionOutcome.Succeeded, null);
                return null;
            }

            if (completed.IsCanceled)
            {
                _logger.LogDebug(Lifetime.IsCancellationRequested
                    ? "ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed"
                    : "ViewActionDispatcher: {ActionKey} was cancelled", run.Action);
                End(run, ActionOutcome.Canceled, null);
                return null;
            }

            var error = completed.Exception?.InnerExceptions.Count == 1
                ? completed.Exception.InnerException
                : completed.Exception;
            if (error is OperationCanceledException && Lifetime.IsCancellationRequested)
            {
                _logger.LogDebug("ViewActionDispatcher: {ActionKey} was cancelled because its owner was disposed", run.Action);
                End(run, ActionOutcome.Canceled, error);
                return null;
            }

            LogHandlerFailure(run.Action, error);
            End(run, ActionOutcome.Failed, error);
            return ExceptionDispatchInfo.Capture(error);
        }

        /// <summary>
        /// The end of every run: the action is enabled again, bound controls refresh (CanExecuteChanged),
        /// then the observer is told — even when a CanExecuteChanged subscriber throws.
        /// </summary>
        private void End(Run run, ActionOutcome outcome, Exception exception)
        {
            var elapsed = run.Stop();
            run.Entry.IsRunning = false;
            try
            {
                NotifyCanExecuteChanged();
            }
            finally
            {
                Report(run, elapsed, outcome, exception);
            }
        }

        private void Report(Run run, TimeSpan elapsed, ActionOutcome outcome, Exception exception)
        {
            var observer = run.Observer;
            if (observer == null) return;
            try
            {
                observer.OnExecuted(new ActionExecution(run.Action, run.Payload, OwnerType, run.StartedAt, elapsed,
                    run.Entry.Handler.IsAsync, outcome, exception));
            }
            catch (Exception ex)
            {
                // Never the handler's outcome: logged (and a throwing logger is ignored too).
                try
                {
                    _logger.LogError(ex, "ViewActionDispatcher: the action observer {ObserverType} threw for {ActionKey}",
                        observer.GetType().Name, run.Action);
                }
                catch
                {
                }
            }
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
