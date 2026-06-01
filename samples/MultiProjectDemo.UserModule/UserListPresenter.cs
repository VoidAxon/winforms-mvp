using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;

namespace MultiProjectDemo.UserModule
{
    /// <summary>
    /// Presenter for the user list. Demonstrates two patterns:
    /// <list type="bullet">
    ///   <item>Constructor injection of a business service (<see cref="IUserRepository"/>)
    ///   and a framework service (<see cref="IPresenterFactory"/>).</item>
    ///   <item>Asking <see cref="IPresenterFactory"/> for a child Presenter, then handing
    ///   it to <c>Navigator.For(...).WithParam(...)</c> — DI resolves constructor deps,
    ///   the Navigator injects runtime parameters via <c>IInitializable&lt;TParam&gt;</c>.</item>
    /// </list>
    /// </summary>
    public class UserListPresenter : WindowPresenterBase<IUserListView>
    {
        private readonly IUserRepository _repository;
        private readonly IPresenterFactory _presenterFactory;

        public UserListPresenter(IUserRepository repository, IPresenterFactory presenterFactory)
        {
            _repository = repository;
            _presenterFactory = presenterFactory;
        }

        protected override void OnViewAttached() { }

        protected override void OnInitialize()
        {
            View.SelectionChanged += (s, e) => _dispatcher.RaiseCanExecuteChanged();
            ReloadList();
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(UserListActions.Add, OnAdd);

            _dispatcher.Register(
                UserListActions.Edit,
                OnEdit,
                canExecute: () => View.HasSelection);

            _dispatcher.Register(UserListActions.Refresh, ReloadList);
        }

        private void ReloadList()
        {
            View.SetUsers(_repository.GetAll());
        }

        private void OnAdd()
        {
            var presenter = _presenterFactory.Create<UserEditPresenter>();
            var result = Navigator.For(presenter)
                                  .WithParam(new UserEditParameters { UserId = 0 })
                                  .ShowAsModal<UserEditResult>();
            if (result.IsOk) ReloadList();
        }

        private void OnEdit()
        {
            var selected = View.SelectedUser;
            if (selected == null) return;

            var presenter = _presenterFactory.Create<UserEditPresenter>();
            var result = Navigator.For(presenter)
                                  .WithParam(new UserEditParameters { UserId = selected.Id })
                                  .ShowAsModal<UserEditResult>();
            if (result.IsOk) ReloadList();
        }
    }
}
