using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class CallbackWindowForm : Form, ICallbackWindowView
    {
        private Label _messageLabel;
        private TextBox _textBox;
        private Button _saveButton;
        private Button _cancelButton;
        private ViewActionBinder _binder;

        public CallbackWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Non-Modal with Callback";
            this.Size = new Size(400, 220);
            this.StartPosition = FormStartPosition.CenterScreen;

            _messageLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(340, 40),
                TextAlign = ContentAlignment.TopLeft
            };

            _textBox = new TextBox
            {
                Location = new Point(20, 70),
                Size = new Size(340, 23)
            };

            _saveButton = new Button
            {
                Text = "Save && Close",
                Location = new Point(170, 110),
                Size = new Size(90, 30)
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(270, 110),
                Size = new Size(90, 30)
            };

            this.Controls.AddRange(new Control[] {
                _messageLabel, _textBox, _saveButton, _cancelButton
            });
        }

        public void SetMessage(string message)
        {
            _messageLabel.Text = message;
        }

        public string GetText()
        {
            return _textBox.Text;
        }

        public IViewActionBinder ActionBinder => _binder;

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.AddRange(
                (CallbackWindowActions.SaveAndClose, _saveButton),
                (CallbackWindowActions.Cancel, _cancelButton)
            );
        }

        bool IWindowView.IsDisposed => base.IsDisposed;
        void IWindowView.Activate() => this.Activate();

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
    }
}
