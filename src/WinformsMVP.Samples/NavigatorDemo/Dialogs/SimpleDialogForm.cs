using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class SimpleDialogForm : Form, ISimpleDialogView
    {
        private Label _messageLabel;
        private Button _okButton;
        private Button _cancelButton;
        private ViewActionBinder _binder;

        public SimpleDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Simple Dialog";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _messageLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(340, 60),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(100, 100),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(210, 100),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                _messageLabel, _okButton, _cancelButton
            });

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (SimpleDialogActions.Ok, _okButton),
                (SimpleDialogActions.Cancel, _cancelButton)
            );
        }

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => this.Activate();
    }
}
