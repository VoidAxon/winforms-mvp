using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MdiDemo
{
    public static class MdiHostActions
    {
        private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("MdiHost");

        public static readonly ViewAction OpenDocument = Factory.Create(nameof(OpenDocument));
        public static readonly ViewAction OpenFirst = Factory.Create(nameof(OpenFirst));
    }

    /// <summary>
    /// PrepareWindow: everything ShowWindow does (create the view, attach, initialize, close gate, key)
    /// except showing it. The container view places and shows it — here as an MDI child.
    /// </summary>
    public class MdiHostPresenter : WindowPresenterBase<IMdiHostView>
    {
        private int _next = 1;

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(MdiHostActions.OpenDocument, () => Open(_next++));
            Dispatcher.Register(MdiHostActions.OpenFirst, () => Open(1));
        }

        protected override void OnInitialize() =>
            View.ShowStatus("「文書を開く」で子ウィンドウ。子の「処理」はその子だけを処理中にします (MDI)。");

        private void Open(int number)
        {
            // key: one window per document number. An open one is returned as it is (not activated).
            var document = Navigator.For(new DocumentPresenter()).WithParam(number)
                .PrepareWindow(key: number);
            if (document == null)
                return;   // the opening was cancelled
            View.ShowDocument(document);
            View.ShowStatus($"文書 {number} (同じ番号はキーで 1 つだけ)。");
        }
    }
}
