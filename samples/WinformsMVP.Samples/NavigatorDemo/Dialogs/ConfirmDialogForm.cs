using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class ConfirmDialogForm : Form, IConfirmDialogView
    {
        private Label _messageLabel;
        private Button _yesButton;
        private Button _noButton;
        private Button _cancelButton;
        private ViewActionBinder _binder;

        public ConfirmDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Confirm";
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

            _yesButton = new Button
            {
                Text = "Yes",
                Location = new Point(60, 100),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Yes
            };

            _noButton = new Button
            {
                Text = "No",
                Location = new Point(150, 100),
                Size = new Size(80, 30),
                DialogResult = DialogResult.No
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(240, 100),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                _messageLabel, _yesButton, _noButton, _cancelButton
            });

            this.CancelButton = _cancelButton;
        }

        public void SetTitle(string title)
        {
            this.Text = title;
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public void SetDefaultChoice(bool yesIsDefault)
        {
            this.AcceptButton = yesIsDefault ? _yesButton : _noButton;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (ConfirmDialogActions.Yes, _yesButton),
                (ConfirmDialogActions.No, _noButton),
                (ConfirmDialogActions.Cancel, _cancelButton)
            );
        }

    }
}
