using System;
using System.Collections.Generic;
using WinformsMVP.Common.Events;
using WinformsMVP.Logging;

namespace WinformsMVP.MVP.ViewActions
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
        /// <see cref="WinformsMVP.MVP.Presenters.PresenterBase{TView}"/> wires this property to the
        /// presenter's logger on first access of its <c>Dispatcher</c> property, so user code typically
        /// does not need to set it.
        /// </remarks>
        public ILogger Logger
        {
            get => _logger;
            set => _logger = value ?? NullLogger.Instance;
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
            if (actionKey != null)
            {
                _handlers[actionKey] = new ActionHandlerEntry
                {
                    Handler = new ViewActionHandler(handler),
                    CanExecute = canExecute ?? (() => true),
                    PayloadType = null
                };
            }
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
            if (actionKey != null)
            {
                _handlers[actionKey] = new ActionHandlerEntry
                {
                    Handler = new ViewActionHandler<T>(handler),
                    CanExecute = canExecute ?? (() => true),
                    PayloadType = typeof(T)
                };
            }
        }

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
        /// Adds an inline middleware as a lambda. Convenience overload of
        /// <see cref="Use(IDispatchMiddleware)"/>.
        /// </summary>
        /// <returns>This dispatcher, to allow chaining.</returns>
        public ViewActionDispatcher Use(Action<DispatchContext, DispatchDelegate> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            return Use(new InlineMiddleware(middleware));
        }

        /// <summary>
        /// Checks if the specified action can be executed.
        /// </summary>
        /// <param name="actionKey">The action key to check</param>
        /// <returns>True if the action can execute; otherwise false</returns>
        public bool CanDispatch(ViewAction actionKey)
        {
            if (actionKey != null && _handlers.TryGetValue(actionKey, out var entry))
            {
                return SafeCanExecute(actionKey, entry);
            }
            return false;
        }

        /// <summary>
        /// Dispatches the specified action with an optional payload.
        /// Only executes if CanDispatch returns true.
        /// Raises ActionExecuted event after successful execution.
        /// </summary>
        /// <param name="actionKey">The action key to dispatch</param>
        /// <param name="payload">Optional payload to pass to the handler</param>
        public void Dispatch(ViewAction actionKey, object payload = null)
        {
            if (actionKey == null)
            {
                return;
            }

            if (!_handlers.TryGetValue(actionKey, out var entry))
            {
                _logger.LogDebug(
                    "ViewActionDispatcher: no handler registered for action {ActionKey}",
                    actionKey);
                return;
            }

            if (!SafeCanExecute(actionKey, entry))
            {
                return;
            }

            if (!IsPayloadCompatible(entry, payload))
            {
                _logger.LogWarning(
                    "ViewActionDispatcher: payload type mismatch dispatching {ActionKey}. " +
                    "Expected {ExpectedType}, got {ActualType}. Handler will not run.",
                    actionKey,
                    entry.PayloadType?.FullName ?? "<none>",
                    payload?.GetType().FullName ?? "<null>");
                return;
            }

            // Fast path: no middleware registered. Zero allocation, zero indirection.
            // Behavior here MUST stay equivalent to the slow path with no middleware —
            // any change to error handling, ActionExecuted timing, etc. must be mirrored
            // in DispatchThroughPipeline.
            if (_middlewares == null)
            {
                if (TryExecuteHandlerDirect(actionKey, entry, payload))
                {
                    ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionKey));
                }
                return;
            }

            DispatchThroughPipeline(actionKey, entry, payload);
        }

        private bool TryExecuteHandlerDirect(ViewAction actionKey, ActionHandlerEntry entry, object payload)
        {
            try
            {
                entry.Handler.Execute(payload);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ViewActionDispatcher: handler for {ActionKey} threw {ExceptionType}",
                    actionKey,
                    ex.GetType().Name);
                return false;
            }
        }

        private void DispatchThroughPipeline(ViewAction actionKey, ActionHandlerEntry entry, object payload)
        {
            // Compile once per dispatcher; invalidated by Use() calls.
            var pipeline = _compiledPipeline ?? (_compiledPipeline = CompilePipeline());

            var context = new DispatchContext(actionKey, payload, entry.PayloadType, entry.Handler);

            try
            {
                pipeline(context);
            }
            catch (Exception ex)
            {
                // Last-resort safety net: no middleware caught the exception.
                // Equivalent to the fast path's behavior — log and swallow.
                _logger.LogError(
                    ex,
                    "ViewActionDispatcher: handler for {ActionKey} threw {ExceptionType}",
                    actionKey,
                    ex.GetType().Name);
                context.Exception = ex;
            }

            // Only raise ActionExecuted if the handler ran to completion AND no exception
            // is recorded (either thrown and uncaught, or recorded by a middleware that
            // chose to absorb it but flag it).
            if (context.HandlerExecuted && context.Exception == null)
            {
                ActionExecuted?.Invoke(this, new ActionExecutedEventArgs(actionKey));
            }
        }

        private DispatchDelegate CompilePipeline()
        {
            // Terminal step: read the handler off the context and invoke it.
            // Reading from context (not capturing the per-dispatch entry) lets the compiled
            // pipeline be cached and reused across all actions registered on this dispatcher.
            DispatchDelegate next = ctx =>
            {
                ctx.Handler.Execute(ctx.Payload);
                ctx.HandlerExecuted = true;
            };

            // Build the chain inside-out so that the first-registered middleware ends up
            // outermost. This means global middleware (applied via PlatformServices.ConfigureDispatcher,
            // which runs before user code) wraps local middleware (added inside RegisterViewActions).
            var middlewares = _middlewares;
            for (int i = middlewares.Count - 1; i >= 0; i--)
            {
                var middleware = middlewares[i];
                var capturedNext = next;
                next = ctx => middleware.Invoke(ctx, capturedNext);
            }

            return next;
        }

        private bool SafeCanExecute(ViewAction actionKey, ActionHandlerEntry entry)
        {
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
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
