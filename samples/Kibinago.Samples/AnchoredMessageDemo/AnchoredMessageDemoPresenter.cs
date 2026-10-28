using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;

namespace Kibinago.Samples.AnchoredMessageDemo
{
    /// <summary>
    /// Demonstrates the feedback division of labor. Default: <c>Toasts.Show</c> (corner)
    /// — it is correct no matter how the action was triggered (mouse, mnemonic, shortcut, or
    /// code). Anchored feedback appears only where position carries meaning, and then through a
    /// semantic view method (<c>View.ConfirmDelete()</c>, <c>View.ShowRowTouched()</c>) whose
    /// implementation picks the anchor. No coordinates or WinForms types here.
    /// </summary>
    public class AnchoredMessageDemoPresenter : ControlPresenterBase<IAnchoredMessageDemoView>
    {
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
            // A command reachable by button, mnemonic, and Ctrl+S alike: the corner toast is the
            // choice that is right for every one of them.
            View.ShowHint("Saved — corner toast: correct for mouse, keyboard, and code triggers.");
            Toasts.Show($"Saved '{View.ItemName}'", ToastType.Success);
        }

        private void OnDelete()
        {
            // Position matters here (the confirmation belongs to the Delete button), so the view
            // owns the placement via a semantic method.
            if (View.ConfirmDelete())
            {
                View.ShowHint("Deleted — the view anchored the confirmation at the Delete button.");
                Toasts.Show("Deleted", ToastType.Warning);
            }
            else
            {
                View.ShowHint("Delete cancelled.");
            }
        }

        private void OnGridTouch()
        {
            // Position matters here too (feedback belongs to the touched row).
            View.ShowHint("Grid — the view anchored the toast at the current row.");
            View.ShowRowTouched();
        }

        private void OnMenuNotify()
        {
            View.ShowHint("Menu command — corner toast.");
            Toasts.Show("Hello from the menu", ToastType.Info);
        }

        private void OnToggleNotify()
        {
            Toasts.Show(
                View.NotificationsEnabled ? "Notifications enabled" : "Notifications disabled",
                ToastType.Info);
        }
    }
}
