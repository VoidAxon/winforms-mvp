using System;
using System.Collections.Generic;
using WinformsMVP.MVP.Views;

namespace MultiProjectDemo.UserModule
{
    public interface IUserListView : IWindowView
    {
        void SetUsers(IReadOnlyList<User> users);
        User SelectedUser { get; }
        bool HasSelection { get; }
        event EventHandler SelectionChanged;
    }
}
