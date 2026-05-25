using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Extensions;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MVPComparisonDemo.SupervisingController
{
    /// <summary>
    /// Supervising Controller implementation.
    ///
    /// In Supervising Controller:
    /// - Form uses DATA BINDING to connect controls to Model
    /// - When Model properties change, UI updates automatically
    /// - When user edits UI, Model is updated automatically
    /// - Form contains simple binding logic
    /// - Presenter focuses on coordination and complex logic
    /// - LESS CODE than Passive View for simple scenarios
    /// </summary>
    public partial class UserEditorForm : Form, IUserEditorView
    {
        // UI Controls
        private TextBox _nameTextBox;
        private TextBox _emailTextBox;
        private NumericUpDown _ageNumericUpDown;
        private CheckBox _isActiveCheckBox;
        private Label _errorTitleLabel;
        private Label _errorLabel;
        private Label _statusLabel;
        private Button _saveButton;
        private Button _resetButton;
        private Button _closeButton;

        private ViewActionBinder _viewActionBinder;
        private UserModel _model;

        public UserEditorForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "User Editor - Supervising Controller Pattern";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "User Editor - Supervising Controller Pattern",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(540, 30),
                ForeColor = Color.DarkGreen
            };

            // Pattern Description
            var descLabel = new Label
            {
                Text = "Supervising Controller: View binds to Model (BindableBase).\n" +
                       "UI updates automatically when model changes!",
                Location = new Point(20, 55),
                Size = new Size(540, 40),
                ForeColor = Color.DarkGray
            };

            // Name
            var nameLabel = new Label
            {
                Text = "Name:",
                Location = new Point(20, 110),
                Size = new Size(120, 20)
            };
            _nameTextBox = new TextBox
            {
                Location = new Point(150, 108),
                Size = new Size(400, 23)
            };

            // Email
            var emailLabel = new Label
            {
                Text = "Email:",
                Location = new Point(20, 145),
                Size = new Size(120, 20)
            };
            _emailTextBox = new TextBox
            {
                Location = new Point(150, 143),
                Size = new Size(400, 23)
            };

            // Age
            var ageLabel = new Label
            {
                Text = "Age:",
                Location = new Point(20, 180),
                Size = new Size(120, 20)
            };
            _ageNumericUpDown = new NumericUpDown
            {
                Location = new Point(150, 178),
                Size = new Size(100, 23),
                Minimum = 0,
                Maximum = 150,
                Value = 30
            };

            // Is Active
            _isActiveCheckBox = new CheckBox
            {
                Text = "Active User",
                Location = new Point(150, 213),
                Size = new Size(200, 20)
            };

            // Error Display
            _errorTitleLabel = new Label
            {
                Text = "Validation Errors (auto-updated):",
                Location = new Point(20, 250),
                Size = new Size(540, 20),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Visible = false  // Only show when there are errors
            };

            _errorLabel = new Label
            {
                Text = "",
                Location = new Point(20, 275),
                Size = new Size(540, 60),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9f),
                AutoSize = false
            };

            // Buttons
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(20, 350),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(16, 137, 62),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;

            _resetButton = new Button
            {
                Text = "Reset",
                Location = new Point(130, 350),
                Size = new Size(100, 35)
            };

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(240, 350),
                Size = new Size(100, 35)
            };

            // Status
            var statusPanel = new Panel
            {
                Location = new Point(0, 400),
                Size = new Size(600, 70),
                BackColor = Color.FromArgb(240, 240, 240),
                Dock = DockStyle.Bottom
            };

            _statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(20, 15),
                Size = new Size(540, 40),
                ForeColor = Color.FromArgb(64, 64, 64),
                AutoSize = false
            };

            statusPanel.Controls.Add(_statusLabel);

            // Add controls
            this.Controls.AddRange(new Control[] {
                titleLabel, descLabel,
                nameLabel, _nameTextBox,
                emailLabel, _emailTextBox,
                ageLabel, _ageNumericUpDown,
                _isActiveCheckBox,
                _errorTitleLabel, _errorLabel,
                _saveButton, _resetButton, _closeButton,
                statusPanel
            });
        }

        #region IUserEditorView Implementation - Supervising Controller Style

        /// <summary>
        /// The Model property.
        /// In Supervising Controller, when the model is set,
        /// we set up DATA BINDINGS to automatically sync UI with Model.
        /// </summary>
        public UserModel Model
        {
            get => _model;
            set
            {
                _model = value;
                SetupDataBindings();
            }
        }

        /// <summary>
        /// Set up data bindings between UI controls and Model.
        /// This is the KEY difference from Passive View!
        ///
        /// When Model properties change, UI updates automatically.
        /// When user edits UI, Model is updated automatically.
        /// </summary>
        private void SetupDataBindings()
        {
            if (_model == null) return;

            // Clear any existing bindings
            _nameTextBox.DataBindings.Clear();
            _emailTextBox.DataBindings.Clear();
            _ageNumericUpDown.DataBindings.Clear();
            _isActiveCheckBox.DataBindings.Clear();
            _errorLabel.DataBindings.Clear();

            // Bind controls to model properties using extension methods
            // These bindings are BIDIRECTIONAL and AUTOMATIC
            _nameTextBox.Bind(_model, m => m.Name);
            _emailTextBox.Bind(_model, m => m.Email);
            _ageNumericUpDown.Bind(_model, m => (decimal)m.Age);
            _isActiveCheckBox.Bind(_model, m => m.IsActive);

            // Bind error label to model's ValidationErrors property
            // When validation changes, error label updates automatically!
            _errorLabel.BindProperty(_model, m => m.ValidationErrors, nameof(_errorLabel.Text));

            // Show/hide error title based on whether there are errors
            // This demonstrates automatic UI updates based on model state
            _model.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_model.ValidationErrors))
                {
                    var hasErrors = !string.IsNullOrEmpty(_model.ValidationErrors);
                    _errorTitleLabel.Visible = hasErrors;
                }
            };

            // Set initial visibility
            _errorTitleLabel.Visible = !string.IsNullOrEmpty(_model.ValidationErrors);
        }

        public void UpdateStatus(string message)
        {
            _statusLabel.Text = message;
        }

        public IViewActionBinder ActionBinder => _viewActionBinder;

        private void InitializeActionBindings()
        {
            _viewActionBinder = new ViewActionBinder();
            _viewActionBinder.Add(UserEditorActions.Save, _saveButton);
            _viewActionBinder.Add(UserEditorActions.Reset, _resetButton);
            _viewActionBinder.Add(UserEditorActions.Close, _closeButton);
        }

        #endregion

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

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
