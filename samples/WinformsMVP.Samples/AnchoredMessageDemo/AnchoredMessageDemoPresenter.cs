using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demonstrates cursor-anchored feedback: the presenter calls View.ShowToast /
    /// View.ConfirmYesNo (IViewBase extensions) synchronously inside action handlers, so the
    /// feedback appears at the click point. No coordinates, controls, or WinForms types here.
    /// </summary>
    public class AnchoredMessageDemoPresenter : WindowPresenterBase<IAnchoredMessageDemoView>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(AnchoredMessageDemoActions.Save, OnSave);
            Dispatcher.Register(AnchoredMessageDemoActions.Delete, OnDelete);
            Dispatcher.Register(AnchoredMessageDemoActions.GridTouch, OnGridTouch);
        }

        private void OnSave()
        {
            View.ShowHint("Saved — toast anchored at the click point.");
            View.ShowToast("Saved!", ToastType.Success);
        }

        private void OnDelete()
        {
            if (View.ConfirmYesNo("Delete this item?", "Confirm"))
            {
                View.ShowHint("Deleted — confirmation was anchored at the click point.");
                View.ShowToast("Deleted", ToastType.Warning);
            }
            else
            {
                View.ShowHint("Delete cancelled.");
            }
        }

        private void OnGridTouch()
        {
            View.ShowHint("Grid clicked — toast anchored where you clicked.");
            View.ShowToast("Row touched", ToastType.Info);
        }
    }
}
