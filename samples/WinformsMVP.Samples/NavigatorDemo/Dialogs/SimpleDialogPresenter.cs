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
    /// Demonstrates the minimal <see cref="IRequestClose{TResult}"/> implementation:
    /// mark the Presenter with <see cref="IRequestClose{TResult}"/> and call
    /// <c>this.RequestClose(...)</c> to close with a result. No <c>CanClose(CloseReason)</c>
    /// override is needed because there is no dirty state — a user clicking X is treated as Cancel.
    /// </remarks>
    public class SimpleDialogPresenter : WindowPresenterBase<ISimpleDialogView>,
                                          IRequestClose<object>
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
            this.RequestClose(null, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            this.RequestClose(null, InteractionStatus.Cancel);
        }
    }

    public static class SimpleDialogActions
    {
        // Directly use standard actions (no prefix)
        public static readonly ViewAction Ok = StandardActions.Ok;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
