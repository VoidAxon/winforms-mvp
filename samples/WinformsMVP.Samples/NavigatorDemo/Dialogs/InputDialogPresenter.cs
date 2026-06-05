using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Input dialog - returns a string value through <see cref="IRequestClose{TResult}"/>.
    /// </summary>
    public class InputDialogPresenter : WindowPresenterBase<IInputDialogView>,
                                         IRequestClose<string>
    {
        protected override void OnViewAttached()
        {
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(InputDialogActions.Ok, OnOk);
            Dispatcher.Register(InputDialogActions.Cancel, OnCancel);
        }

        protected override void OnInitialize()
        {
            View.SetPrompt("Please enter your name:");
        }

        private void OnOk()
        {
            var input = View.GetInput();
            if (string.IsNullOrWhiteSpace(input))
            {
                Messages.ShowWarning("Please enter a value.", "Input Required");
                return;
            }

            this.RequestClose(input, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            this.RequestClose(null, InteractionStatus.Cancel);
        }
    }

    public static class InputDialogActions
    {
        public static readonly ViewAction Ok = StandardActions.Ok;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
