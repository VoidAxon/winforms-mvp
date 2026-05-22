using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    /// <summary>
    /// Main demo form for WindowNavigator features.
    /// </summary>
    public partial class NavigatorDemoForm : Form, INavigatorDemoView
    {
        private Button _modalSimpleButton;
        private Button _modalWithResultButton;
        private Button _modalWithParamsButton;
        private Button _nonModalSimpleButton;
        private Button _nonModalWithCallbackButton;
        private Button _singletonWindowButton;
        private TextBox _logTextBox;
        private Button _clearLogButton;

        private ViewActionBinder _viewActionBinder;

        public NavigatorDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "WinForms MVP - WindowNavigator Demo";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "WindowNavigator Demo - Modal & Non-Modal Windows",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(740, 30),
                ForeColor = Color.DarkBlue
            };

            // Modal Section
            var modalGroupBox = new GroupBox
            {
                Text = "Modal Windows (Blocking)",
                Location = new Point(20, 60),
                Size = new Size(360, 200),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _modalSimpleButton = new Button
            {
                Text = "Simple Modal Dialog",
                Location = new Point(15, 30),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _modalWithResultButton = new Button
            {
                Text = "Modal with Return Value",
                Location = new Point(15, 80),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _modalWithParamsButton = new Button
            {
                Text = "Modal with Parameters & Result",
                Location = new Point(15, 130),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            modalGroupBox.Controls.AddRange(new Control[] {
                _modalSimpleButton, _modalWithResultButton, _modalWithParamsButton
            });

            // Non-Modal Section
            var nonModalGroupBox = new GroupBox
            {
                Text = "Non-Modal Windows (Non-Blocking)",
                Location = new Point(400, 60),
                Size = new Size(360, 200),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _nonModalSimpleButton = new Button
            {
                Text = "Simple Non-Modal Window",
                Location = new Point(15, 30),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _nonModalWithCallbackButton = new Button
            {
                Text = "Non-Modal with Callback",
                Location = new Point(15, 80),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _singletonWindowButton = new Button
            {
                Text = "Singleton Window (keySelector)",
                Location = new Point(15, 130),
                Size = new Size(320, 40),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            nonModalGroupBox.Controls.AddRange(new Control[] {
                _nonModalSimpleButton, _nonModalWithCallbackButton, _singletonWindowButton
            });

            // Log Section
            var logLabel = new Label
            {
                Text = "Event Log:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(20, 280),
                Size = new Size(200, 20)
            };

            _clearLogButton = new Button
            {
                Text = "Clear Log",
                Location = new Point(680, 275),
                Size = new Size(80, 25)
            };

            _logTextBox = new TextBox
            {
                Location = new Point(20, 310),
                Size = new Size(740, 220),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(240, 240, 240),
                Font = new Font("Consolas", 9f)
            };

            // Add all controls
            this.Controls.AddRange(new Control[] {
                titleLabel,
                modalGroupBox,
                nonModalGroupBox,
                logLabel,
                _clearLogButton,
                _logTextBox
            });

            // Initialize log
            AppendLog("WindowNavigator demo ready.");
            AppendLog("Try different window types to see how they work.");
            AppendLog("---");
        }

        #region INavigatorDemoView Implementation

        public void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logTextBox.AppendText($"[{timestamp}] {message}\r\n");
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();

            _viewActionBinder.AddRange(
                (NavigatorDemoActions.ShowModalSimple, _modalSimpleButton),
                (NavigatorDemoActions.ShowModalWithResult, _modalWithResultButton),
                (NavigatorDemoActions.ShowModalWithParams, _modalWithParamsButton),
                (NavigatorDemoActions.ShowNonModalSimple, _nonModalSimpleButton),
                (NavigatorDemoActions.ShowNonModalWithCallback, _nonModalWithCallbackButton),
                (NavigatorDemoActions.ShowSingletonWindow, _singletonWindowButton),
                (NavigatorDemoActions.ClearLog, _clearLogButton)
            );

        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        #endregion
    }
}
