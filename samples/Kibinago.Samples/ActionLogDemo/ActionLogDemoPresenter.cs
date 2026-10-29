using System;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ActionLogDemo
{
    public static class ActionLogActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("ActionLog");

        /// <summary>Carries the customer number: the observer records it as the payload.</summary>
        public static readonly ViewAction<int> Save = Factory.Create<int>(nameof(Save));
        public static readonly ViewAction Load = Factory.Create(nameof(Load));
        public static readonly ViewAction Fail = Factory.Create(nameof(Fail));
        public static readonly ViewAction Clear = Factory.Create(nameof(Clear));
    }

    /// <summary>
    /// The presenter's handlers know nothing about the log: the observer registered at startup
    /// records each run (action, payload, presenter, start, duration, outcome). This presenter only
    /// shows what the log holds, so the demo can show it.
    /// </summary>
    public class ActionLogDemoPresenter : ControlPresenterBase<IActionLogDemoView>
    {
        private readonly SampleActionLog _log;

        public ActionLogDemoPresenter(SampleActionLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ActionLogActions.Save, (int customerId) => Toasts.Show($"顧客 {customerId} を保存しました", ToastType.Success));
            Dispatcher.Register(ActionLogActions.Load, LoadAsync);
            Dispatcher.Register(ActionLogActions.Fail, () => throw new InvalidOperationException("保存先に接続できません (デモ用の例外)"));
            Dispatcher.Register(ActionLogActions.Clear, OnClear);
        }

        protected override void OnInitialize()
        {
            View.ShowExecutions(_log.Executions);
            _log.Recorded += OnRecorded;
            Disposable.Create(() => _log.Recorded -= OnRecorded).DisposeWith(Disposables);
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            await Task.Delay(1500, ct);   // its duration shows in the log; switching to another category meanwhile records Canceled
            Toasts.Show("読み込みました", ToastType.Info);
        }

        private void OnClear()
        {
            _log.Clear();
            View.ShowExecutions(_log.Executions);   // this Clear itself is recorded right after
        }

        private void OnRecorded(object sender, ActionExecution execution) => View.AddExecution(execution);
    }
}
