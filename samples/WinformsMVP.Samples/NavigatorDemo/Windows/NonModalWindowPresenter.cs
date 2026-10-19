using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Simple non-modal window.
    /// </summary>
    public class NonModalWindowPresenter : WindowPresenterBase<INonModalWindowView>
    {
        private int _counter = 0;

        protected override void OnViewAttached()
        {
            // Nothing to do here
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(NonModalWindowActions.Increment, OnIncrement);
            Dispatcher.Register(NonModalWindowActions.Close, OnClose);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.SetCounter(_counter);
        }

        private void OnIncrement()
        {
            _counter++;
            View.SetCounter(_counter);
        }

        private void OnClose()
        {
            // Non-modal windows can just close themselves
            RequestClose(InteractionStatus.Cancel);
        }
    }

    public static class NonModalWindowActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("NonModal");

        public static readonly ViewAction Increment = Factory.Create("Increment");
        public static readonly ViewAction Close = Factory.Create("Close");
    }
}
