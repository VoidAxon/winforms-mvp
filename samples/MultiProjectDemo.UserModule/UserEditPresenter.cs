using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;

namespace MultiProjectDemo.UserModule
{
    /// <summary>
    /// Demonstrates the canonical "DI for stable deps + IInitializable&lt;TParam&gt; for
    /// runtime parameters" pattern. The repository comes through the constructor (and
    /// would be resolved by the DI container in real apps); the user id comes through
    /// <see cref="OnInitialize"/> at the moment the window opens.
    /// </summary>
    public class UserEditPresenter
        : WindowPresenterBase<IUserEditView, UserEditParameters>,
          IRequestClose<UserEditResult>
    {
        private readonly IUserRepository _repository;
        private int _userId;

        public event EventHandler<CloseRequestedEventArgs<UserEditResult>> CloseRequested;

        public UserEditPresenter(IUserRepository repository)
        {
            _repository = repository;
        }

        protected override void OnViewAttached() { }

        protected override void OnInitialize(UserEditParameters parameters)
        {
            _userId = parameters.UserId;

            if (_userId == 0)
            {
                View.SetTitle("Add User");
                View.UserName = string.Empty;
                View.Email = string.Empty;
            }
            else
            {
                var user = _repository.GetById(_userId);
                View.SetTitle($"Edit User #{_userId}");
                View.UserName = user?.Name ?? string.Empty;
                View.Email = user?.Email ?? string.Empty;
            }
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(UserEditActions.Save, OnSave);
            _dispatcher.Register(UserEditActions.Cancel, OnCancel);
        }

        private void OnSave()
        {
            if (string.IsNullOrWhiteSpace(View.UserName))
            {
                Messages.ShowWarning("Name is required.", "Validation");
                return;
            }

            var user = new User
            {
                Id = _userId,
                Name = View.UserName.Trim(),
                Email = View.Email?.Trim() ?? string.Empty,
            };
            _repository.Save(user);

            RaiseClose(new UserEditResult { UserId = user.Id, Name = user.Name }, InteractionStatus.Ok);
        }

        private void OnCancel() => RaiseClose(null, InteractionStatus.Cancel);

        private void RaiseClose(UserEditResult result, InteractionStatus status)
            => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserEditResult>(result, status));
    }
}
