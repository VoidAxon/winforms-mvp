using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class ConfirmDialogParameters
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public bool DefaultYes { get; set; }
    }

    /// <summary>
    /// Confirm dialog - takes parameters, returns <see cref="bool"/>.
    /// </summary>
    public class ConfirmDialogPresenter :
        WindowPresenterBase<IConfirmDialogView, ConfirmDialogParameters>,
        IRequestClose<bool>
    {
        public event EventHandler<CloseRequestedEventArgs<bool>> CloseRequested;

        protected override void OnViewAttached()
        {
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(ConfirmDialogActions.Yes, OnYes);
            Dispatcher.Register(ConfirmDialogActions.No, OnNo);
            Dispatcher.Register(ConfirmDialogActions.Cancel, OnCancel);
        }

        protected override void OnInitialize(ConfirmDialogParameters parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            View.SetTitle(parameters.Title);
            View.SetMessage(parameters.Message);
            View.SetDefaultChoice(parameters.DefaultYes);
        }

        private void OnYes()    => RaiseClose(true, InteractionStatus.Ok);
        private void OnNo()     => RaiseClose(false, InteractionStatus.Ok);
        private void OnCancel() => RaiseClose(false, InteractionStatus.Cancel);

        private void RaiseClose(bool result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<bool>(result, status));
    }

    public static class ConfirmDialogActions
    {
        public static readonly ViewAction Yes = StandardActions.Yes;
        public static readonly ViewAction No = StandardActions.No;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
