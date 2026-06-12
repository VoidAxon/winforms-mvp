using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demonstrates interaction-point-anchored feedback: the presenter calls View.ShowToast /
    /// View.ConfirmYesNo (IViewBase extensions) synchronously inside action handlers. The
    /// feedback appears at the click point for mouse input and at the focused control for
    /// keyboard input — the presenter neither knows nor cares which; there are no coordinates,
    /// controls, or WinForms types here.
    /// </summary>
    public class AnchoredMessageDemoPresenter : WindowPresenterBase<IAnchoredMessageDemoView>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(AnchoredMessageDemoActions.Save, OnSave);
            Dispatcher.Register(AnchoredMessageDemoActions.Delete, OnDelete);
            Dispatcher.Register(AnchoredMessageDemoActions.GridTouch, OnGridTouch);
            Dispatcher.Register(AnchoredMessageDemoActions.MenuNotify, OnMenuNotify);
            Dispatcher.Register(AnchoredMessageDemoActions.ToggleNotify, OnToggleNotify);
        }

        private void OnSave()
        {
            View.ShowHint("Saved — toast anchored at the interaction point.");
            View.ShowToast($"Saved '{View.ItemName}'", ToastType.Success);
        }

        private void OnDelete()
        {
            if (View.ConfirmYesNo("Delete this item?", "Confirm"))
            {
                View.ShowHint("Deleted — confirmation was anchored at the interaction point.");
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

        private void OnMenuNotify()
        {
            View.ShowHint("Menu item — mouse: at the item; keyboard: at the focused control.");
            View.ShowToast("Hello from the menu", ToastType.Info);
        }

        private void OnToggleNotify()
        {
            View.ShowToast(
                View.NotificationsEnabled ? "Notifications enabled" : "Notifications disabled",
                ToastType.Info);
        }
    }
}
