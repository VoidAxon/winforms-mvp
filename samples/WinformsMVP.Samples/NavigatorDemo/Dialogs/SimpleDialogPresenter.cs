using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
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
    /// declare the <c>CloseRequested</c> event and a small private helper that raises it.
    /// No subscription to <c>View.Closing</c> is needed because there is no dirty state
    /// — a user clicking X is treated as Cancel.
    /// </remarks>
    public class SimpleDialogPresenter : WindowPresenterBase<ISimpleDialogView>,
                                          IRequestClose<object>
    {
        public event EventHandler<CloseRequestedEventArgs<object>> CloseRequested;

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
            RaiseClose(null, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            RaiseClose(null, InteractionStatus.Cancel);
        }

        private void RaiseClose(object result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<object>(result, status));
    }

    public static class SimpleDialogActions
    {
        // Directly use standard actions (no prefix)
        public static readonly ViewAction Ok = StandardActions.Ok;
        public static readonly ViewAction Cancel = StandardActions.Cancel;
    }
}
