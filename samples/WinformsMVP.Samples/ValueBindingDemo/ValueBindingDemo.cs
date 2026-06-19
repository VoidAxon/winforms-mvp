using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.ValueBindingDemo
{
    // ============================================================================
    // Value Binding Demo
    // ----------------------------------------------------------------------------
    // Shows the value-carrying binding overload:
    //
    //     _binder.Add(action, control, valueSelector);
    //
    // The control's current value is read at trigger time and dispatched as the
    // action payload. The Presenter just registers typed handlers (Register<T>) —
    // it never touches a control, and there is no ActionRequest event or manual
    // value extraction. Also demonstrates the value controls that now bind out of
    // the box: NumericUpDown / DateTimePicker (ValueChanged) and MonthCalendar
    // (DateChanged, adapted via the subscribe strategy).
    // ============================================================================

    #region View Actions

    public static class ValueBindingActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("ValueBinding");

        public static readonly ViewAction SetAutoSave = Factory.Create("SetAutoSave");
        public static readonly ViewAction SetFontSize = Factory.Create("SetFontSize");
        public static readonly ViewAction SetReminder = Factory.Create("SetReminder");
        public static readonly ViewAction SetTheme = Factory.Create("SetTheme");
        public static readonly ViewAction PickDay = Factory.Create("PickDay");
    }

    #endregion

    #region View Interface

    /// <summary>
    /// View contract — only behavior, no UI controls (MVP rule 2). The Presenter
    /// receives control values purely through typed action payloads.
    /// </summary>
    public interface IValueBindingView : IWindowView
    {
        void AddLog(string message);
    }

    #endregion

    #region View Implementation

    public class ValueBindingForm : Form, IValueBindingView
    {
        private ViewActionBinder _binder;
        public IViewActionBinder ActionBinder => _binder;

        private CheckBox _chkAutoSave;
        private NumericUpDown _numFontSize;
        private DateTimePicker _dtpReminder;
        private ComboBox _cboTheme;
        private MonthCalendar _calendar;
        private ListBox _log;

        public ValueBindingForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Text = "Value Binding Demo";
            ClientSize = new Size(560, 460);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            _chkAutoSave = new CheckBox { Text = "Auto-save", Left = 20, Top = 20, Width = 160 };

            var lblFont = new Label { Text = "Font size:", Left = 20, Top = 56, Width = 80 };
            _numFontSize = new NumericUpDown { Left = 110, Top = 53, Width = 70, Minimum = 8, Maximum = 48, Value = 12 };

            var lblReminder = new Label { Text = "Reminder:", Left = 20, Top = 90, Width = 80 };
            _dtpReminder = new DateTimePicker { Left = 110, Top = 87, Width = 200, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm" };

            var lblTheme = new Label { Text = "Theme:", Left = 20, Top = 124, Width = 80 };
            _cboTheme = new ComboBox { Left = 110, Top = 121, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboTheme.Items.AddRange(new object[] { "Light", "Dark", "High Contrast" });
            _cboTheme.SelectedIndex = 0;

            var lblCalendar = new Label { Text = "Pick a day:", Left = 20, Top = 160, Width = 80 };
            _calendar = new MonthCalendar { Left = 110, Top = 158, MaxSelectionCount = 1 };

            var lblLog = new Label { Text = "Dispatched values:", Left = 330, Top = 20, Width = 200 };
            _log = new ListBox { Left = 330, Top = 42, Width = 210, Height = 396 };

            Controls.Add(_chkAutoSave);
            Controls.Add(lblFont);
            Controls.Add(_numFontSize);
            Controls.Add(lblReminder);
            Controls.Add(_dtpReminder);
            Controls.Add(lblTheme);
            Controls.Add(_cboTheme);
            Controls.Add(lblCalendar);
            Controls.Add(_calendar);
            Controls.Add(lblLog);
            Controls.Add(_log);
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            // Each control's current value flows to the handler as the payload.
            // No ActionRequest event, no if-else, no value extraction in the Presenter.
            _binder.Add(ValueBindingActions.SetAutoSave, _chkAutoSave, c => c.Checked);            // bool
            _binder.Add(ValueBindingActions.SetFontSize, _numFontSize, n => (int)n.Value);          // int
            _binder.Add(ValueBindingActions.SetReminder, _dtpReminder, d => d.Value);               // DateTime
            _binder.Add(ValueBindingActions.SetTheme, _cboTheme, c => (string)c.SelectedItem);      // string (committed)
            _binder.Add(ValueBindingActions.PickDay, _calendar, c => c.SelectionStart);             // DateTime (DateChanged)

            // Framework auto-calls ActionBinder.Bind(dispatcher) after RegisterViewActions.
        }

        public void AddLog(string message)
        {
            _log.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            _log.SelectedIndex = _log.Items.Count - 1;
        }
    }

    #endregion

    #region Presenter

    public class ValueBindingPresenter : WindowPresenterBase<IValueBindingView>
    {
        protected override void OnViewAttached()
        {
            // Nothing to wire up here — bindings live in the View, handlers in RegisterViewActions.
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register<bool>(ValueBindingActions.SetAutoSave, on => View.AddLog($"Auto-save: {(on ? "ON" : "OFF")}"));
            Dispatcher.Register<int>(ValueBindingActions.SetFontSize, size => View.AddLog($"Font size: {size}pt"));
            Dispatcher.Register<DateTime>(ValueBindingActions.SetReminder, when => View.AddLog($"Reminder: {when:yyyy-MM-dd HH:mm}"));
            Dispatcher.Register<string>(ValueBindingActions.SetTheme, theme => View.AddLog($"Theme: {theme}"));
            Dispatcher.Register<DateTime>(ValueBindingActions.PickDay, day => View.AddLog($"Picked day: {day:yyyy-MM-dd}"));
        }

        protected override void OnInitialize()
        {
            View.AddLog("Change any control — its value is dispatched automatically.");
        }
    }

    #endregion
}
