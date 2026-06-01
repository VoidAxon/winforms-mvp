using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Singleton window - only one instance allowed (via keySelector).
    /// </summary>
    public class SingletonWindowPresenter : WindowPresenterBase<ISingletonWindowView>
    {
        private readonly int _instanceId;

        public SingletonWindowPresenter(int instanceId)
        {
            _instanceId = instanceId;
        }

        protected override void OnViewAttached()
        {
            // Nothing to do here
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(SingletonWindowActions.ShowInfo, OnShowInfo);
            _dispatcher.Register(SingletonWindowActions.Close, OnClose);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            View.SetInstanceId(_instanceId);
            View.SetMessage(
                "This is a singleton window.\n" +
                "Try clicking the 'Singleton Window' button again.\n" +
                "It will activate THIS window instead of creating a new one!");
        }

        private void OnShowInfo()
        {
            Messages.ShowInfo(
                $"This is singleton window instance #{_instanceId}.\n" +
                "Only one instance can exist at a time.",
                "Singleton Window");
        }

        private void OnClose()
        {
            if (View is System.Windows.Forms.Form form)
            {
                form.Close();
            }
        }
    }

    public static class SingletonWindowActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Singleton");

        public static readonly ViewAction ShowInfo = Factory.Create("ShowInfo");  // Business-specific, use Factory to add prefix
        public static readonly ViewAction Close = StandardActions.Close;  // Standard action, use directly
    }
}
