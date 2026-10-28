using System.Threading;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MdiDemo
{
    public static class DocumentActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Document");

        public static readonly ViewAction Process = Factory.Create(nameof(Process));
    }

    /// <summary>A document window. ShowBusy in an MDI child locks only that child: the parent and the other children stay usable.</summary>
    public class DocumentPresenter : WindowPresenterBase<IDocumentView, int>
    {
        protected override void RegisterViewActions() =>
            Dispatcher.Register(DocumentActions.Process, async ct =>
            {
                using (ShowBusy("この文書だけ処理中…"))
                {
                    await Task.Run(() => Thread.Sleep(3000), ct);
                }
                View.ShowStatus("処理が終わりました。");
            });

        protected override void OnInitialize(int number)
        {
            View.Title = $"文書 {number}";
            View.ShowStatus("「処理」の 3 秒間、この子ウィンドウだけが処理中になります。");
        }
    }
}
