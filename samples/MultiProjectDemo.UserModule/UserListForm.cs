using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.UserModule
{
    public class UserListForm : Form, IUserListView
    {
        private ListBox _listBox;
        private Button _addButton;
        private Button _editButton;
        private Button _refreshButton;
        private ViewActionBinder _binder;

        public UserListForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Text = "Users";
            Size = new Size(420, 360);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);

            _listBox = new ListBox
            {
                Location = new Point(12, 12),
                Size = new Size(380, 250),
                Font = new Font("Segoe UI", 10f),
            };
            _listBox.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);

            _addButton = new Button
            {
                Text = "Add",
                Location = new Point(12, 275),
                Size = new Size(90, 30),
            };

            _editButton = new Button
            {
                Text = "Edit",
                Location = new Point(110, 275),
                Size = new Size(90, 30),
                Enabled = false,
            };

            _refreshButton = new Button
            {
                Text = "Refresh",
                Location = new Point(302, 275),
                Size = new Size(90, 30),
            };

            Controls.AddRange(new Control[] { _listBox, _addButton, _editButton, _refreshButton });
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(UserListActions.Add, _addButton);
            _binder.Add(UserListActions.Edit, _editButton);
            _binder.Add(UserListActions.Refresh, _refreshButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        public void SetUsers(IReadOnlyList<User> users)
        {
            _listBox.BeginUpdate();
            _listBox.Items.Clear();
            foreach (var u in users)
            {
                _listBox.Items.Add(new UserRow(u));
            }
            _listBox.EndUpdate();
        }

        public User SelectedUser => (_listBox.SelectedItem as UserRow)?.User;
        public bool HasSelection => _listBox.SelectedIndex >= 0;

        public event EventHandler SelectionChanged;

        #region IWindowView plumbing

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => Activate();

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);

        #endregion

        /// <summary>Wrapper so ListBox.Items.Add can carry both the display string and the User.</summary>
        private class UserRow
        {
            public User User { get; }
            public UserRow(User user) { User = user; }
            public override string ToString() => $"#{User.Id}  {User.Name}  <{User.Email}>";
        }
    }
}
