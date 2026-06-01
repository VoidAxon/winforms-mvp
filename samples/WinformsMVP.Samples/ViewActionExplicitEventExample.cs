using System;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples
{
    // ========================================
    // Explicit Event-Based ViewAction Pattern
    // ========================================
    //
    // This example demonstrates the EXPLICIT event-based pattern for ViewActions.
    //
    // Key difference from implicit pattern:
    // - IMPLICIT: Framework auto-calls View.ActionBinder.Bind(_dispatcher) after RegisterViewActions()
    // - EXPLICIT: View.ActionRequest += OnActionRequest - clear subscription you write yourself
    //
    // Benefits of explicit pattern:
    // ✅ Clear, visible event subscription code
    // ✅ F12 navigation works (jump to handler)
    // ✅ IDE refactoring support (rename, find references)
    // ✅ Easy to debug (set breakpoint on event handler)
    // ✅ Familiar pattern (standard .NET events)
    //
    // Use when:
    // - You need explicit control flow visibility
    // - Team prefers explicit over implicit
    // - Complex scenarios with conditional routing

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
    /// Automatic CanExecute UI updates work without manual state change events!
    /// </summary>
    public interface IExplicitEventDemoView : IWindowView
    {
        // Data properties
        string ItemName { get; set; }
        int ItemCount { get; set; }
        string StatusMessage { set; }

        // State properties for CanExecute
        bool HasUnsavedChanges { get; }
        bool HasSelection { get; }

        // ✅ Explicit event for ViewAction handling
        event EventHandler<ActionRequestEventArgs> ActionRequest;

        // ✅ No state change events needed! Automatic CanExecute updates now work in explicit pattern

        // Methods
        void ShowItems(string[] items);
        void ClearForm();
    }

    #endregion

    #region View Implementation

    public partial class ExplicitEventDemoForm : Form, IExplicitEventDemoView
    {
        private ViewActionBinder _binder;

        // ✅ Return binder to enable framework auto-binding and automatic CanExecute UI updates
        // The framework will call Bind(_dispatcher) after RegisterViewActions()
        // Mode detection prevents double-dispatch (skips callback when ActionTriggered has subscribers)
        public IViewActionBinder ActionBinder => _binder;

        // ✅ Expose ActionRequest event (explicit pattern)
        public event EventHandler<ActionRequestEventArgs> ActionRequest;

        // ✅ No state change events needed - automatic CanExecute updates work!

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

            // Track changes for CanExecute
            _txtItemName.TextChanged += (s, e) => OnDataChanged();
            _numItemCount.ValueChanged += (s, e) => OnDataChanged();
            _lstItems.SelectedIndexChanged += (s, e) => OnSelectionChanged();
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            // Map buttons to ViewActions
            _binder.Add(ExplicitDemoActions.Save, _btnSave);
            _binder.Add(ExplicitDemoActions.Delete, _btnDelete);
            _binder.Add(ExplicitDemoActions.Refresh, _btnRefresh);

            // ✅ EXPLICIT PATTERN: Subscribe to ActionTriggered and forward to ActionRequest
            _binder.ActionTriggered += (sender, e) =>
            {
                // Forward to public ActionRequest event
                ActionRequest?.Invoke(this, e);
            };

            // No need to call Bind() here - Framework will call View.ActionBinder.Bind(_dispatcher)
            // automatically in RegisterViewActions(), which enables automatic CanExecute UI updates
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

        public bool HasUnsavedChanges { get; private set; }

        public bool HasSelection => _lstItems.SelectedIndex >= 0;

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
            HasUnsavedChanges = false;
        }

        // Helper methods
        private void OnDataChanged()
        {
            HasUnsavedChanges = true;
            // ✅ No need to trigger event - automatic CanExecute updates work!
        }

        private void OnSelectionChanged()
        {
            // ✅ No need to notify presenter - automatic CanExecute updates work!
        }

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
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
            // ✅ SIMPLE EXPLICIT SUBSCRIPTION - Use base class helper method!
            // OnViewActionTriggered is a built-in helper that forwards to Dispatcher
            View.ActionRequest += OnViewActionTriggered;  // One line - super clean!

            // Load initial data
            View.ShowItems(_items);
            View.StatusMessage = "Ready";
        }

        protected override void RegisterViewActions()
        {
            // ✅ Register handlers with CanExecute predicates
            Dispatcher.Register(
                ExplicitDemoActions.Save,
                OnSave,
                canExecute: () => View.HasUnsavedChanges);

            Dispatcher.Register(
                ExplicitDemoActions.Delete,
                OnDelete,
                canExecute: () => View.HasSelection);

            Dispatcher.Register(ExplicitDemoActions.Refresh, OnRefresh);

            // ✅ Framework automatically calls View.ActionBinder.Bind(_dispatcher) here!
            // Mode detection recognizes explicit pattern (ActionTriggered has subscribers)
            // and prevents double-dispatch while enabling automatic CanExecute UI updates.
            //
            // Benefits: Automatic UI state updates without manual RaiseCanExecuteChanged() calls!
        }

        // ✅ No OnInitialize() needed - automatic CanExecute updates work out of the box!
        // Previously required manual subscriptions: View.SelectionChanged, View.DataChanged
        // Now those are unnecessary - framework handles it automatically

        // ✅ Note: No need for manual OnActionRequest handler!
        // OnViewActionTriggered (base class method) handles routing to Dispatcher

        private void OnSave()
        {
            var item = $"{View.ItemName} (x{View.ItemCount})";
            Array.Resize(ref _items, _items.Length + 1);
            _items[_items.Length - 1] = item;

            View.ShowItems(_items);
            View.ClearForm();
            View.StatusMessage = $"Saved: {item}";

            _messageService.ShowInfo($"Item saved: {item}", "Success");
        }

        private void OnDelete()
        {
            // Delete selected item
            var index = Array.IndexOf(_items, "Item 1"); // Simplified
            if (index >= 0)
            {
                var newItems = new string[_items.Length - 1];
                Array.Copy(_items, 0, newItems, 0, index);
                Array.Copy(_items, index + 1, newItems, index, _items.Length - index - 1);
                _items = newItems;
            }

            View.ShowItems(_items);
            View.StatusMessage = "Item deleted";
            _messageService.ShowInfo("Item deleted successfully.", "Success");
        }

        private void OnRefresh()
        {
            View.ShowItems(_items);
            View.StatusMessage = "Refreshed";
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
     * IMPLICIT PATTERN (ActionBinder + Dispatcher):
     * ----------------------------------------------
     * // View Interface
     * public interface IMyView : IWindowView
     * {
     *     ViewActionBinder ActionBinder { get; }  // Returns _binder instance
     * }
     *
     * // Presenter
     * protected override void RegisterViewActions()
     * {
     *     Dispatcher.Register(CommonActions.Save, OnSave,
     *         canExecute: () => View.HasUnsavedChanges);
     *
     *     // Framework auto-calls View.ActionBinder.Bind(_dispatcher) here!
     * }
     *
     * ✅ Pros: Less code, automatic CanExecute UI updates, auto-binding by framework
     * ❌ Cons: Implicit connection (framework magic), harder to debug event flow
     *
     *
     * EXPLICIT PATTERN (ActionRequest Event + Automatic CanExecute):
     * ---------------------------------------------------------------
     * // View Interface
     * public interface IMyView : IWindowView
     * {
     *     ViewActionBinder ActionBinder { get; }  // Returns _binder (enables auto CanExecute updates!)
     *     event EventHandler<ActionRequestEventArgs> ActionRequest;
     *     // ✅ No state change events needed!
     * }
     *
     * // View Implementation
     * public IViewActionBinder ActionBinder => _binder;  // Return binder for framework integration
     * _binder.ActionTriggered += (s, e) => ActionRequest?.Invoke(this, e);
     * // No manual Bind() call - framework handles it automatically!
     *
     * // Presenter - Simple and clean!
     * protected override void OnViewAttached()
     * {
     *     View.ActionRequest += OnViewActionTriggered;  // ✅ Explicit event subscription
     * }
     *
     * // ✅ No OnInitialize() needed - automatic CanExecute updates work!
     *
     * protected override void RegisterViewActions()
     * {
     *     Dispatcher.Register(CommonActions.Save, OnSave,
     *         canExecute: () => View.HasUnsavedChanges);
     *
     *     // Framework calls View.ActionBinder.Bind(_dispatcher) automatically
     *     // Mode detection prevents double-dispatch while enabling automatic UI updates
     * }
     *
     * ✅ Pros: Explicit, debuggable, F12 navigation works, automatic CanExecute updates!
     * ✅ Best of both worlds: Explicit event flow + Automatic UI updates
     *
     *
     * WHEN TO USE EACH:
     * -----------------
     * Implicit: Simple CRUD, standard workflows, less code (no ActionRequest event)
     * Explicit: Complex logic, debugging needs, team prefers explicit event subscriptions, learning
     *
     * KEY IMPROVEMENTS:
     * -----------------
     * ✅ Both patterns now get automatic CanExecute UI updates (buttons auto enable/disable)
     * ✅ Explicit pattern no longer requires manual state change events
     * ✅ Explicit pattern no longer requires manual RaiseCanExecuteChanged() calls
     * ✅ Mode detection prevents double-dispatch automatically
     * ✅ Simpler View interfaces (fewer events to define)
     * ✅ Less boilerplate code in Presenter
     */

    #endregion
}
