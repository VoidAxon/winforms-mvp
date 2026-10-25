using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Kibinago.Common.Events;

namespace Kibinago.ViewActions
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
        private enum BindMode { Unbound, EventOnly, Dispatcher }

        // One record per bound component: the action it triggers, how its payload is read, and its
        // live subscriptions. Kept per component so a control can be added after Bind, removed, or
        // dropped when it is disposed, without touching its siblings.
        private sealed class Entry
        {
            public ViewAction ActionKey;
            public Component Component;
            public Func<Component, object> ValueSelector;
            public EventHandler Handler;          // the trigger handed to the strategy
            public Action Unsubscribe;            // live trigger subscription, null while not bound
            public EventHandler DisposedHandler;
            public bool Removed;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<Component, Entry> _byComponent = new Dictionary<Component, Entry>();

        // Strategy per component type; a null value marks a type as unsupported (e.g. separators).
        private readonly Dictionary<Type, Func<Component, Delegate, Action>> _bindingStrategies
            = new Dictionary<Type, Func<Component, Delegate, Action>>();

        // Controls whose natural interaction is a click and that have no change event of their own.
        // Exact types only: a third-party editor deriving from Panel must not silently become a click.
        private static readonly HashSet<Type> ClickControls = new HashSet<Type>
        {
            typeof(Label), typeof(LinkLabel), typeof(PictureBox),
            typeof(Panel), typeof(FlowLayoutPanel), typeof(TableLayoutPanel)
        };

        private BindMode _mode = BindMode.Unbound;
        private ViewActionDispatcher _dispatcher;

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
            // CheckBox / RadioButton - CheckedChanged (also on programmatic Checked)
            RegisterStrategy<CheckBox>(
                (c, h) => c.CheckedChanged += (EventHandler)h,
                (c, h) => c.CheckedChanged -= (EventHandler)h);
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

            // TextBox / RichTextBox / MaskedTextBox - TextChanged: every change, typing AND programmatic
            // Text assignment (no committed-only event). A view that writes a value back assigns it only
            // when it differs (if (txt.Text != value) txt.Text = value;), or the write dispatches again.
            RegisterStrategy<TextBoxBase>(
                (c, h) => c.TextChanged += (EventHandler)h,
                (c, h) => c.TextChanged -= (EventHandler)h);

            // NumericUpDown - fires on value change (spin buttons, typing, AND programmatic Value
            // assignment). Unlike ComboBox there is no user-committed-only event, so initializing or
            // refilling Value in code will also trigger the action; guard such assignments if that matters.
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

            // ToolStripMenuItem, ToolStripButton, ToolStripDropDownButton, ToolStripStatusLabel, ...
            RegisterStrategy<ToolStripItem>(
                (c, h) => c.Click += (EventHandler)h,
                (c, h) => c.Click -= (EventHandler)h);

            // Editors hosted in a ToolStrip are not clicks.
            RegisterStrategy<ToolStripTextBox>(
                (c, h) => c.TextChanged += (EventHandler)h,
                (c, h) => c.TextChanged -= (EventHandler)h);
            // The host clears its ComboBox before raising Disposed: detach from the captured instance.
            RegisterStrategy<ToolStripComboBox>((c, h) =>
            {
                var combo = c.ComboBox;
                combo.SelectionChangeCommitted += (EventHandler)h;
                return () => combo.SelectionChangeCommitted -= (EventHandler)h;
            });
            _bindingStrategies[typeof(ToolStripControlHost)] = null;   // other hosted controls: register explicitly
            _bindingStrategies[typeof(ToolStripSeparator)] = null;     // nothing to trigger
        }

        /// <summary>
        /// Registers a binding strategy for a specific control type using a separate detach callback.
        /// Use when the event handler is a plain <see cref="EventHandler"/> (Click, CheckedChanged, ValueChanged, ...).
        /// </summary>
        /// <remarks>The nearest registered base type of a control's runtime type is used. Register a
        /// strategy before adding a control whose type has none (a third-party editor, a custom
        /// clickable control); while bound, a new strategy applies to controls added afterwards.</remarks>
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
        /// Registers a binding strategy that returns its own unsubscribe callback. Use this overload when
        /// the control event is not a plain <see cref="EventHandler"/> (e.g. <see cref="MonthCalendar.DateChanged"/>
        /// uses <see cref="DateRangeEventHandler"/>), so the handler must be adapted and the adapted
        /// delegate captured for later removal.
        /// </summary>
        public void RegisterStrategy<T>(Func<T, Delegate, Action> subscribe) where T : Component
        {
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));
            _bindingStrategies[typeof(T)] = (comp, handler) => subscribe((T)comp, handler);
        }

        /// <summary>
        /// Binds one or more controls to an action. A control triggers one action. Can be called after
        /// <c>Bind</c> (e.g. controls a view creates later): the control is subscribed at once and, when
        /// bound to a dispatcher, its Enabled state follows the action's <c>canExecute</c>.
        /// </summary>
        /// <exception cref="ArgumentException">A default action key or a null control.</exception>
        /// <exception cref="ObjectDisposedException">A disposed control.</exception>
        /// <exception cref="InvalidOperationException">A control already bound (here or in this call), or a
        /// control type with no binding strategy — register one with <c>RegisterStrategy&lt;T&gt;</c>.
        /// Nothing is added when any control is rejected.</exception>
        public void Add(ViewAction actionKey, params Component[] controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            AddEntries(actionKey, (Component[])controls.Clone(), null);
        }

        /// <summary>
        /// Binds a control to an action whose payload is read from the control when it triggers
        /// (e.g. <c>Add(SetName, txtName, t => t.Text)</c>).
        /// </summary>
        public void Add<TControl, TValue>(ViewAction actionKey, TControl control, Func<TControl, TValue> valueSelector)
            where TControl : Component
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            AddEntries(actionKey, new Component[] { control }, comp => valueSelector((TControl)comp));
        }

        /// <summary>Typed form of <see cref="Add{TControl, TValue}(ViewAction, TControl, Func{TControl, TValue})"/>.</summary>
        public void Add<TControl, T>(ViewAction<T> actionKey, TControl control, Func<TControl, T> valueSelector)
            where TControl : Component
            => Add<TControl, T>(actionKey.Key, control, valueSelector);

        /// <summary>
        /// Unbinds controls: they no longer trigger their action and their Enabled state is no longer
        /// managed (left as it is). A removed control can be added again, to any action. Unknown or
        /// already removed controls are ignored. A disposed control is removed automatically.
        /// </summary>
        public void Remove(params Component[] controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            foreach (var component in controls)
            {
                if (component == null) throw new ArgumentException("Controls must not contain null.", nameof(controls));
                if (_byComponent.TryGetValue(component, out var entry)) RemoveEntry(entry);
            }
        }

        private void AddEntries(ViewAction actionKey, Component[] components, Func<Component, object> valueSelector)
        {
            if (actionKey.Name == null)
                throw new ArgumentException(
                    "default(ViewAction) is not a valid action key; create keys with ViewAction.Create(...).");

            // Validate everything first: a rejected control leaves nothing half-added.
            var seen = new HashSet<Component>();
            foreach (var component in components)
            {
                if (component == null)
                    throw new ArgumentException("Controls must not contain null.");
                if (IsDisposed(component))
                    throw new ObjectDisposedException(DescribeComponent(component));
                if (!seen.Add(component))
                    throw new InvalidOperationException(
                        $"{DescribeComponent(component)} appears twice in this call. A control triggers one action.");
                if (_byComponent.TryGetValue(component, out var existing))
                    throw new InvalidOperationException(
                        $"{DescribeComponent(component)} is already bound to action '{existing.ActionKey}'; " +
                        $"it cannot also be bound to '{actionKey}'. A control triggers one action.");
                if (ResolveStrategy(component) == null)
                    throw new InvalidOperationException(
                        $"{DescribeComponent(component)} ({component.GetType().FullName}) has no binding strategy, so " +
                        "nothing would trigger the action. Register the event that should trigger it before adding it, " +
                        $"e.g. binder.RegisterStrategy<{component.GetType().Name}>((c, h) => c.TextChanged += (EventHandler)h, " +
                        "(c, h) => c.TextChanged -= (EventHandler)h).");
            }

            var added = new List<Entry>(components.Length);
            foreach (var component in components)
            {
                var entry = new Entry { ActionKey = actionKey, Component = component, ValueSelector = valueSelector };
                entry.Handler = (s, e) => Trigger(entry);
                entry.DisposedHandler = (s, e) => RemoveEntry(entry);
                component.Disposed += entry.DisposedHandler;
                _entries.Add(entry);
                _byComponent[component] = entry;
                added.Add(entry);
            }

            if (_mode == BindMode.Unbound) return;
            try
            {
                foreach (var entry in added) Subscribe(entry);
            }
            catch
            {
                foreach (var entry in added) RemoveEntry(entry);
                throw;
            }
            if (_mode == BindMode.Dispatcher) UpdateCanExecuteStates(added);
        }

        private static bool IsDisposed(Component component)
            => component is Control c ? c.IsDisposed
             : component is ToolStripItem t && t.IsDisposed;

        private static string DescribeComponent(Component component)
            => component is Control c && !string.IsNullOrEmpty(c.Name) ? $"Control '{c.Name}'"
             : component is ToolStripItem t && !string.IsNullOrEmpty(t.Name) ? $"Item '{t.Name}'"
             : component.GetType().Name;

        // Nearest registered base type wins; a type registered as null is unsupported. Without a
        // registration only the plain click controls fall back to Click.
        private Func<Component, Delegate, Action> ResolveStrategy(Component component)
        {
            for (var type = component.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                if (_bindingStrategies.TryGetValue(type, out var strategy)) return strategy;
            }
            if (ClickControls.Contains(component.GetType()))
                return (c, h) =>
                {
                    var control = (Control)c;
                    control.Click += (EventHandler)h;
                    return () => control.Click -= (EventHandler)h;
                };
            return null;
        }

        private void Subscribe(Entry entry)
        {
            if (entry.Unsubscribe != null || entry.Removed) return;
            var strategy = ResolveStrategy(entry.Component)
                ?? throw new InvalidOperationException(
                    $"{DescribeComponent(entry.Component)} has no binding strategy any more.");
            entry.Unsubscribe = strategy(entry.Component, entry.Handler);
        }

        private static void Unsubscribe(Entry entry)
        {
            var unsubscribe = entry.Unsubscribe;
            entry.Unsubscribe = null;
            unsubscribe?.Invoke();
        }

        private void RemoveEntry(Entry entry)
        {
            if (entry.Removed) return;
            entry.Removed = true;
            Unsubscribe(entry);
            entry.Component.Disposed -= entry.DisposedHandler;
            _entries.Remove(entry);
            if (_byComponent.TryGetValue(entry.Component, out var mapped) && ReferenceEquals(mapped, entry))
                _byComponent.Remove(entry.Component);
        }

        /// <summary>
        /// Dispatches (when bound to a dispatcher) and then raises <see cref="ActionTriggered"/>, so a
        /// failing notification subscriber cannot prevent the dispatch.
        /// </summary>
        private void Trigger(Entry entry)
        {
            if (entry.Removed || _mode == BindMode.Unbound) return;
            var key = entry.ActionKey;

            // Read the payload off the triggering control, if this binding carries a value.
            object payload = entry.ValueSelector != null ? entry.ValueSelector(entry.Component) : null;

            if (_mode == BindMode.Dispatcher) _dispatcher?.Dispatch(key, payload);

            // Value bindings raise a value-carrying ActionRequestEventArgs<T> so the payload
            // survives the explicit hop through the view's ActionRequest event.
            ActionTriggered?.Invoke(this, entry.ValueSelector != null
                ? new ActionRequestEventArgs<object>(key, payload)
                : new ActionRequestEventArgs(key));
        }

        /// <summary>
        /// Binds all registered controls for the explicit event-based pattern: a triggered control raises
        /// <see cref="ActionTriggered"/> only (no dispatcher, no Enabled management). Detaches a dispatcher
        /// bound earlier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This overload is for the explicit event pattern, where the view returns a null
        /// <c>ActionBinder</c> and calls <c>Bind()</c> itself; returning the binder from
        /// <c>ActionBinder</c> would also let the framework call <see cref="Bind(ViewActionDispatcher)"/>,
        /// so the framework does not also call <see cref="Bind(ViewActionDispatcher)"/>, which would
        /// rebind the controls for dispatching.
        /// </para>
        /// </remarks>
        public void Bind()
        {
            DetachDispatcher();
            _mode = BindMode.EventOnly;
            Resubscribe();
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

            DetachDispatcher();
            _dispatcher = dispatcher;
            _mode = BindMode.Dispatcher;
            Resubscribe();

            // Subscribe to both events for automatic UI refresh
            _dispatcher.ActionExecuted += OnActionExecuted;
            _dispatcher.CanExecuteChanged += OnCanExecuteChanged;

            UpdateCanExecuteStates();
        }

        private void Resubscribe()
        {
            foreach (var entry in _entries.ToArray()) Unsubscribe(entry);
            foreach (var entry in _entries.ToArray()) Subscribe(entry);
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
        public void UpdateCanExecuteStates() => UpdateCanExecuteStates(_entries.ToArray());

        // CanExecute and EnabledChanged handlers may add, remove or dispose controls, or rebind: work on a
        // snapshot and re-check each entry (and the dispatcher) before touching its control.
        private void UpdateCanExecuteStates(IList<Entry> entries)
        {
            var dispatcher = _dispatcher;
            if (dispatcher == null || _mode != BindMode.Dispatcher) return;

            var canExecute = new Dictionary<ViewAction, bool>();
            foreach (var entry in entries)
            {
                if (!ReferenceEquals(_dispatcher, dispatcher)) return;   // unbound / rebound meanwhile
                if (entry.Removed || IsDisposed(entry.Component)) continue;
                if (!canExecute.TryGetValue(entry.ActionKey, out var enabled))
                    canExecute[entry.ActionKey] = enabled = dispatcher.CanDispatch(entry.ActionKey);
                SetControlEnabled(entry, enabled, dispatcher);
            }
        }

        private void SetControlEnabled(Entry entry, bool enabled, ViewActionDispatcher dispatcher)
        {
            // Marshalled updates re-check that the control is still bound to the same dispatcher.
            Action apply = () =>
            {
                if (entry.Removed || IsDisposed(entry.Component) || !ReferenceEquals(_dispatcher, dispatcher)) return;
                if (entry.Component is Control c) c.Enabled = enabled;
                else if (entry.Component is ToolStripItem t) t.Enabled = enabled;
            };

            if (entry.Component is Control control)
            {
                if (control.InvokeRequired) control.BeginInvoke(apply);
                else apply();
            }
            else if (entry.Component is ToolStripItem toolStripItem)
            {
                var owner = toolStripItem.GetCurrentParent();
                if (owner != null && owner.InvokeRequired) owner.BeginInvoke(apply);
                else apply();
            }
        }

        /// <summary>
        /// Detaches the controls and the dispatcher: clicks no longer dispatch and control states are no
        /// longer refreshed. Called by the framework when the presenter that bound this binder is
        /// disposed. The configured bindings stay, so <see cref="Bind(ViewActionDispatcher)"/> can bind
        /// again; controls added meanwhile are recorded and subscribed on the next bind. Calling it twice
        /// does nothing.
        /// </summary>
        public void Unbind()
        {
            DetachDispatcher();
            _mode = BindMode.Unbound;
            foreach (var entry in _entries.ToArray()) Unsubscribe(entry);
        }

        private void DetachDispatcher()
        {
            if (_dispatcher == null) return;
            _dispatcher.ActionExecuted -= OnActionExecuted;
            _dispatcher.CanExecuteChanged -= OnCanExecuteChanged;
            _dispatcher = null;
        }

        IEnumerator<ActionBinding> IEnumerable<ActionBinding>.GetEnumerator()
        {
            foreach (var entry in _entries.ToArray())
                yield return new ActionBinding(entry.ActionKey, new[] { entry.Component }, entry.ValueSelector);
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ActionBinding>)this).GetEnumerator();
    }
}
