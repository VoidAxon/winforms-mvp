using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using WinformsMVP.Common.Events;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Binds UI controls to view actions with support for dynamic enable/disable based on CanExecute.
    /// When bound to a ViewActionDispatcher, automatically updates control states after each action execution.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ViewActionBinder supports two usage patterns:
    /// </para>
    ///
    /// <para>
    /// <b>Pattern 1: Implicit Binding (via Dispatcher)</b>
    /// <code>
    /// // Presenter
    /// Dispatcher.Register(CommonActions.Save, OnSave);
    /// View.ActionBinder.Bind(Dispatcher);  // Implicit connection
    /// </code>
    /// </para>
    ///
    /// <para>
    /// <b>Pattern 2: Explicit Event-Based (via ActionTriggered)</b>
    /// <code>
    /// // View
    /// public event EventHandler&lt;ActionRequestEventArgs&gt; ActionRequest;
    /// _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
    ///
    /// // Presenter
    /// View.ActionRequest += OnActionRequest;  // Explicit subscription
    /// </code>
    /// </para>
    /// </remarks>
    public class ViewActionBinder : IViewActionBinder, IEnumerable<ActionBinding>
    {
        private struct BindingStrategy
        {
            public Action<Component, Delegate> Attacher;
            public Action<Component, Delegate> Detacher;
        }

        private struct ActiveSubscription
        {
            public Component Control;
            public Delegate Handler;
            public Action<Component, Delegate> Detacher;
        }

        private readonly List<ActionBinding> _bindings = new List<ActionBinding>();
        private readonly Dictionary<Type, BindingStrategy> _bindingStrategies
            = new Dictionary<Type, BindingStrategy>();
        private readonly List<ActiveSubscription> _activeSubscriptions
            = new List<ActiveSubscription>();
        private readonly Dictionary<ViewAction, List<Component>> _actionToControls = new Dictionary<ViewAction, List<Component>>();
        private ViewActionDispatcher _dispatcher;

        /// <summary>
        /// Raised when an action is triggered from a bound control.
        /// This event enables explicit event-based handling as an alternative to Bind(dispatcher).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this event when you prefer explicit event subscriptions over implicit dispatcher binding:
        /// </para>
        /// <code>
        /// // View interface
        /// public interface IMyView : IWindowView
        /// {
        ///     event EventHandler&lt;ActionRequestEventArgs&gt; ActionRequest;
        /// }
        ///
        /// // View implementation
        /// _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
        ///
        /// // Presenter
        /// View.ActionRequest += (s, e) =>
        /// {
        ///     if (e.ActionKey == CommonActions.Save) OnSave();
        /// };
        /// </code>
        /// </remarks>
        public event EventHandler<ActionRequestEventArgs> ActionTriggered;

        public ViewActionBinder()
        {
            RegisterDefaultStrategies();
        }

        private void RegisterDefaultStrategies()
        {
            // CheckBox - supports both Click and CheckedChanged
            RegisterStrategy<CheckBox>(
                (c, h) => c.CheckedChanged += (EventHandler)h,
                (c, h) => c.CheckedChanged -= (EventHandler)h);

            // RadioButton - supports both Click and CheckedChanged
            RegisterStrategy<RadioButton>(
                (c, h) => c.CheckedChanged += (EventHandler)h,
                (c, h) => c.CheckedChanged -= (EventHandler)h);

            // Button (regular buttons, not CheckBox/RadioButton)
            RegisterStrategy<ButtonBase>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);

            // ToolStripMenuItem, ToolStripButton, ToolStripDropDownButton, ToolStripStatusLabel
            RegisterStrategy<ToolStripItem>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);

            // Default fallback for any Control with Click event
            RegisterStrategy<Control>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);
        }

        public void RegisterStrategy<T>(Action<T, Delegate> attacher, Action<T, Delegate> detacher) where T : Component
        {
            _bindingStrategies[typeof(T)] = new BindingStrategy
            {
                Attacher = (comp, handler) => attacher((T)comp, handler),
                Detacher = (comp, handler) => detacher((T)comp, handler)
            };
        }

        public void Add(ViewAction actionKey, params Component[] controls)
        {
            _bindings.Add(new ActionBinding(actionKey, controls));
        }

        /// <summary>
        /// Binds all registered controls to their actions.
        /// Automatically detects whether to use implicit or explicit pattern based on ActionTriggered event subscribers.
        /// </summary>
        /// <param name="onActionTriggered">Callback when an action is triggered (used in implicit pattern)</param>
        /// <remarks>
        /// <para>
        /// <b>Automatic Mode Detection:</b>
        /// </para>
        /// <list type="bullet">
        /// <item><b>Explicit Pattern:</b> If ActionTriggered event has subscribers, only the event fires (callback skipped to prevent double-dispatch)</item>
        /// <item><b>Implicit Pattern:</b> If ActionTriggered event has no subscribers, only the callback fires</item>
        /// </list>
        /// </remarks>
        private void Bind(Action<ViewAction> onActionTriggered)
        {
            Unbind();

            _actionToControls.Clear();
            var actionMap = new Dictionary<object, ViewAction>();

            foreach (var binding in _bindings)
            {
                if (!_actionToControls.ContainsKey(binding.ActionKey))
                {
                    _actionToControls[binding.ActionKey] = new List<Component>();
                }

                foreach (var component in binding.Components)
                {
                    actionMap[component] = binding.ActionKey;
                    _actionToControls[binding.ActionKey].Add(component);
                }
            }

            Delegate handler = new EventHandler((sender, args) =>
            {
                if (actionMap.TryGetValue(sender, out var key))
                {
                    // Record the triggering component for the duration of the synchronous
                    // dispatch (a fact about the input event; consumed by anchored feedback).
                    // Save/restore keeps nested dispatches correct.
                    var previousTrigger = InteractionSource.Swap(sender as Component);
                    try
                    {
                        // Always raise ActionTriggered event (for explicit event-based pattern)
                        ActionTriggered?.Invoke(this, new ActionRequestEventArgs(key));

                        // Auto-detect mode: Check if explicit event handlers are registered
                        bool hasExplicitHandlers = ActionTriggered != null &&
                                                  ActionTriggered.GetInvocationList().Length > 0;

                        if (!hasExplicitHandlers)
                        {
                            // Implicit pattern: No explicit handlers, use callback
                            onActionTriggered?.Invoke(key);
                        }
                        // Explicit pattern: Has explicit handlers, skip callback to prevent double-dispatch
                    }
                    finally
                    {
                        InteractionSource.Swap(previousTrigger);
                    }
                }
            });

            foreach (var control in actionMap.Keys)
            {
                ApplyBindingStrategy((Component)control, handler);
            }
        }

        /// <summary>
        /// Binds all registered controls to trigger ActionTriggered event only.
        /// Typically used for explicit event-based pattern, but can also work with implicit pattern if Bind(dispatcher) is called later.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This enables the explicit event-based pattern:
        /// </para>
        /// <code>
        /// // View
        /// _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
        /// _binder.Bind();  // ✅ Event-only binding (no callback)
        ///
        /// // Presenter
        /// View.ActionRequest += OnActionRequest;
        /// </code>
        /// <para>
        /// Note: The automatic mode detection still applies. If ActionTriggered has subscribers,
        /// explicit pattern is used. Otherwise, actions won't dispatch anywhere (unless Bind(dispatcher) is called later).
        /// </para>
        /// </remarks>
        public void Bind()
        {
            Bind(onActionTriggered: null);
        }

        /// <summary>
        /// Binds all registered controls to a ViewActionDispatcher.
        /// This overload enables automatic CanExecute support.
        /// After binding, UI state will automatically update when:
        /// 1. Actions are executed (ActionExecuted event)
        /// 2. RaiseCanExecuteChanged() is called (CanExecuteChanged event)
        /// </summary>
        /// <param name="dispatcher">The dispatcher to bind to</param>
        public void Bind(ViewActionDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            // Important: Call Bind first (which calls Unbind internally)
            // Then subscribe to events after, otherwise Unbind will unsubscribe them
            Bind(action => dispatcher.Dispatch(action));

            // Subscribe to both events for automatic UI refresh
            _dispatcher.ActionExecuted += OnActionExecuted;
            _dispatcher.CanExecuteChanged += OnCanExecuteChanged;

            UpdateCanExecuteStates();
        }

        /// <summary>
        /// Called automatically after an action is executed.
        /// Refreshes the enabled state of all bound controls.
        /// </summary>
        private void OnActionExecuted(object sender, ActionExecutedEventArgs e)
        {
            UpdateCanExecuteStates();
        }

        /// <summary>
        /// Called automatically when RaiseCanExecuteChanged() is invoked.
        /// Refreshes the enabled state of all bound controls.
        /// </summary>
        private void OnCanExecuteChanged(object sender, EventArgs e)
        {
            UpdateCanExecuteStates();
        }

        /// <summary>
        /// Updates the enabled state of all bound controls based on their action's CanExecute.
        /// When using Bind(dispatcher), this is called automatically after each action execution.
        /// Only call manually if state changes outside of action handlers (e.g., from view events).
        /// </summary>
        public void UpdateCanExecuteStates()
        {
            if (_dispatcher == null)
                return;

            foreach (var kvp in _actionToControls)
            {
                var canExecute = _dispatcher.CanDispatch(kvp.Key);

                foreach (var component in kvp.Value)
                {
                    SetControlEnabled(component, canExecute);
                }
            }
        }

        private void SetControlEnabled(Component component, bool enabled)
        {
            if (component is Control control)
            {
                control.Enabled = enabled;
            }
            else if (component is ToolStripItem toolStripItem)
            {
                toolStripItem.Enabled = enabled;
            }
        }

        public void Unbind()
        {
            // Unsubscribe from dispatcher events
            if (_dispatcher != null)
            {
                _dispatcher.ActionExecuted -= OnActionExecuted;
                _dispatcher.CanExecuteChanged -= OnCanExecuteChanged;
            }

            foreach (var sub in _activeSubscriptions)
            {
                sub.Detacher(sub.Control, sub.Handler);
            }
            _activeSubscriptions.Clear();
        }

        private void ApplyBindingStrategy(Component control, Delegate handler)
        {
            Type currentType = control.GetType();
            while (currentType != null && currentType != typeof(object))
            {
                if (_bindingStrategies.TryGetValue(currentType, out var strategy))
                {
                    Delegate specificHandler = handler;
                    strategy.Attacher(control, specificHandler);
                    _activeSubscriptions.Add(new ActiveSubscription
                    {
                        Control = control,
                        Handler = specificHandler,
                        Detacher = strategy.Detacher
                    });
                    return;
                }
                currentType = currentType.BaseType;
            }
        }

        IEnumerator<ActionBinding> IEnumerable<ActionBinding>.GetEnumerator() => _bindings.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ActionBinding>)this).GetEnumerator();
    }
}
