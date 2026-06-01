using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Non-modal window with callback result.
    /// </summary>
    /// <remarks>
    /// The framework calls <c>Form.Close()</c> automatically when
    /// <see cref="IRequestClose{TResult}.CloseRequested"/> is raised — there is no need for
    /// the Presenter to touch the Form directly.
    /// </remarks>
    public class CallbackWindowPresenter : WindowPresenterBase<ICallbackWindowView>,
                                            IRequestClose<string>
    {
        public event EventHandler<CloseRequestedEventArgs<string>> CloseRequested;

        protected override void OnViewAttached()
        {
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(CallbackWindowActions.SaveAndClose, OnSaveAndClose);
            Dispatcher.Register(CallbackWindowActions.Cancel, OnCancel);
        }

        protected override void OnInitialize()
        {
            View.SetMessage("Enter some text and click 'Save & Close'.\nThe main window will receive the result via callback.");
        }

        private void OnSaveAndClose()
        {
            var text = View.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                Messages.ShowWarning("Please enter some text.", "Input Required");
                return;
            }

            RaiseClose(text, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            RaiseClose(null, InteractionStatus.Cancel);
        }

        private void RaiseClose(string result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<string>(result, status));
    }

    public static class CallbackWindowActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Callback");

        public static readonly ViewAction SaveAndClose = Factory.Create("SaveAndClose");
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
