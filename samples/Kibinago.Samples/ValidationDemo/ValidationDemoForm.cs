using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Validation;
using Kibinago.ViewActions;

namespace Kibinago.Samples.ValidationDemo
{
    /// <summary>
    /// The view side of the validation flow: an <see cref="ErrorProvider"/> wrapped in an
    /// <see cref="ErrorProviderDisplay"/> that maps each model property to its control. The
    /// display shows a field's errors once the user has left that control, and every error after
    /// Submit / Validate All. Errors not tied to a field appear on the Submit button. The form has
    /// no validation logic and no per-field error code.
    /// </summary>
    public partial class ValidationDemoForm : Form, IValidationDemoView
    {
        private TextBox _userNameTextBox;
        private TextBox _emailTextBox;
        private TextBox _passwordTextBox;
        private TextBox _confirmPasswordTextBox;
        private NumericUpDown _ageNumeric;
        private TextBox _phoneTextBox;

        private Button _submitButton;
        private Button _resetButton;
        private Button _validateAllButton;

        private ErrorProvider _errorProvider;
        private ControlErrorDisplay _errors;
        private ViewActionBinder _binder;

        public ValidationDemoForm()
        {
            InitializeComponent();
            InitializeValidationDisplay();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Validation Demo - Complex Validation Patterns";
            this.Size = new Size(700, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int labelX = 30;
            int fieldX = 200;
            int fieldWidth = 400;
            int rowHeight = 40;
            int currentY = 30;

            var titleLabel = new Label
            {
                Text = "Registration Form with Validation",
                Location = new Point(30, currentY),
                Size = new Size(600, 25),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(titleLabel);
            currentY += 30;

            var hintLabel = new Label
            {
                Text = "Errors appear next to a field after you leave it. Submit or Validate All shows every error. " +
                       "Try the reserved user name \"admin\".",
                Location = new Point(30, currentY),
                Size = new Size(620, 35),
                ForeColor = Color.DimGray
            };
            this.Controls.Add(hintLabel);
            currentY += 45;

            _userNameTextBox = CreateTextRow("User Name:", labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _emailTextBox = CreateTextRow("Email:", labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _passwordTextBox = CreateTextRow("Password:", labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _passwordTextBox.UseSystemPasswordChar = true;
            _confirmPasswordTextBox = CreateTextRow("Confirm Password:", labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            _confirmPasswordTextBox.UseSystemPasswordChar = true;

            this.Controls.Add(CreateRowLabel("Age:", labelX, currentY));
            _ageNumeric = new NumericUpDown
            {
                Location = new Point(fieldX, currentY),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 150
            };
            _ageNumeric.ValueChanged += OnFieldChanged;
            this.Controls.Add(_ageNumeric);
            currentY += rowHeight;

            _phoneTextBox = CreateTextRow("Phone Number (Optional):", labelX, fieldX, fieldWidth, ref currentY, rowHeight);
            currentY += 20;

            _submitButton = CreateButton("Submit", labelX, currentY, 150, Color.FromArgb(40, 167, 69));
            _validateAllButton = CreateButton("Validate All", labelX + 160, currentY, 150, Color.FromArgb(0, 120, 215));
            _resetButton = CreateButton("Reset", labelX + 320, currentY, 150, Color.Gray);
        }

        private void InitializeValidationDisplay()
        {
            _errorProvider = new ErrorProvider(this) { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            Disposed += (s, e) => _errorProvider.Dispose();

            _errors = new ErrorProviderDisplay(_errorProvider)
                .Map(nameof(RegistrationModel.UserName), _userNameTextBox)
                .Map(nameof(RegistrationModel.Email), _emailTextBox)
                .Map(nameof(RegistrationModel.Password), _passwordTextBox)
                .Map(nameof(RegistrationModel.ConfirmPassword), _confirmPasswordTextBox)
                .Map(nameof(RegistrationModel.Age), _ageNumeric)
                .Map(nameof(RegistrationModel.PhoneNumber), _phoneTextBox)
                .Map(ValidationState.FormLevel, _submitButton);
        }

        private TextBox CreateTextRow(string labelText, int labelX, int fieldX, int fieldWidth, ref int currentY, int rowHeight)
        {
            this.Controls.Add(CreateRowLabel(labelText, labelX, currentY));

            var textBox = new TextBox
            {
                Location = new Point(fieldX, currentY),
                Size = new Size(fieldWidth, 25)
            };
            textBox.TextChanged += OnFieldChanged;
            this.Controls.Add(textBox);

            currentY += rowHeight;
            return textBox;
        }

        private static Label CreateRowLabel(string text, int x, int y) => new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(165, 20),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

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

        private void OnFieldChanged(object sender, EventArgs e) => DataChanged?.Invoke(this, EventArgs.Empty);

        #region IValidationDemoView Implementation

        public IViewActionBinder ActionBinder => _binder;

        public IValidationErrorDisplay ValidationErrors => _errors;

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

        public event EventHandler DataChanged;

        #endregion
    }
}
