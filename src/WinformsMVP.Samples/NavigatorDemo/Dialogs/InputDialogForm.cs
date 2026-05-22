using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class InputDialogForm : Form, IInputDialogView
    {
        private Label _promptLabel;
        private TextBox _inputTextBox;
        private Button _okButton;
        private Button _cancelButton;
        private ViewActionBinder _binder;

        public InputDialogForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Input Dialog";
            this.Size = new Size(400, 180);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _promptLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(340, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _inputTextBox = new TextBox
            {
                Location = new Point(20, 50),
                Size = new Size(340, 23)
            };

            _okButton = new Button
            {
                Text = "OK",
                Location = new Point(170, 90),
                Size = new Size(90, 30),
                DialogResult = DialogResult.OK
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(270, 90),
                Size = new Size(90, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                _promptLabel, _inputTextBox, _okButton, _cancelButton
            });

            this.AcceptButton = _okButton;
            this.CancelButton = _cancelButton;
        }

        public void SetPrompt(string prompt)
        {
            _promptLabel.Text = prompt;
        }

        public string GetInput()
        {
            return _inputTextBox.Text;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (InputDialogActions.Ok, _okButton),
                (InputDialogActions.Cancel, _cancelButton)
            );
        }

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => this.Activate();
    }
}
