using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
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
        public event EventHandler<CloseRequestedEventArgs<string>> CloseRequested;

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

            RaiseClose(input, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            RaiseClose(null, InteractionStatus.Cancel);
        }

        private void RaiseClose(string result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<string>(result, status));
    }

    public static class InputDialogActions
    {
        public static readonly ViewAction Ok = StandardActions.Ok;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
