using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Events;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.AsyncDemo
{
    public partial class AsyncDemoForm : Form, IAsyncDemoView
    {
        private ViewActionBinder _binder;

        public AsyncDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
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

        #endregion

    }
}
