using System;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Common.Events;
using Kibinago.Views;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Services;

namespace Kibinago.Samples
{
    // ========================================
    // Explicit Event-Based ViewAction Pattern
    // ========================================
    //
    // This example demonstrates the EXPLICIT event-based pattern for ViewActions.
    //
    // The recommended default is still the IMPLICIT pattern (see ViewActionExample.cs).
    // The explicit pattern exists mainly for migrating legacy Forms that already raise
    // their own events, or for teams that want every hop written out.
    //
    // How the two patterns differ:
    // - IMPLICIT: ActionBinder returns the binder; the framework calls
    //             View.ActionBinder.Bind(_dispatcher) after RegisterViewActions(), dispatches
    //             directly and keeps control Enabled state in sync with CanExecute.
    // - EXPLICIT: ActionBinder returns null (the framework binds nothing); the View calls
    //             _binder.Bind() itself (event-only binding) and forwards ActionTriggered to its
    //             own ActionRequest event; the Presenter writes View.ActionRequest += OnViewActionTriggered.
    //
    // Routing rule: which Bind overload runs decides the routing, never who listens.
    //   Bind(dispatcher) always dispatches (ActionTriggered is then just a notification).
    //   Bind()           only raises ActionTriggered.
    // Returning the binder AND forwarding ActionTriggered to ActionRequest would dispatch twice.
    //
    // Trade-off: the binder is not connected to the dispatcher, so control Enabled state is
    // NOT synced from CanExecute. Express availability through semantic view members the
    // Presenter sets (here: bool CanDelete { set; }). Dispatcher CanExecute predicates still
    // guard execution — a disabled action is never run.
    //
    // Benefits of explicit pattern:
    // ✅ Clear, visible event subscription code
    // ✅ F12 navigation works (jump to handler)
    // ✅ Easy to debug (set breakpoint on event handler)
    // ✅ Familiar pattern (standard .NET events) — close to what legacy Forms already do

    #region View Actions

    public static class ExplicitDemoActions
    {
        public static readonly ViewAction Save = ViewAction.Create("ExplicitDemo.Save");
        public static readonly ViewAction Delete = ViewAction.Create("ExplicitDemo.Delete");
        public static readonly ViewAction Refresh = ViewAction.Create("ExplicitDemo.Refresh");
    }

    #endregion

    #region View Interface

    /// <summary>
    /// View interface using explicit event-based pattern.
    /// Exposes ActionRequest event for explicit ViewAction routing to Presenter.
    /// Action availability is pushed by the Presenter through semantic members (CanDelete).
    /// </summary>
    public interface IExplicitEventDemoView : IWindowView
    {
        // Data properties
        string ItemName { get; set; }
        int ItemCount { get; set; }
        string StatusMessage { set; }

        // Selection state
        bool HasSelection { get; }
        string SelectedItem { get; }

        // ✅ Semantic availability — the Form decides how to show it (here: button Enabled)
        bool CanDelete { set; }

        // ✅ Explicit event for ViewAction handling
        event EventHandler<ActionRequestEventArgs> ActionRequest;

        // ✅ State change event — no binder sync in this pattern, so the Presenter listens itself
        event EventHandler SelectionChanged;

        // Methods
        void ShowItems(string[] items);
        void ClearForm();
    }

    #endregion

    #region View Implementation

    public partial class ExplicitEventDemoForm : Form, IExplicitEventDemoView
    {
        private ViewActionBinder _binder;

        // ✅ Return null: the framework must NOT call Bind(_dispatcher) — the actions already
        // reach the Presenter through ActionRequest, so binding them would dispatch twice.
        public IViewActionBinder ActionBinder => null;

        // ✅ Expose ActionRequest event (explicit pattern)
        public event EventHandler<ActionRequestEventArgs> ActionRequest;

        public event EventHandler SelectionChanged;

        // Form controls (designer-initialized)
        private TextBox _txtItemName;
        private NumericUpDown _numItemCount;
        private ListBox _lstItems;
        private Label _lblStatus;
        private Button _btnSave;
        private Button _btnDelete;
        private Button _btnRefresh;

