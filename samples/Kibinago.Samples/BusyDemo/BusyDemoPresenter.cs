using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.BusyDemo
{
    public static class BusyDemoActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("BusyDemo");

        public static readonly ViewAction RunSynchronously = Factory.Create(nameof(RunSynchronously));
        public static readonly ViewAction RunAsync = Factory.Create(nameof(RunAsync));
        public static readonly ViewAction RunOnWorker = Factory.Create(nameof(RunOnWorker));
    }

    /// <summary>
    /// ShowBusy: <c>using (ShowBusy("…")) { … }</c> around the work. While it is open the window shows the
    /// wait cursor and takes no input; with <c>mvp.Busy.UseOverlay()</c> (this sample's Program.cs) an overlay
    /// with the message appears after a short delay. No busy flag, no Cursor, no try/finally.
    /// </summary>
    public class BusyDemoPresenter : ControlPresenterBase<IBusyDemoView>
    {
        protected override void RegisterViewActions()
        {
            Dispatcher.Register(BusyDemoActions.RunSynchronously, RunSynchronously);
            Dispatcher.Register(BusyDemoActions.RunAsync, RunAsync);
            Dispatcher.Register(BusyDemoActions.RunOnWorker, RunOnWorker);
        }

        protected override void OnInitialize() =>
            View.ShowResult("ボタンを押すと、数秒の処理の間メイン画面全体が「処理中」になります (サイドバーも押せません)。");

        // Synchronous work on the UI thread: the wait cursor shows and input is blocked, but the overlay
        // cannot animate (the UI thread is busy) — move long work off the UI thread (next button).
        private void RunSynchronously()
        {
            var watch = Stopwatch.StartNew();
            using (ShowBusy("同期処理中…"))
            {
                Thread.Sleep(2000);
            }
            View.ShowResult($"同期処理: {watch.ElapsedMilliseconds} ms。待ちカーソルだけで、オーバーレイは描けません (UI スレッドが止まっているため)。");
        }

        // The usual form: the work runs on the thread pool, the UI stays responsive and the overlay animates.
        // Lifetime is cancelled if the presenter is disposed (here: when another category is chosen).
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            using (ShowBusy("集計中…"))
            {
                await Task.Run(() => Thread.Sleep(3000), cancellationToken);
            }
            View.ShowResult($"await Task.Run: {watch.ElapsedMilliseconds} ms。オーバーレイが回り、画面は応答し続けます。");
        }

        // ShowBusy may be called from any thread: from a worker it is posted to the UI thread.
        private void RunOnWorker()
        {
            View.ShowResult("ワーカースレッドで処理しています…");
            Task.Run(() =>
            {
                using (ShowBusy("ワーカーで処理中…"))
                {
                    Thread.Sleep(2500);
                }
            });
        }
    }
}
