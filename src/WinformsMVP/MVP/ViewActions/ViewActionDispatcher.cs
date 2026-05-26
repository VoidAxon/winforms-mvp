using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Logger used to report handler/predicate failures and payload type mismatches.
        /// Defaults to <see cref="NullLogger.Instance"/>; set to an <see cref="ILogger"/> from
        /// <c>ILoggerFactory.CreateLogger</c> to surface dispatch failures.
        /// </summary>
        /// <remarks>
        /// <see cref="WinformsMVP.Core.Presenters.PresenterBase{TView}"/> wires this property to the
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
        public event EventHandler<ViewAction> ActionExecuted;

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

            try
            {
                entry.Handler.Execute(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "ViewActionDispatcher: handler for {ActionKey} threw {ExceptionType}",
                    actionKey,
                    ex.GetType().Name);
                return;
            }

            ActionExecuted?.Invoke(this, actionKey);
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