        public ExplicitEventDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            // Form setup
            this.Text = "Explicit Event Pattern Demo";
            this.Size = new System.Drawing.Size(500, 400);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Controls initialization
            _txtItemName = new TextBox { Left = 120, Top = 20, Width = 200 };
            _numItemCount = new NumericUpDown { Left = 120, Top = 50, Width = 100, Maximum = 1000 };
            _lstItems = new ListBox { Left = 20, Top = 100, Width = 300, Height = 200 };
            _lblStatus = new Label { Left = 20, Top = 320, Width = 450, Height = 20 };

            _btnSave = new Button { Text = "Save", Left = 350, Top = 100, Width = 100 };
            _btnDelete = new Button { Text = "Delete", Left = 350, Top = 140, Width = 100 };
            _btnRefresh = new Button { Text = "Refresh", Left = 350, Top = 180, Width = 100 };

            var lblName = new Label { Text = "Item Name:", Left = 20, Top = 23, Width = 90 };
            var lblCount = new Label { Text = "Count:", Left = 20, Top = 53, Width = 90 };

            this.Controls.Add(lblName);
            this.Controls.Add(lblCount);
            this.Controls.Add(_txtItemName);
            this.Controls.Add(_numItemCount);
            this.Controls.Add(_lstItems);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_btnSave);
            this.Controls.Add(_btnDelete);
            this.Controls.Add(_btnRefresh);

            // Tell the Presenter the selection changed; it decides what is available
            _lstItems.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            // Map buttons to ViewActions
            _binder.Add(ExplicitDemoActions.Save, _btnSave);
            _binder.Add(ExplicitDemoActions.Delete, _btnDelete);
            _binder.Add(ExplicitDemoActions.Refresh, _btnRefresh);

            // ✅ EXPLICIT PATTERN: forward ActionTriggered to the public ActionRequest event
            _binder.ActionTriggered += (sender, e) => ActionRequest?.Invoke(this, e);

