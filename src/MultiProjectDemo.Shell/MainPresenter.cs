using MultiProjectDemo.OrderModule;
using MultiProjectDemo.UserModule;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;

namespace MultiProjectDemo.Shell
{
    /// <summary>
    /// Shell Presenter — only knows about <see cref="IPresenterFactory"/>, never about
    /// concrete module dependencies. It asks the factory for a module Presenter and
    /// hands it to the Navigator; the DI container resolves the rest.
    /// </summary>
    public class MainPresenter : WindowPresenterBase<IMainView>
    {
        private readonly IPresenterFactory _presenters;

        public MainPresenter(IPresenterFactory presenters)
        {
            _presenters = presenters;
        }

        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(MainActions.OpenUsers, OnOpenUsers);
            _dispatcher.Register(MainActions.OpenOrders, OnOpenOrders);
            _dispatcher.Register(MainActions.Exit, OnExit);
        }

        private void OnOpenUsers()
        {
            var presenter = _presenters.Create<UserListPresenter>();
            Navigator.For(presenter).ShowAsModal();
        }

        private void OnOpenOrders()
        {
            var presenter = _presenters.Create<OrderListPresenter>();
            Navigator.For(presenter).ShowAsModal();
        }

        private void OnExit() => View.RequestExit();
    }
}
