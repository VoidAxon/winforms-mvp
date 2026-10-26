using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;   // Navigator.For(...) and the other service interfaces

namespace MyMvpApp
{
    /// <summary>
    /// The main window's use cases. A presenter never touches WinForms types: it talks to the
    /// window through <see cref="IMainView"/> and to the user through Messages / Dialogs.
    /// </summary>
    public sealed class MainPresenter : WindowPresenterBase<IMainView>
    {
        protected override void RegisterViewActions()
        {
            Dispatcher.Register(MainActions.SayHello, OnSayHello);
            // Closes like X: goes through CanClose (override it to ask before exiting).
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
