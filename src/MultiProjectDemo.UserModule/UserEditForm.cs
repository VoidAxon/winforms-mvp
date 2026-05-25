using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.UserModule
{
    public class UserEditForm : Form, IUserEditView
    {
        private TextBox _nameBox;
        private TextBox _emailBox;
        private Button _saveButton;
        private Button _cancelButton;
        private ViewActionBinder _binder;

        public UserEditForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Size = new Size(360, 200);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Segoe UI", 9f);

            Controls.Add(new Label { Text = "Name:",  Location = new Point(12, 15), Size = new Size(60, 20) });
            Controls.Add(new Label { Text = "Email:", Location = new Point(12, 45), Size = new Size(60, 20) });

            _nameBox  = new TextBox { Location = new Point(80, 12), Size = new Size(250, 23) };
            _emailBox = new TextBox { Location = new Point(80, 42), Size = new Size(250, 23) };

            _saveButton   = new Button { Text = "Save",   Location = new Point(154, 110), Size = new Size(85, 30) };
            _cancelButton = new Button { Text = "Cancel", Location = new Point(245, 110), Size = new Size(85, 30) };

            Controls.AddRange(new Control[] { _nameBox, _emailBox, _saveButton, _cancelButton });

            AcceptButton = _saveButton;
            CancelButton = _cancelButton;
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(UserEditActions.Save, _saveButton);
            _binder.Add(UserEditActions.Cancel, _cancelButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        public string UserName
        {
            get => _nameBox.Text;
            set => _nameBox.Text = value;
        }

        public string Email
        {
            get => _emailBox.Text;
            set => _emailBox.Text = value;
        }

        public void SetTitle(string title) => Text = title;

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
    }
}
