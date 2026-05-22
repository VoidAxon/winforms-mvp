using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Extensions;
using WinformsMVP.Core.Models;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// Compose email Form implementation
    /// Demonstrates ChangeTracker and validation features using Supervising Controller pattern
    /// </summary>
    public partial class ComposeEmailForm : Form, IComposeEmailView
    {
        private readonly ComposeEmailViewModel _viewModel;
        private ViewActionBinder _binder;

        // UI Controls
        private Label toLabel;
        private TextBox toTextBox;
        private Label subjectLabel;
        private TextBox subjectTextBox;
        private Label bodyLabel;
        private TextBox bodyTextBox;
        private Button sendButton;
        private Button saveDraftButton;
        private Button discardButton;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar progressBar;
        private Panel buttonPanel;

        public ComposeEmailForm()
        {
            _viewModel = new ComposeEmailViewModel();

            InitializeComponent();
            SetupUI();
            AttachEventHandlers();
            InitializeActionBindings();
            SetupDataBindings();
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            _binder.Add(ComposeEmailActions.Send, sendButton);
            _binder.Add(ComposeEmailActions.SaveDraft, saveDraftButton);
            _binder.Add(ComposeEmailActions.Discard, discardButton);

            // No Bind() call here - Presenter will call View.ActionBinder.Bind()
        }

        private void SetupDataBindings()
        {
            // Use framework's extension methods for type-safe binding with lambda expressions
            toTextBox.Bind(_viewModel, vm => vm.To);
            subjectTextBox.Bind(_viewModel, vm => vm.Subject);
            bodyTextBox.Bind(_viewModel, vm => vm.Body);

            // Subscribe to ViewModel property changes
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Raise semantic event when email data changes
            // This hides the technical INotifyPropertyChanged from the View interface
            EmailDataChanged?.Invoke(this, EventArgs.Empty);
        }

        #region IComposeEmailView Implementation

        public string To
        {
            get => _viewModel.To;
            set => _viewModel.To = value;
        }

        public string Subject
        {
            get => _viewModel.Subject;
            set => _viewModel.Subject = value;
        }

        public string Body
        {
            get => _viewModel.Body;
            set => _viewModel.Body = value;
        }

        public ComposeMode Mode
        {
            get => _viewModel.Mode;
            set => _viewModel.Mode = value;
        }

        public bool IsSaving
        {
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        progressBar.Visible = value;
                        statusLabel.Text = value ? "Sending..." : "Ready";
                    }));
                }
                else
                {
                    progressBar.Visible = value;
                    statusLabel.Text = value ? "Sending..." : "Ready";
                }
            }
        }

        public bool IsDirty
        {
            get => _viewModel.IsDirty;
            set
            {
                if (_viewModel.IsDirty != value)
                {
                    _viewModel.IsDirty = value;
                    UpdateTitle();
                }
            }
        }

        public bool EnableInput
        {
            set
            {
                toTextBox.Enabled = value;
                subjectTextBox.Enabled = value;
                bodyTextBox.Enabled = value;
                sendButton.Enabled = value;
                saveDraftButton.Enabled = value;
                discardButton.Enabled = value;
            }
        }

        public IViewActionBinder ActionBinder => _binder;

        #endregion

        #region Semantic Events

        /// <summary>
        /// Raised when email data (To, Subject, Body) changes via ViewModel binding.
        /// Follows semantic event pattern - see ToDoDemo.DataChanged, MainEmailView.EmailSelectionChanged.
        /// </summary>
        public event EventHandler EmailDataChanged;

        #endregion

        #region UI Setup

        private void InitializeComponent()
        {
            this.components = new Container();
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(600, 400);

            CreateControls();
            LayoutControls();
        }

        private void CreateControls()
        {
            // To field
            toLabel = new Label
            {
                Text = "To:",
                AutoSize = true,
                Location = new Point(20, 20)
            };

            toTextBox = new TextBox
            {
                Location = new Point(100, 17),
                Width = 650,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Subject field
            subjectLabel = new Label
            {
                Text = "Subject:",
                AutoSize = true,
                Location = new Point(20, 55)
            };

            subjectTextBox = new TextBox
            {
                Location = new Point(100, 52),
                Width = 650,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Body field
            bodyLabel = new Label
            {
                Text = "Body:",
                AutoSize = true,
                Location = new Point(20, 90)
            };

            bodyTextBox = new TextBox
            {
                Location = new Point(20, 110),
                Width = 730,
                Height = 350,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                AcceptsTab = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Button panel
            buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            sendButton = new Button
            {
                Text = "Send",
                Width = 100,
                Height = 30,
                Location = new Point(10, 10)
            };

            saveDraftButton = new Button
            {
                Text = "Save Draft",
                Width = 100,
                Height = 30,
                Location = new Point(120, 10)
            };

            discardButton = new Button
            {
                Text = "Discard",
                Width = 100,
                Height = 30,
                Location = new Point(230, 10)
            };

            buttonPanel.Controls.Add(sendButton);
            buttonPanel.Controls.Add(saveDraftButton);
            buttonPanel.Controls.Add(discardButton);

            // Status strip
            statusStrip = new StatusStrip();

            statusLabel = new ToolStripStatusLabel
            {
                Text = "Ready",
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            progressBar = new ToolStripProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(progressBar);
        }

        private void LayoutControls()
        {
            this.Controls.Add(toLabel);
            this.Controls.Add(toTextBox);
            this.Controls.Add(subjectLabel);
            this.Controls.Add(subjectTextBox);
            this.Controls.Add(bodyLabel);
            this.Controls.Add(bodyTextBox);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(statusStrip);
        }

        private void SetupUI()
        {
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            var modeText = _viewModel.Mode switch
            {
                ComposeMode.Reply => "Reply",
                ComposeMode.Forward => "Forward",
                _ => "New Message"
            };

            this.Text = _viewModel.IsDirty ? $"{modeText} *" : modeText;
        }

        private void AttachEventHandlers()
        {
            // Enter key to switch focus
            toTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    subjectTextBox.Focus();
                    e.SuppressKeyPress = true;
                }
            };

            subjectTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    bodyTextBox.Focus();
                    e.SuppressKeyPress = true;
                }
            };

            // Ctrl+Enter shortcut to send
            bodyTextBox.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    sendButton.PerformClick();
                    e.SuppressKeyPress = true;
                }
            };
        }

        #endregion

        #region IDisposable

        private IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
