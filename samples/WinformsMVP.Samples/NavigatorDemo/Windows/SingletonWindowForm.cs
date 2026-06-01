using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public class SingletonWindowForm : Form, ISingletonWindowView
    {
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _infoButton;
        private Button _closeButton;
        private ViewActionBinder _binder;

        public SingletonWindowForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Singleton Window";
            this.Size = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterScreen;

            _titleLabel = new Label
            {
                Text = "Instance #?",
                Location = new Point(20, 20),
                Size = new Size(340, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            _messageLabel = new Label
            {
                Location = new Point(20, 60),
                Size = new Size(340, 80),
                TextAlign = ContentAlignment.TopCenter
            };

            _infoButton = new Button
            {
                Text = "Show Info",
                Location = new Point(100, 160),
                Size = new Size(90, 30)
            };

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(200, 160),
                Size = new Size(90, 30)
            };

            this.Controls.AddRange(new Control[] {
                _titleLabel, _messageLabel, _infoButton, _closeButton
            });
        }

        public void SetInstanceId(int id)
        {
            _titleLabel.Text = $"Instance #{id}";
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
                (SingletonWindowActions.ShowInfo, _infoButton),
                (SingletonWindowActions.Close, _closeButton)
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
