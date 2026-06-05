using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Simple modal dialog - no parameters, no return value.
    /// </summary>
    /// <remarks>
    /// Demonstrates the minimal push-close implementation: call <c>RequestClose(...)</c>
    /// (the protected base method) to close with a status. No <c>CanClose(CloseReason)</c>
    /// override is needed because there is no dirty state — a user clicking X is treated as Cancel.
    /// </remarks>
    public class SimpleDialogPresenter : WindowPresenterBase<ISimpleDialogView>
    {
        protected override void OnViewAttached()
        {
            // No close-handling needed: no dirty state to protect.
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(SimpleDialogActions.Ok, OnOk);
            Dispatcher.Register(SimpleDialogActions.Cancel, OnCancel);
        }

        protected override void OnInitialize()
        {
            View.SetMessage("This is a simple modal dialog.\nClick OK to confirm or Cancel to dismiss.");
        }

        private void OnOk()
        {
            Messages.ShowInfo("You clicked OK!", "Simple Dialog");
            RequestClose(InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            RequestClose(InteractionStatus.Cancel);
        }
    }

    public static class SimpleDialogActions
    {
        // Directly use standard actions (no prefix)
        public static readonly ViewAction Ok = StandardActions.Ok;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
