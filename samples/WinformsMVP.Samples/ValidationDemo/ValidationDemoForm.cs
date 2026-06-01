using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.ValidationDemo
{
    public partial class ValidationDemoForm : Form, IValidationDemoView
    {
        private TextBox _userNameTextBox;
        private TextBox _emailTextBox;
        private TextBox _passwordTextBox;
        private TextBox _confirmPasswordTextBox;
        private NumericUpDown _ageNumeric;
        private TextBox _phoneTextBox;

        private Label _userNameErrorLabel;
        private Label _emailErrorLabel;
        private Label _passwordErrorLabel;
        private Label _confirmPasswordErrorLabel;
        private Label _ageErrorLabel;
        private Label _phoneErrorLabel;

        private TextBox _validationSummaryTextBox;

        private Button _submitButton;
        private Button _resetButton;
        private Button _validateAllButton;

        private ViewActionBinder _binder;

        private readonly Dictionary<string, Label> _errorLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, Control> _fieldControls = new Dictionary<string, Control>();

        public ValidationDemoForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Validation Demo - Complex Validation Patterns";
            this.Size = new Size(700, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int labelX = 30;
            int fieldX = 200;
            int fieldWidth = 400;
            int startY = 30;
            int rowHeight = 70;
            int currentY = startY;

            // Title
            var titleLabel = new Label
            {
                Text = "Registration Form with Validation",
                Location = new Point(30, currentY),
                Size = new Size(600, 25),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(titleLabel);
            currentY += 40;

            // User Name
            CreateFieldRow("User Name:", ref _userNameTextBox, ref _userNameErrorLabel,
                labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _userNameTextBox.TextChanged += (s, e) => UserNameChanged?.Invoke(this, EventArgs.Empty);
            _fieldControls["UserName"] = _userNameTextBox;
            _errorLabels["UserName"] = _userNameErrorLabel;

            // Email
            CreateFieldRow("Email:", ref _emailTextBox, ref _emailErrorLabel,
                labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _emailTextBox.TextChanged += (s, e) => EmailChanged?.Invoke(this, EventArgs.Empty);
            _fieldControls["Email"] = _emailTextBox;
            _errorLabels["Email"] = _emailErrorLabel;

            // Password
            CreateFieldRow("Password:", ref _passwordTextBox, ref _passwordErrorLabel,
                labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _passwordTextBox.UseSystemPasswordChar = true;
            _passwordTextBox.TextChanged += (s, e) => PasswordChanged?.Invoke(this, EventArgs.Empty);
            _fieldControls["Password"] = _passwordTextBox;
            _errorLabels["Password"] = _passwordErrorLabel;

            // Confirm Password
            CreateFieldRow("Confirm Password:", ref _confirmPasswordTextBox, ref _confirmPasswordErrorLabel,
                labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _confirmPasswordTextBox.UseSystemPasswordChar = true;
            _confirmPasswordTextBox.TextChanged += (s, e) => ConfirmPasswordChanged?.Invoke(this, EventArgs.Empty);
            _fieldControls["ConfirmPassword"] = _confirmPasswordTextBox;
            _errorLabels["ConfirmPassword"] = _confirmPasswordErrorLabel;

            // Age
            var ageLabel = new Label
            {
                Text = "Age:",
                Location = new Point(labelX, currentY),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(ageLabel);

            _ageNumeric = new NumericUpDown
            {
                Location = new Point(fieldX, currentY),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 150
            };
            _ageNumeric.ValueChanged += (s, e) => AgeChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_ageNumeric);

            _ageErrorLabel = new Label
            {
                Location = new Point(fieldX, currentY + 25),
                Size = new Size(fieldWidth, 40),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 8f)
            };
            this.Controls.Add(_ageErrorLabel);
            _fieldControls["Age"] = _ageNumeric;
            _errorLabels["Age"] = _ageErrorLabel;
            currentY += rowHeight;

            // Phone Number (Optional)
            var phoneHintLabel = new Label
            {
                Text = "Phone Number (Optional):",
                Location = new Point(labelX, currentY),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(phoneHintLabel);

            _phoneTextBox = new TextBox
            {
                Location = new Point(fieldX, currentY),
                Size = new Size(fieldWidth, 25)
            };
            _phoneTextBox.TextChanged += (s, e) => PhoneNumberChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(_phoneTextBox);

            _phoneErrorLabel = new Label
            {
                Location = new Point(fieldX, currentY + 25),
                Size = new Size(fieldWidth, 40),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 8f)
            };
            this.Controls.Add(_phoneErrorLabel);
            _fieldControls["PhoneNumber"] = _phoneTextBox;
            _errorLabels["PhoneNumber"] = _phoneErrorLabel;
            currentY += rowHeight + 10;

            // Validation Summary
            var summaryLabel = new Label
            {
                Text = "Validation Summary:",
                Location = new Point(labelX, currentY),
                Size = new Size(200, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(summaryLabel);
            currentY += 25;

            _validationSummaryTextBox = new TextBox
            {
                Location = new Point(labelX, currentY),
                Size = new Size(620, 80),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(255, 250, 240),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(_validationSummaryTextBox);
            currentY += 90;

            // Buttons
            _submitButton = CreateButton("Submit", labelX, currentY, 150, Color.FromArgb(40, 167, 69));
            _validateAllButton = CreateButton("Validate All", labelX + 160, currentY, 150, Color.FromArgb(0, 120, 215));
            _resetButton = CreateButton("Reset", labelX + 320, currentY, 150, Color.Gray);
        }

        private void CreateFieldRow(string labelText, ref TextBox textBox, ref Label errorLabel,
            int labelX, int fieldX, int fieldWidth, ref int currentY, int rowHeight)
        {
            var label = new Label
            {
                Text = labelText,
                Location = new Point(labelX, currentY),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            this.Controls.Add(label);

            textBox = new TextBox
            {
                Location = new Point(fieldX, currentY),
                Size = new Size(fieldWidth, 25)
            };
            this.Controls.Add(textBox);

            errorLabel = new Label
            {
                Location = new Point(fieldX, currentY + 25),
                Size = new Size(fieldWidth, 40),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 8f)
            };
            this.Controls.Add(errorLabel);

            currentY += rowHeight;
        }

        private Button CreateButton(string text, int x, int y, int width, Color backColor)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 40),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            button.FlatAppearance.BorderSize = 0;
            this.Controls.Add(button);
            return button;
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            _binder.Add(ValidationActions.Submit, _submitButton);
            _binder.Add(ValidationActions.Reset, _resetButton);
            _binder.Add(ValidationActions.ValidateAll, _validateAllButton);
        }

        #region IValidationDemoView Implementation

        public IViewActionBinder ActionBinder => _binder;

        public string UserName
        {
            get => _userNameTextBox.Text;
            set => _userNameTextBox.Text = value;
        }

        public string Email
        {
            get => _emailTextBox.Text;
            set => _emailTextBox.Text = value;
        }

        public string Password
        {
            get => _passwordTextBox.Text;
            set => _passwordTextBox.Text = value;
        }

        public string ConfirmPassword
        {
            get => _confirmPasswordTextBox.Text;
            set => _confirmPasswordTextBox.Text = value;
        }

        public int Age
        {
            get => (int)_ageNumeric.Value;
            set => _ageNumeric.Value = value;
        }

        public string PhoneNumber
        {
            get => _phoneTextBox.Text;
            set => _phoneTextBox.Text = value;
        }

        public bool HasValidationErrors =>
            _errorLabels.Values.Any(label => !string.IsNullOrWhiteSpace(label.Text));

        public void SetFieldError(string fieldName, string errorMessage)
        {
            if (_errorLabels.TryGetValue(fieldName, out var errorLabel))
            {
                errorLabel.Text = errorMessage;
            }

            if (_fieldControls.TryGetValue(fieldName, out var control))
            {
                control.BackColor = Color.FromArgb(255, 240, 240);  // Light red
            }
        }

        public void ClearFieldError(string fieldName)
        {
            if (_errorLabels.TryGetValue(fieldName, out var errorLabel))
            {
                errorLabel.Text = string.Empty;
            }

            if (_fieldControls.TryGetValue(fieldName, out var control))
            {
                control.BackColor = Color.White;
            }
        }

        public void ClearAllErrors()
        {
            foreach (var errorLabel in _errorLabels.Values)
            {
                errorLabel.Text = string.Empty;
            }

            foreach (var control in _fieldControls.Values)
            {
                control.BackColor = Color.White;
            }

            _validationSummaryTextBox.Text = string.Empty;
        }

        public void ShowValidationSummary(IEnumerable<string> errors)
        {
            _validationSummaryTextBox.Text = string.Join(Environment.NewLine, errors);
        }

        public event EventHandler UserNameChanged;
        public event EventHandler EmailChanged;
        public event EventHandler PasswordChanged;
        public event EventHandler ConfirmPasswordChanged;
        public event EventHandler AgeChanged;
        public event EventHandler PhoneNumberChanged;

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
