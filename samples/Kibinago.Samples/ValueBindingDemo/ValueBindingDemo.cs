using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.ValueBindingDemo
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

    public partial class ValueBindingForm : Form, IValueBindingView
    {
        private ViewActionBinder _binder;
        public IViewActionBinder ActionBinder => _binder;

        public ValueBindingForm()
        {
            InitializeComponent();
            InitializeActionBindings();
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
