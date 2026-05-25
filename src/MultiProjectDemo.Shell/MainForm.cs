using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace MultiProjectDemo.Shell
{
    public class MainForm : Form, IMainView
    {
        private Button _usersButton;
        private Button _ordersButton;
        private Button _exitButton;
        private ViewActionBinder _binder;

        public MainForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            Text = "Multi-Project Demo Shell";
            Size = new Size(440, 240);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            var title = new Label
            {
                Text = "WinformsMVP — Multi-Project Demo",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(400, 26),
                ForeColor = Color.DarkBlue,
            };
            var subtitle = new Label
            {
                Text = "Each module owns its Views, Presenters, and services.",
                Location = new Point(20, 50),
                Size = new Size(400, 20),
                ForeColor = Color.DimGray,
            };

            _usersButton = new Button
            {
                Text = "Users…",
                Location = new Point(20, 90),
                Size = new Size(120, 38),
            };
            _ordersButton = new Button
            {
                Text = "Orders…",
                Location = new Point(150, 90),
                Size = new Size(120, 38),
            };
            _exitButton = new Button
            {
                Text = "Exit",
                Location = new Point(280, 90),
                Size = new Size(120, 38),
            };

            Controls.AddRange(new Control[] { title, subtitle, _usersButton, _ordersButton, _exitButton });
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(MainActions.OpenUsers,  _usersButton);
            _binder.Add(MainActions.OpenOrders, _ordersButton);
            _binder.Add(MainActions.Exit,       _exitButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        public void RequestExit() => Close();

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
