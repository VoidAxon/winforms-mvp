using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Events;
using Kibinago.Views;
using Kibinago.Common;
using Kibinago.ViewActions;

namespace Kibinago.Samples.EmailDemo
{
    /// <summary>
    /// Compose email Form implementation
    /// Demonstrates ChangeTracker and validation features using Supervising Controller pattern
    /// </summary>
    public partial class ComposeEmailForm : Form, IComposeEmailView
    {
        private readonly ComposeEmailViewModel _viewModel;
        private ViewActionBinder _binder;

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
