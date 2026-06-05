using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.AsyncDemo
{
    public partial class AsyncDemoForm : Form, IAsyncDemoView
    {
        private Label _titleLabel;
        private Label _statusLabel;
        private Label _elapsedTimeLabel;
        private ProgressBar _progressBar;
        private ListBox _resultsListBox;
        private Button _startSimpleButton;
        private Button _startLongButton;
        private Button _startProgressButton;
        private Button _startErrorButton;
        private Button _cancelButton;
        private Button _clearButton;
        private ViewActionBinder _binder;

        public AsyncDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Async Operations Demo - Async/Await Patterns";
            this.Size = new Size(800, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            int marginX = 30;
            int currentY = 20;

            // Title
            _titleLabel = new Label
            {
                Text = "Async/Await Patterns in MVP",
                Location = new Point(marginX, currentY),
                Size = new Size(700, 30),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(_titleLabel);
            currentY += 40;

            // Info label
            var infoLabel = new Label
            {
                Text = "Demonstrates async operations with progress tracking, cancellation, and error handling.\n" +
                       "All operations use proper async/await patterns in the Presenter.",
                Location = new Point(marginX, currentY),
                Size = new Size(700, 40),
                ForeColor = Color.DarkGray
            };
            this.Controls.Add(infoLabel);
            currentY += 50;

            // Status section
            var statusHeaderLabel = new Label
            {
                Text = "Status:",
                Location = new Point(marginX, currentY),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(statusHeaderLabel);

            _statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(marginX + 110, currentY),
                Size = new Size(600, 20),
                ForeColor = Color.DarkGreen,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(_statusLabel);
            currentY += 30;

            // Elapsed time
            _elapsedTimeLabel = new Label
            {
                Location = new Point(marginX, currentY),
                Size = new Size(300, 20),
                ForeColor = Color.Blue
            };
            this.Controls.Add(_elapsedTimeLabel);
            currentY += 30;

            // Progress bar
            _progressBar = new ProgressBar
            {
                Location = new Point(marginX, currentY),
                Size = new Size(720, 25),
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };
            this.Controls.Add(_progressBar);
            currentY += 40;

            // Operation buttons
            var buttonGroupLabel = new Label
            {
                Text = "Async Operations:",
                Location = new Point(marginX, currentY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(buttonGroupLabel);
            currentY += 30;

            int btnY = currentY;
            int btnSpacing = 190;

            _startSimpleButton = CreateButton(
                "Simple Async\n(2 seconds)",
                marginX, btnY, 180, 60,
                Color.FromArgb(0, 120, 215));

            _startLongButton = CreateButton(
                "Long Operation\n(20 items, cancellable)",
                marginX + btnSpacing, btnY, 180, 60,
                Color.FromArgb(40, 167, 69));

            _startProgressButton = CreateButton(
                "With Progress\n(50 items, progress bar)",
                marginX + btnSpacing * 2, btnY, 180, 60,
                Color.FromArgb(255, 140, 0));

            _startErrorButton = CreateButton(
                "Simulate Error\n(error handling demo)",
                marginX + btnSpacing * 3, btnY, 180, 60,
                Color.FromArgb(220, 53, 69));

            currentY += 70;

            // Control buttons
            btnY = currentY;
            _cancelButton = CreateButton(
                "Cancel Operation",
                marginX, btnY, 180, 40,
                Color.FromArgb(108, 117, 125));

            _clearButton = CreateButton(
                "Clear Results",
                marginX + btnSpacing, btnY, 180, 40,
                Color.Gray);

            currentY += 60;

            // Results section
            var resultsLabel = new Label
            {
                Text = "Results:",
                Location = new Point(marginX, currentY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(resultsLabel);
            currentY += 25;

            _resultsListBox = new ListBox
            {
                Location = new Point(marginX, currentY),
                Size = new Size(720, 200),
                Font = new Font("Consolas", 8.5f)
            };
            this.Controls.Add(_resultsListBox);
        }

        private Button CreateButton(string text, int x, int y, int width, int height, Color backColor)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f)
            };
            button.FlatAppearance.BorderSize = 0;
            this.Controls.Add(button);
            return button;
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            _binder.Add(AsyncActions.StartSimpleOperation, _startSimpleButton);
            _binder.Add(AsyncActions.StartLongOperation, _startLongButton);
            _binder.Add(AsyncActions.StartWithProgress, _startProgressButton);
            _binder.Add(AsyncActions.StartWithError, _startErrorButton);
            _binder.Add(AsyncActions.Cancel, _cancelButton);
            _binder.Add(AsyncActions.Clear, _clearButton);
        }

        #region IAsyncDemoView Implementation

        public IViewActionBinder ActionBinder => _binder;

        public string StatusMessage
        {
            get => _statusLabel.Text;
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => _statusLabel.Text = value));
                }
                else
                {
                    _statusLabel.Text = value;
                }
            }
        }

        public int ProgressPercentage
        {
            get => _progressBar.Value;
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => _progressBar.Value = Math.Min(100, Math.Max(0, value))));
                }
                else
                {
                    _progressBar.Value = Math.Min(100, Math.Max(0, value));
                }
            }
        }

        public bool ShowProgress
        {
            get => _progressBar.Visible;
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => _progressBar.Visible = value));
                }
                else
                {
                    _progressBar.Visible = value;
                }
            }
        }

        public IEnumerable<string> ResultData
        {
            get => _resultsListBox.Items.Cast<string>();
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        _resultsListBox.Items.Clear();
                        if (value != null)
                        {
                            foreach (var item in value)
                            {
                                _resultsListBox.Items.Add(item);
                            }
                        }
                    }));
                }
                else
                {
                    _resultsListBox.Items.Clear();
                    if (value != null)
                    {
                        foreach (var item in value)
                        {
                            _resultsListBox.Items.Add(item);
                        }
                    }
                }
            }
        }

        public string ElapsedTime
        {
            get => _elapsedTimeLabel.Text;
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => _elapsedTimeLabel.Text = value));
                }
                else
                {
                    _elapsedTimeLabel.Text = value;
                }
            }
        }

        private bool _isOperationRunning;
        public bool IsOperationRunning
        {
            get => _isOperationRunning;
            set => _isOperationRunning = value;
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
