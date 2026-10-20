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
    /// <b>Pattern 2: Explicit Event-Based (via ActionTriggered)</b> — e.g. for migrating a legacy Form
    /// <code>
    /// // View: no binder for the framework; event-only binding, forwarded to the view's own event
    /// public IViewActionBinder ActionBinder => null;
    /// public event EventHandler&lt;ActionRequestEventArgs&gt; ActionRequest;
    /// _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
    /// _binder.Bind();
    ///
    /// // Presenter
    /// View.ActionRequest += OnViewActionTriggered;  // Explicit subscription
    /// </code>
    /// </para>
    ///
    /// <para>
    /// Routing is decided by which <c>Bind</c> overload runs, never by who listens:
    /// <see cref="Bind(ViewActionDispatcher)"/> always dispatches; <see cref="Bind()"/> only raises
    /// <see cref="ActionTriggered"/>. Subscribing to <see cref="ActionTriggered"/> never changes routing.
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
        private Action<ViewAction, object> _onActionTriggered;   // dispatch callback of the current Bind; null = events only
        private bool _bound;
        private readonly Dictionary<ViewAction, ViewActionCommand> _commands = new Dictionary<ViewAction, ViewActionCommand>();

        /// <summary>
        /// Raised when an action is triggered from a bound control. With <see cref="Bind(ViewActionDispatcher)"/>
        /// it is a notification raised after the dispatch (status bar, logging, ...); with
        /// <see cref="Bind()"/> it is the only output, for the explicit event-based pattern.
        /// Subscribing never changes whether actions reach the dispatcher.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Explicit event-based pattern (the view returns a null <c>ActionBinder</c> and calls
        /// <see cref="Bind()"/> itself):
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
        /// Binds a control to a typed action: <paramref name="valueSelector"/> must produce the
        /// key's payload type, so a mismatch with the presenter's <c>Register(key, (T v) =&gt; ...)</c>
        /// is a compile error rather than a dispatch-time one.
        /// </summary>
        public void Add<TControl, T>(ViewAction<T> actionKey, TControl control, Func<TControl, T> valueSelector)
            where TControl : Component
            => Add<TControl, T>(actionKey.Key, control, valueSelector);

        /// <summary>
        /// The <see cref="System.Windows.Input.ICommand"/> for <paramref name="actionKey"/> — e.g. for
        /// WinForms' <c>ButtonBase.Command</c> / <c>ToolStripItem.Command</c> (.NET 7+). Executing it
        /// triggers the action like a bound control: it dispatches once the binder is bound to a
        /// dispatcher, and raises <see cref="ActionTriggered"/>. CanExecute follows the action's
        /// canExecute (false before binding). One instance per key. Give a control either a
        /// <c>Command</c> or an <c>Add</c> binding for an action, not both, or it triggers twice.
        /// Create it on the UI thread (e.g. in the form constructor): CanExecuteChanged is raised there.
        /// </summary>
        public System.Windows.Input.ICommand CommandFor(ViewAction actionKey)
            => GetCommand(actionKey, parameter => parameter);

        /// <summary>
        /// The <see cref="System.Windows.Input.ICommand"/> for a typed action; the command parameter
        /// is the payload and must be a <typeparamref name="T"/> (null passes <c>default(T)</c>).
        /// </summary>
        public System.Windows.Input.ICommand CommandFor<T>(ViewAction<T> actionKey)
            => GetCommand(actionKey.Key, parameter =>
            {
                if (parameter == null) return default(T);
                if (parameter is T) return parameter;
                throw new ArgumentException(
                    $"Command for '{actionKey.Name}' expects a {typeof(T).Name} parameter, got {parameter.GetType().Name}.",
                    nameof(parameter));
            });

        private ViewActionCommand GetCommand(ViewAction actionKey, Func<object, object> payload)
        {
            if (actionKey.Name == null)
                throw new ArgumentException(
                    "default(ViewAction) is not a valid action key; create keys with ViewAction.Create(...).");
            if (!_commands.TryGetValue(actionKey, out var command))
            {
                command = new ViewActionCommand(this, actionKey, payload);
                _commands[actionKey] = command;
            }
            return command;
        }

        // Shared by bound controls and commands: dispatch (when bound to a dispatcher), then notify.
        internal void Trigger(ViewAction key, object payload, bool hasValue = true)
        {
            if (!_bound) return;
            _onActionTriggered?.Invoke(key, payload);
            ActionTriggered?.Invoke(this, hasValue
                ? new ActionRequestEventArgs<object>(key, payload)
                : new ActionRequestEventArgs(key));
        }

        // Explicit mode (Bind() without a dispatcher) leaves availability to the view, as it does for
        // bound controls, so a command stays executable there instead of disabling its control.
        internal bool CanTrigger(ViewAction key)
        {
            if (!_bound) return false;
            return _onActionTriggered == null || _dispatcher == null || _dispatcher.CanDispatch(key);
        }

        private void RaiseCommandsCanExecuteChanged()
        {
            foreach (var command in _commands.Values)
                command.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Binds all registered controls to their actions. A trigger first runs
        /// <paramref name="onActionTriggered"/> (the dispatch, when bound to a dispatcher), then raises
        /// <see cref="ActionTriggered"/>, so a failing notification subscriber cannot prevent the dispatch.
        /// </summary>
        /// <param name="onActionTriggered">Dispatch callback; null for event-only binding.</param>
        private void Bind(Action<ViewAction, object> onActionTriggered)
        {
            Unbind();
            _onActionTriggered = onActionTriggered;
            _bound = true;

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

                    // Value bindings raise a value-carrying ActionRequestEventArgs<T> so the payload
                    // survives the explicit hop through the view's ActionRequest event.
                    Trigger(key, payload, hasValue: binding.ValueSelector != null);
                }
            });

            foreach (var control in actionMap.Keys)
            {
                ApplyBindingStrategy((Component)control, handler);
            }
        }

        /// <summary>
        /// Binds all registered controls to raise <see cref="ActionTriggered"/> only (explicit
        /// event-based pattern). Nothing is dispatched.
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
        /// Actions reach no dispatcher in this mode. Return null from the view's <c>ActionBinder</c>
        /// so the framework does not also call <see cref="Bind(ViewActionDispatcher)"/>, which would
        /// rebind the controls for dispatching.
        /// </para>
        /// </remarks>
        public void Bind()
        {
            Bind(onActionTriggered: null);
            RaiseCommandsCanExecuteChanged();
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
            RaiseCommandsCanExecuteChanged();

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

            // Commands stop triggering until the next Bind.
            _bound = false;
            _onActionTriggered = null;
            RaiseCommandsCanExecuteChanged();
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
