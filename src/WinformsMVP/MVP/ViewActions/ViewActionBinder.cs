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
        private struct ActiveSubscription
        {
            public Action Unsubscribe;
        }

        private readonly List<ActionBinding> _bindings = new List<ActionBinding>();
        private readonly Dictionary<Type, Func<Component, Delegate, Action>> _bindingStrategies
            = new Dictionary<Type, Func<Component, Delegate, Action>>();
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

            // ComboBox - fires only on user-committed selection, not on programmatic SelectedIndex changes
            RegisterStrategy<ComboBox>(
                (c, h) => c.SelectionChangeCommitted += (EventHandler)h,
                (c, h) => c.SelectionChangeCommitted -= (EventHandler)h);

            // NumericUpDown - fires on value change (spin buttons, typing, AND programmatic Value
            // assignment). Unlike ComboBox there is no user-committed-only event, so initializing or
            // refilling Value in code will also trigger the action; suppress around such assignments
            // (Unbind/Bind or a guard flag) if that matters.
            RegisterStrategy<NumericUpDown>(
                (c, h) => c.ValueChanged += (EventHandler)h,
                (c, h) => c.ValueChanged -= (EventHandler)h);

            // DateTimePicker - fires on ValueChanged (user pick or programmatic Value set). Same
            // caveat as NumericUpDown: no committed-only event, so assigning Value in code triggers too.
            RegisterStrategy<DateTimePicker>(
                (c, h) => c.ValueChanged += (EventHandler)h,
                (c, h) => c.ValueChanged -= (EventHandler)h);

            // MonthCalendar - DateChanged uses DateRangeEventHandler (not EventHandler), so register
            // via the subscribe overload and adapt the delegate. Same caveat as the other value
            // controls: programmatic SetDate / SelectionRange assignment also triggers.
            RegisterStrategy<MonthCalendar>((c, h) =>
            {
                DateRangeEventHandler d = (s, e) => ((EventHandler)h)(s, e);
                c.DateChanged += d;
                return () => c.DateChanged -= d;
            });

            // ToolStripMenuItem, ToolStripButton, ToolStripDropDownButton, ToolStripStatusLabel
            RegisterStrategy<ToolStripItem>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);

            // Default fallback for any Control with Click event
            RegisterStrategy<Control>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);
        }

        /// <summary>
        /// Registers a binding strategy for controls of type <typeparamref name="T"/> whose trigger
        /// event is a plain <see cref="EventHandler"/> (Click, CheckedChanged, ValueChanged, ...).
        /// </summary>
        public void RegisterStrategy<T>(Action<T, Delegate> attacher, Action<T, Delegate> detacher) where T : Component
        {
            if (attacher == null) throw new ArgumentNullException(nameof(attacher));
            if (detacher == null) throw new ArgumentNullException(nameof(detacher));

            RegisterStrategy<T>((control, handler) =>
            {
                attacher(control, handler);
                return () => detacher(control, handler);
            });
        }

        /// <summary>
        /// Registers a binding strategy for controls of type <typeparamref name="T"/>, subscribing to
        /// the trigger event and returning an unsubscribe callback. Use this overload for controls
        /// whose event uses a non-<see cref="EventHandler"/> delegate (e.g. MonthCalendar's
        /// <c>DateChanged</c>): adapt the delegate inside, and the returned closure captures the exact
        /// subscription so <see cref="Unbind"/> can detach it cleanly.
        /// </summary>
        public void RegisterStrategy<T>(Func<T, Delegate, Action> subscribe) where T : Component
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            _bindingStrategies[typeof(T)] = (comp, handler) => subscribe((T)comp, handler);
        }

        public void Add(ViewAction actionKey, params Component[] controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            AddBinding(new ActionBinding(actionKey, controls));
        }

        /// <summary>
        /// A control drives exactly one action: a second binding would silently replace the first
        /// at <c>Bind</c> time, and the control's Enabled state would follow whichever action the
        /// refresh visited last. Reject it where the mistake is made.
        /// </summary>
        private void AddBinding(ActionBinding binding)
        {
            if (binding.ActionKey.Name == null)
                throw new ArgumentException(
                    "default(ViewAction) is not a valid action key; create keys with ViewAction.Create(...).");
            foreach (var component in binding.Components)
            {
                if (component == null)
                    throw new ArgumentException("Controls must not contain null.");
                foreach (var existing in _bindings)
                {
                    if (Array.IndexOf(existing.Components, component) >= 0)
                        throw new InvalidOperationException(
                            $"{DescribeComponent(component)} is already bound to action '{existing.ActionKey}'; " +
                            $"it cannot also be bound to '{binding.ActionKey}'. A control triggers one action.");
                }
            }
            _bindings.Add(binding);
        }

        private static string DescribeComponent(Component component)
            => component is Control c && !string.IsNullOrEmpty(c.Name) ? $"Control '{c.Name}'"
             : component is ToolStripItem t && !string.IsNullOrEmpty(t.Name) ? $"Item '{t.Name}'"
             : component.GetType().Name;

        /// <summary>
        /// Binds a control whose current value is captured at trigger time and forwarded as the
        /// action payload. The value is read from <paramref name="valueSelector"/> when the control
        /// fires — flowing to a <c>Register&lt;TValue&gt;</c> handler (implicit pattern) or carried on a
        /// value-bearing <see cref="ActionRequestEventArgs{T}"/> via <see cref="ActionTriggered"/>
        /// (explicit pattern). The presenter never touches the control.
        /// </summary>
        /// <typeparam name="TControl">The control type (inferred from <paramref name="control"/>).</typeparam>
        /// <typeparam name="TValue">The payload type (inferred from <paramref name="valueSelector"/>).</typeparam>
        /// <param name="actionKey">The action to dispatch.</param>
        /// <param name="control">The control to bind.</param>
        /// <param name="valueSelector">Reads the payload value off the control when it triggers.</param>
        public void Add<TControl, TValue>(ViewAction actionKey, TControl control, Func<TControl, TValue> valueSelector)
            where TControl : Component
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));

            AddBinding(new ActionBinding(
                actionKey,
                new Component[] { control },
                comp => valueSelector((TControl)comp)));
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
        private void Bind(Action<ViewAction, object> onActionTriggered)
        {
            Unbind();

            _actionToControls.Clear();
            var actionMap = new Dictionary<object, ActionBinding>();

            foreach (var binding in _bindings)
            {
                if (!_actionToControls.ContainsKey(binding.ActionKey))
                {
                    _actionToControls[binding.ActionKey] = new List<Component>();
                }

                foreach (var component in binding.Components)
                {
                    actionMap[component] = binding;
                    _actionToControls[binding.ActionKey].Add(component);
                }
            }

            Delegate handler = new EventHandler((sender, args) =>
            {
                if (actionMap.TryGetValue(sender, out var binding))
                {
                    var key = binding.ActionKey;

                    // Read the payload off the triggering control, if this binding carries a value.
                    object payload = binding.ValueSelector != null
                        ? binding.ValueSelector((Component)sender)
                        : null;

                    // Always raise ActionTriggered event (for explicit event-based pattern).
                    // Value bindings raise a value-carrying ActionRequestEventArgs<T> so the payload
                    // survives the explicit hop through the view's ActionRequest event.
                    ActionTriggered?.Invoke(this, binding.ValueSelector != null
                        ? new ActionRequestEventArgs<object>(key, payload)
                        : new ActionRequestEventArgs(key));

                    // Auto-detect mode: Check if explicit event handlers are registered
                    bool hasExplicitHandlers = ActionTriggered != null &&
                                              ActionTriggered.GetInvocationList().Length > 0;

                    if (!hasExplicitHandlers)
                    {
                        // Implicit pattern: No explicit handlers, use callback
                        onActionTriggered?.Invoke(key, payload);
                    }
                    // Explicit pattern: Has explicit handlers, skip callback to prevent double-dispatch
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
            if (dispatcher == null) throw new ArgumentNullException(nameof(dispatcher));

            // Save the old dispatcher so we can clean up its subscriptions after Unbind().
            // Unbind() uses _dispatcher; if we overwrote it first the old dispatcher's events
            // would never be removed on a re-bind.
            var oldDispatcher = _dispatcher;
            _dispatcher = dispatcher;

            // Rebind controls (calls Unbind() internally, which now harmlessly attempts to
            // remove events from the new dispatcher — that's a no-op since they haven't been
            // added yet).
            Bind((action, payload) => dispatcher.Dispatch(action, payload));

            // Remove subscriptions from the old dispatcher that Unbind() missed.
            if (oldDispatcher != null)
            {
                oldDispatcher.ActionExecuted -= OnActionExecuted;
                oldDispatcher.CanExecuteChanged -= OnCanExecuteChanged;
            }

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
                if (control.InvokeRequired)
                    control.BeginInvoke(new Action(() => control.Enabled = enabled));
                else
                    control.Enabled = enabled;
            }
            else if (component is ToolStripItem toolStripItem)
            {
                var owner = toolStripItem.GetCurrentParent();
                if (owner != null && owner.InvokeRequired)
                    owner.BeginInvoke(new Action(() => toolStripItem.Enabled = enabled));
                else
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
                sub.Unsubscribe();
            }
            _activeSubscriptions.Clear();
        }

        private void ApplyBindingStrategy(Component control, Delegate handler)
        {
            Type currentType = control.GetType();
            while (currentType != null && currentType != typeof(object))
            {
                if (_bindingStrategies.TryGetValue(currentType, out var subscribe))
                {
                    Action unsubscribe = subscribe(control, handler);
                    _activeSubscriptions.Add(new ActiveSubscription { Unsubscribe = unsubscribe });
                    return;
                }
                currentType = currentType.BaseType;
            }
        }

        IEnumerator<ActionBinding> IEnumerable<ActionBinding>.GetEnumerator() => _bindings.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ActionBinding>)this).GetEnumerator();
    }
}