            // ✅ Event-only binding: controls raise ActionTriggered, nothing is dispatched here.
            // The View calls Bind() itself because ActionBinder returns null.
            _binder.Bind();
        }

        // IExplicitEventDemoView implementation

        public string ItemName
        {
            get => _txtItemName.Text;
            set => _txtItemName.Text = value;
        }

        public int ItemCount
        {
            get => (int)_numItemCount.Value;
            set => _numItemCount.Value = value;
        }

        public string StatusMessage
        {
            set => _lblStatus.Text = value;
        }

        public bool HasSelection => _lstItems.SelectedIndex >= 0;

        public string SelectedItem => _lstItems.SelectedItem as string;

        public bool CanDelete
        {
            set => _btnDelete.Enabled = value;
        }

        public void ShowItems(string[] items)
        {
            _lstItems.Items.Clear();
            if (items != null)
            {
                _lstItems.Items.AddRange(items);
            }
        }

        public void ClearForm()
        {
            _txtItemName.Clear();
            _numItemCount.Value = 0;
        }
    }

    #endregion

    #region Presenter

    public class ExplicitEventDemoPresenter : WindowPresenterBase<IExplicitEventDemoView>
    {
        private readonly IMessageService _messageService;
        private string[] _items = new[] { "Item 1", "Item 2", "Item 3" };

        public ExplicitEventDemoPresenter(IMessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        protected override void OnViewAttached()
        {
            // ✅ EXPLICIT SUBSCRIPTION - OnViewActionTriggered is a built-in base helper that
            // forwards the request (and its payload, if any) to Dispatcher
            View.ActionRequest += OnViewActionTriggered;
            Disposable.Create(() => View.ActionRequest -= OnViewActionTriggered).DisposeWith(Disposables);

            View.SelectionChanged += OnSelectionChanged;
            Disposable.Create(() => View.SelectionChanged -= OnSelectionChanged).DisposeWith(Disposables);
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ExplicitDemoActions.Save, OnSave);

            // canExecute still guards execution; it just doesn't drive Enabled in this pattern
            Dispatcher.Register(
                ExplicitDemoActions.Delete,
                OnDelete,
                canExecute: () => View.HasSelection);

            Dispatcher.Register(ExplicitDemoActions.Refresh, OnRefresh);

            // ActionBinder is null, so the framework binds nothing here.
            // Actions arrive via View.ActionRequest → OnViewActionTriggered → Dispatcher.
        }

        protected override void OnInitialize()
        {
            // Load initial data
            View.ShowItems(_items);
            View.StatusMessage = "Ready";
            UpdateActionAvailability();
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateActionAvailability();
        }

        // ✅ Explicit pattern: the Presenter pushes availability through a semantic view member
        private void UpdateActionAvailability()
        {
            View.CanDelete = View.HasSelection;
        }

        private void OnSave()
        {
            var item = $"{View.ItemName} (x{View.ItemCount})";
            Array.Resize(ref _items, _items.Length + 1);
            _items[_items.Length - 1] = item;

            View.ShowItems(_items);
            View.ClearForm();
            View.StatusMessage = $"Saved: {item}";
            UpdateActionAvailability();

            _messageService.ShowInfo($"Item saved: {item}", "Success");
        }

        private void OnDelete()
        {
            var selected = View.SelectedItem;
            var index = Array.IndexOf(_items, selected);
            if (index >= 0)
            {
                var newItems = new string[_items.Length - 1];
                Array.Copy(_items, 0, newItems, 0, index);
                Array.Copy(_items, index + 1, newItems, index, _items.Length - index - 1);
                _items = newItems;
            }

            View.ShowItems(_items);
            View.StatusMessage = $"Deleted: {selected}";
            UpdateActionAvailability();
            _messageService.ShowInfo("Item deleted successfully.", "Success");
        }

        private void OnRefresh()
        {
            View.ShowItems(_items);
            View.StatusMessage = "Refreshed";
            UpdateActionAvailability();
            _messageService.ShowInfo("Data refreshed.", "Info");
        }
    }

    #endregion

    #region Comparison Summary

    /*
     * ========================================
     * IMPLICIT vs EXPLICIT Pattern Comparison
     * ========================================
     *
     * IMPLICIT PATTERN (recommended default):
     * ---------------------------------------
     * // View (IWindowView already declares IViewActionBinder ActionBinder { get; })
     * public IViewActionBinder ActionBinder => _binder;   // framework binds it
     *
     * // Presenter
     * protected override void RegisterViewActions()
     * {
     *     Dispatcher.Register(CommonActions.Delete, OnDelete,
     *         canExecute: () => View.HasSelection);
     *
     *     // Framework auto-calls View.ActionBinder.Bind(_dispatcher) after this returns
     * }
     *
     * ✅ Pros: Less code, automatic CanExecute → Enabled sync, auto-binding by framework
     * ❌ Cons: Implicit connection (framework magic)
     *
     *
     * EXPLICIT PATTERN (mainly for migrating legacy Forms):
     * -----------------------------------------------------
     * // View Interface
     * public interface IMyView : IWindowView
     * {
     *     event EventHandler<ActionRequestEventArgs> ActionRequest;
     *     bool CanDelete { set; }              // semantic availability, set by the Presenter
     *     event EventHandler SelectionChanged; // state the Presenter must react to
     * }
     *
     * // View Implementation
     * public IViewActionBinder ActionBinder => null;   // framework must NOT bind it
     * _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
     * _binder.Bind();                                  // event-only binding, done by the View
     *
     * // Presenter
     * protected override void OnViewAttached()
     * {
     *     View.ActionRequest += OnViewActionTriggered;
     *     Disposable.Create(() => View.ActionRequest -= OnViewActionTriggered).DisposeWith(Disposables);
     * }
     *
     * protected override void RegisterViewActions()
     * {
     *     Dispatcher.Register(CommonActions.Delete, OnDelete,
     *         canExecute: () => View.HasSelection);   // guards execution only
     * }
     *
     * private void UpdateActionAvailability() => View.CanDelete = View.HasSelection;
     *
     * ✅ Pros: Explicit, debuggable, F12 navigation works, fits existing event-driven Forms
     * ❌ Cons: No automatic Enabled sync — the Presenter pushes availability itself
     *
     *
     * RULES:
     * ------
     * - Never return the binder from ActionBinder AND forward ActionTriggered to ActionRequest:
     *   Bind(dispatcher) always dispatches, and ActionRequest → OnViewActionTriggered would
     *   dispatch the same action a second time.
     * - Subscribing to ActionTriggered never changes routing; only the Bind overload does.
     *
     * WHEN TO USE EACH:
     * -----------------
     * Implicit: New code — the default.
     * Explicit: Migrating legacy Forms, or teams that require every event hop written out.
     */

    #endregion
}
