using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace MyMvpApp
{
    /// <summary>
    /// The main window's use cases. A presenter never touches WinForms types: it talks to the
    /// window through <see cref="IMainView"/> and to the user through Messages / Dialogs.
    /// </summary>
    public sealed class MainPresenter : WindowPresenterBase<IMainView>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(MainActions.SayHello, OnSayHello);
            Dispatcher.Register(MainActions.Exit, () => RequestClose(InteractionStatus.Ok));
        }

        protected override void OnInitialize()
        {
            View.StatusText = "Ready";
        }

        private void OnSayHello()
        {
            Messages.ShowInfo("Hello from MainPresenter!");
            View.StatusText = "Said hello";
        }
    }
}
