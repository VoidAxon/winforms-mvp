using System;
using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MVPComparisonDemo.PassiveView
{
    /// <summary>
    /// Passive View implementation.
    ///
    /// In Passive View:
    /// - Form is COMPLETELY passive - just a dumb container
    /// - NO data binding
    /// - NO business logic in the form
    /// - Properties simply get/set TextBox.Text, CheckBox.Checked, etc.
    /// - Presenter controls everything explicitly
    /// </summary>
    public partial class UserEditorForm : Form, IUserEditorView
    {
        // UI Controls
        private TextBox _nameTextBox;
        private TextBox _emailTextBox;
        private NumericUpDown _ageNumericUpDown;
        private CheckBox _isActiveCheckBox;
        private Label _errorLabel;
        private Label _statusLabel;
        private Button _saveButton;
        private Button _resetButton;
        private Button _closeButton;

        private ViewActionBinder _viewActionBinder;

        public UserEditorForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "User Editor - Passive View Pattern";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Title
            var titleLabel = new Label
            {
                Text = "User Editor - Passive View Pattern",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                Location = new Point(20, 20),
                Size = new Size(540, 30),
                ForeColor = Color.DarkBlue
            };

            // Pattern Description
            var descLabel = new Label
            {
                Text = "Passive View: Presenter controls ALL view properties.\n" +
                       "No data binding - everything is explicit.",
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
            _errorLabel = new Label
            {
                Text = "",
                Location = new Point(20, 250),
                Size = new Size(540, 80),
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
                BackColor = Color.FromArgb(0, 120, 215),
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
                _errorLabel,
                _saveButton, _resetButton, _closeButton,
                statusPanel
            });
        }

        #region IUserEditorView Implementation - Passive View Style

        // In Passive View, these properties just expose the control values
        // Presenter reads/writes them explicitly - NO data binding

        public string UserName
        {
            get => _nameTextBox.Text;
            set => _nameTextBox.Text = value;
        }

        public string Email
        {
            get => _emailTextBox.Text;
            set => _emailTextBox.Text = value;
        }

        public int Age
        {
            get => (int)_ageNumericUpDown.Value;
            set => _ageNumericUpDown.Value = value;
        }

        public bool IsActive
        {
            get => _isActiveCheckBox.Checked;
            set => _isActiveCheckBox.Checked = value;
        }

        public void ShowValidationErrors(string errors)
        {
            _errorLabel.Text = errors;
            _errorLabel.Visible = true;
        }

        public void ClearValidationErrors()
        {
            _errorLabel.Text = "";
            _errorLabel.Visible = false;
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

    }
}
