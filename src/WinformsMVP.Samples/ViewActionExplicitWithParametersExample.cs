using System;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples
{
    // ========================================
    // Explicit Pattern with Mixed Parameters
    // ========================================
    //
    // This example demonstrates how to handle BOTH:
    // 1. Actions WITHOUT parameters (simple ActionRequestEventArgs)
    // 2. Actions WITH parameters (ActionRequestEventArgs<T>)
    //
    // In the SAME View using explicit event pattern.

    #region View Actions

    public static class MixedDemoActions
    {
        // Actions without parameters
        public static readonly ViewAction Refresh = ViewAction.Create("MixedDemo.Refresh");
        public static readonly ViewAction Clear = ViewAction.Create("MixedDemo.Clear");

        // Actions with parameters
        public static readonly ViewAction SelectTab = ViewAction.Create("MixedDemo.SelectTab");
        public static readonly ViewAction LoadDocument = ViewAction.Create("MixedDemo.LoadDocument");
        public static readonly ViewAction SetZoom = ViewAction.Create("MixedDemo.SetZoom");
    }

    #endregion

    #region View Interface

    /// <summary>
    /// View interface using explicit pattern with mixed parameter types.
    /// Single ActionRequest event handles both parameterized and non-parameterized actions.
    /// </summary>
    public interface IMixedParametersDemoView : IWindowView
    {
        // Data properties
        string CurrentDocument { get; set; }
        int ZoomLevel { get; set; }
        int SelectedTabIndex { get; set; }
        string StatusMessage { set; }

        // ✅ SINGLE ActionRequest event for ALL actions (with or without parameters)
        // The framework uses ActionRequestEventArgs base class, which can hold derived types
        event EventHandler<ActionRequestEventArgs> ActionRequest;

        // UI Methods
        void AddLogEntry(string message);
        void ClearLog();
    }

    #endregion

    #region View Implementation

    public partial class MixedParametersDemoForm : Form, IMixedParametersDemoView
    {
        private ViewActionBinder _binder;

        // Return binder for automatic CanExecute UI updates
        public IViewActionBinder ActionBinder => _binder;

        // Single ActionRequest event (handles both types)
        public event EventHandler<ActionRequestEventArgs> ActionRequest;

        // Form controls
        private Button _btnRefresh;
        private Button _btnClear;
        private Button _btnTab1;
        private Button _btnTab2;
        private Button _btnTab3;
        private Button _btnLoadDoc;
        private NumericUpDown _numZoom;
        private Button _btnSetZoom;
        private ListBox _lstLog;
        private Label _lblStatus;
        private TextBox _txtDocument;
        private Label _lblZoom;

        public MixedParametersDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Explicit Pattern - Mixed Parameters Demo";
            this.Size = new System.Drawing.Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Simple actions (no parameters)
            _btnRefresh = new Button { Text = "Refresh", Left = 20, Top = 20, Width = 100 };
            _btnClear = new Button { Text = "Clear Log", Left = 130, Top = 20, Width = 100 };

            // Tab selection actions (with int parameter)
            var lblTabs = new Label { Text = "Select Tab:", Left = 20, Top = 60, Width = 80 };
            _btnTab1 = new Button { Text = "Tab 1", Left = 110, Top = 55, Width = 70 };
            _btnTab2 = new Button { Text = "Tab 2", Left = 190, Top = 55, Width = 70 };
            _btnTab3 = new Button { Text = "Tab 3", Left = 270, Top = 55, Width = 70 };

            // Document loading (with string parameter)
            var lblDoc = new Label { Text = "Document:", Left = 20, Top = 100, Width = 80 };
            _txtDocument = new TextBox { Left = 110, Top = 97, Width = 230 };
            _btnLoadDoc = new Button { Text = "Load", Left = 350, Top = 95, Width = 70 };

            // Zoom control (with int parameter)
            var lblZoomLabel = new Label { Text = "Zoom:", Left = 20, Top = 140, Width = 80 };
            _numZoom = new NumericUpDown { Left = 110, Top = 137, Width = 100, Minimum = 50, Maximum = 200, Value = 100 };
            _btnSetZoom = new Button { Text = "Set Zoom", Left = 220, Top = 135, Width = 80 };
            _lblZoom = new Label { Left = 310, Top = 140, Width = 100, Text = "100%" };

            // Log display
            var lblLog = new Label { Text = "Event Log:", Left = 20, Top = 180, Width = 100 };
            _lstLog = new ListBox { Left = 20, Top = 210, Width = 640, Height = 200 };

            // Status bar
            _lblStatus = new Label { Left = 20, Top = 430, Width = 640, Height = 20 };

            this.Controls.Add(_btnRefresh);
            this.Controls.Add(_btnClear);
            this.Controls.Add(lblTabs);
            this.Controls.Add(_btnTab1);
            this.Controls.Add(_btnTab2);
            this.Controls.Add(_btnTab3);
            this.Controls.Add(lblDoc);
            this.Controls.Add(_txtDocument);
            this.Controls.Add(_btnLoadDoc);
            this.Controls.Add(lblZoomLabel);
            this.Controls.Add(_numZoom);
            this.Controls.Add(_btnSetZoom);
            this.Controls.Add(_lblZoom);
            this.Controls.Add(lblLog);
            this.Controls.Add(_lstLog);
            this.Controls.Add(_lblStatus);
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            // ✅ Bind simple actions (no parameters)
            _binder.Add(MixedDemoActions.Refresh, _btnRefresh);
            _binder.Add(MixedDemoActions.Clear, _btnClear);

            // ✅ Bind parameterized actions
            // Note: We'll pass parameters when triggering the event
            _binder.Add(MixedDemoActions.SelectTab, _btnTab1, _btnTab2, _btnTab3);
            _binder.Add(MixedDemoActions.LoadDocument, _btnLoadDoc);
            _binder.Add(MixedDemoActions.SetZoom, _btnSetZoom);

            // ✅ CRITICAL: Subscribe to ActionTriggered and handle BOTH types
            _binder.ActionTriggered += (sender, e) =>
            {
                // Check if this action needs a parameter
                if (e.ActionKey == MixedDemoActions.SelectTab)
                {
                    // Determine which tab button was clicked
                    var clickedButton = sender as ViewActionBinder;
                    int tabIndex = GetTabIndexFromSender();

                    // Create parameterized event args
                    var parameterizedArgs = new ActionRequestEventArgs<int>(e.ActionKey, tabIndex);
                    ActionRequest?.Invoke(this, parameterizedArgs);
                }
                else if (e.ActionKey == MixedDemoActions.LoadDocument)
                {
                    // Get document name from textbox
                    var docName = _txtDocument.Text;
                    var parameterizedArgs = new ActionRequestEventArgs<string>(e.ActionKey, docName);
                    ActionRequest?.Invoke(this, parameterizedArgs);
                }
                else if (e.ActionKey == MixedDemoActions.SetZoom)
                {
                    // Get zoom level from NumericUpDown
                    int zoomLevel = (int)_numZoom.Value;
                    var parameterizedArgs = new ActionRequestEventArgs<int>(e.ActionKey, zoomLevel);
                    ActionRequest?.Invoke(this, parameterizedArgs);
                }
                else
                {
                    // Simple action without parameters
                    ActionRequest?.Invoke(this, e);
                }
            };

            // Subscribe to button clicks to track which tab button was clicked
            _btnTab1.Click += (s, e) => _lastClickedTabButton = _btnTab1;
            _btnTab2.Click += (s, e) => _lastClickedTabButton = _btnTab2;
            _btnTab3.Click += (s, e) => _lastClickedTabButton = _btnTab3;

            // Framework will call View.ActionBinder.Bind(_dispatcher) automatically
        }

        private Button _lastClickedTabButton;

        private int GetTabIndexFromSender()
        {
            if (_lastClickedTabButton == _btnTab1) return 0;
            if (_lastClickedTabButton == _btnTab2) return 1;
            if (_lastClickedTabButton == _btnTab3) return 2;
            return 0;
        }

        // IMixedParametersDemoView implementation

        public string CurrentDocument
        {
            get => _txtDocument.Text;
            set => _txtDocument.Text = value;
        }

        public int ZoomLevel
        {
            get => (int)_numZoom.Value;
            set
            {
                _numZoom.Value = value;
                _lblZoom.Text = $"{value}%";
            }
        }

        public int SelectedTabIndex { get; set; }

        public string StatusMessage
        {
            set => _lblStatus.Text = value;
        }

        public void AddLogEntry(string message)
        {
            _lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            _lstLog.SelectedIndex = _lstLog.Items.Count - 1;
        }

        public void ClearLog()
        {
            _lstLog.Items.Clear();
        }
    }

    #endregion

    #region Presenter

    public class MixedParametersDemoPresenter : WindowPresenterBase<IMixedParametersDemoView>
    {
        private readonly IMessageService _messageService;

        public MixedParametersDemoPresenter(IMessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        protected override void OnViewAttached()
        {
            // ✅ Use the OnViewActionTriggered method provided by the base class!
            // It automatically checks parameters, extracts them, and calls Dispatcher.Dispatch()
            View.ActionRequest += OnViewActionTriggered;

            // Initialize
            View.StatusMessage = "Ready - Mix of simple and parameterized actions";
            View.AddLogEntry("Application started");
        }

        protected override void RegisterViewActions()
        {
            // Register simple actions (no parameters)
            Dispatcher.Register(MixedDemoActions.Refresh, OnRefresh);
            Dispatcher.Register(MixedDemoActions.Clear, OnClear);

            // Register parameterized actions
            Dispatcher.Register<int>(MixedDemoActions.SelectTab, OnSelectTab);
            Dispatcher.Register<string>(MixedDemoActions.LoadDocument, OnLoadDocument);
            Dispatcher.Register<int>(MixedDemoActions.SetZoom, OnSetZoom);

            // Framework calls View.ActionBinder.Bind(_dispatcher) automatically
            // Mode detection recognizes explicit pattern and prevents double-dispatch
        }

        // ✅ No need to manually write parameter checking logic!
        // OnViewActionTriggered (base class method) has already handled:
        // - Check e is IActionRequestEventArgsWithValue
        // - Extract parameter payload = valueProvider.GetValue()
        // - Call Dispatcher.Dispatch(actionKey, payload)

        // Simple action handlers (no parameters)

        private void OnRefresh()
        {
            View.StatusMessage = "Refreshing...";
            View.AddLogEntry("Refresh completed");
            _messageService.ShowInfo("Data refreshed!", "Info");
        }

        private void OnClear()
        {
            View.ClearLog();
            View.StatusMessage = "Log cleared";
        }

        // Parameterized action handlers

        private void OnSelectTab(int tabIndex)
        {
            View.SelectedTabIndex = tabIndex;
            View.StatusMessage = $"Switched to Tab {tabIndex + 1}";
            View.AddLogEntry($"Tab selected: {tabIndex + 1}");
            _messageService.ShowInfo($"Switched to Tab {tabIndex + 1}", "Tab Selection");
        }

        private void OnLoadDocument(string documentName)
        {
            if (string.IsNullOrWhiteSpace(documentName))
            {
                _messageService.ShowWarning("Please enter a document name.", "Validation");
                return;
            }

            View.CurrentDocument = documentName;
            View.StatusMessage = $"Loaded document: {documentName}";
            View.AddLogEntry($"Document loaded: {documentName}");
            _messageService.ShowInfo($"Document '{documentName}' loaded successfully!", "Success");
        }

        private void OnSetZoom(int zoomLevel)
        {
            View.ZoomLevel = zoomLevel;
            View.StatusMessage = $"Zoom set to {zoomLevel}%";
            View.AddLogEntry($"Zoom level changed to {zoomLevel}%");
            _messageService.ShowInfo($"Zoom set to {zoomLevel}%", "Zoom");
        }
    }

    #endregion

    #region Pattern Summary

    /*
     * ========================================
     * HANDLING MIXED PARAMETERS IN EXPLICIT PATTERN
     * ========================================
     *
     * KEY POINTS:
     * -----------
     *
     * 1. SINGLE ActionRequest EVENT:
     *    - Use base type: event EventHandler<ActionRequestEventArgs>
     *    - Handles both simple and parameterized actions
     *
     * 2. VIEW IMPLEMENTATION (ActionTriggered Handler):
     *    - Check which action was triggered
     *    - For parameterized actions: Create ActionRequestEventArgs<T> with parameter
     *    - For simple actions: Forward original ActionRequestEventArgs
     *
     * 3. PRESENTER (Using Base Class Method - Super Simple):
     *    - Subscribe directly: View.ActionRequest += OnViewActionTriggered;
     *    - Base class OnViewActionTriggered automatically handles parameter checking and extraction
     *    - No need to manually write any parameter handling code!
     *
     * 4. DISPATCHER REGISTRATION:
     *    - Simple actions: Dispatcher.Register(action, handler)
     *    - Parameterized actions: Dispatcher.Register<T>(action, handler)
     *
     *
     * EVENT FLOW DIAGRAM:
     * -------------------
     *
     * Simple Action (Refresh):
     * Button Click → ActionTriggered(ActionRequestEventArgs)
     *              → ActionRequest(ActionRequestEventArgs)
     *              → Dispatcher.Dispatch(actionKey)
     *              → OnRefresh()
     *
     * Parameterized Action (SelectTab):
     * Button Click → ActionTriggered(ActionRequestEventArgs)
     *              → Determine parameter (tab index)
     *              → ActionRequest(ActionRequestEventArgs<int>)
     *              → OnViewActionTriggered (base class automatically checks and extracts parameter)
     *              → Dispatcher.Dispatch(actionKey, tabIndex)
     *              → OnSelectTab(int tabIndex)
     *
     *
     * ALTERNATIVE APPROACHES:
     * -----------------------
     *
     * Approach 1: Single Event (CURRENT - Recommended)
     * - event EventHandler<ActionRequestEventArgs> ActionRequest;
     * ✅ Pros: Simple interface, single subscription point
     * ✅ Uses polymorphism (ActionRequestEventArgs<T> : ActionRequestEventArgs)
     *
     * Approach 2: Multiple Typed Events (More Explicit)
     * - event EventHandler<ActionRequestEventArgs> SimpleActions;
     * - event EventHandler<ActionRequestEventArgs<int>> IntActions;
     * - event EventHandler<ActionRequestEventArgs<string>> StringActions;
     * ✅ Pros: Type-safe at event level, clear separation
     * ❌ Cons: More events to manage, more subscriptions
     *
     * Approach 3: Generic Event (Not Recommended)
     * - event EventHandler<ActionRequestEventArgs<object>> ActionRequest;
     * ❌ Cons: Loses type safety, requires casting
     *
     *
     * BEST PRACTICES:
     * ---------------
     *
     * 1. Use base class OnViewActionTriggered method (Most Important!)
     *    - Subscribe directly: View.ActionRequest += OnViewActionTriggered;
     *    - Base class automatically handles parameter checking and extraction
     *    - No need to manually write if (e is IActionRequestEventArgsWithValue) logic
     *
     * 2. Keep parameter extraction in View layer
     *    - View knows about UI controls (_txtDocument, _numZoom)
     *    - Presenter doesn't need to know about controls
     *
     * 3. Validate parameters in Presenter
     *    - Check for null, empty strings, valid ranges
     *    - Show validation errors via IMessageService
     *
     * 4. Log both types of actions
     *    - Helps with debugging
     *    - Shows parameter values in log
     */

    #endregion
}
