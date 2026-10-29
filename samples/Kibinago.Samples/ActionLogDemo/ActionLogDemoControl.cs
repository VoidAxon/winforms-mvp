using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ActionLogDemo
{
    public partial class ActionLogDemoControl : UserControl, IActionLogDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public ActionLogDemoControl()
        {
            InitializeComponent();
            _binder.Add(ActionLogActions.Save, _btnSave, _ => (int)_numCustomerId.Value);
            _binder.Add(ActionLogActions.Load, _btnLoad);
            _binder.Add(ActionLogActions.Fail, _btnFail);
            _binder.Add(ActionLogActions.Clear, _btnClear);
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => _binder;

        public void ShowExecutions(IReadOnlyList<ActionExecution> executions)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var execution in executions)
                _list.Items.Add(ToItem(execution));
            _list.EndUpdate();
            ScrollToEnd();
        }

        public void AddExecution(ActionExecution execution)
        {
            _list.Items.Add(ToItem(execution));
            ScrollToEnd();
        }

        private void ScrollToEnd()
        {
            if (_list.Items.Count > 0) _list.EnsureVisible(_list.Items.Count - 1);
        }

        private static ListViewItem ToItem(ActionExecution e)
        {
            var item = new ListViewItem(new[]
            {
                e.StartedAt.ToLocalTime().ToString("HH:mm:ss.fff"),
                e.Action.ToString(),
                e.Payload?.ToString() ?? "",
                e.PresenterType?.Name ?? "",
                $"{e.Elapsed.TotalMilliseconds:N0} ms" + (e.IsAsync ? " (async)" : ""),
                e.Outcome.ToString(),
                e.Exception?.Message ?? "",
            });
            if (e.Outcome == ActionOutcome.Failed) item.ForeColor = Color.Firebrick;
            else if (e.Outcome == ActionOutcome.Canceled) item.ForeColor = SystemColors.GrayText;
            return item;
        }
    }
}
