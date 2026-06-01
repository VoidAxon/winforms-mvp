using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinformsMVP.Samples.ExecutionRequestDemo
{
    /// <summary>
    /// Legacy old form - Not MVP pattern
    ///
    /// This represents old code in the project that cannot or is inconvenient to modify to MVP architecture.
    /// Through the ExecutionRequest pattern, it can be opened from new MVP forms.
    /// </summary>
    public class LegacyCustomerForm : Form
    {
        private TextBox _customerNameTextBox;
        private TextBox _emailTextBox;
        private NumericUpDown _ageNumericUpDown;
        private Button _saveButton;
        private Button _cancelButton;

        // Public properties for callers to retrieve results
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }

        public LegacyCustomerForm()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void InitializeComponent()
        {
            this.Text = "Customer Information Editor (Legacy Form - Non-MVP)";
            this.Size = new Size(450, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title
            var titleLabel = new Label
            {
                Text = "⚠️ This is a legacy old form (Non-MVP pattern)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.DarkOrange,
                Location = new Point(20, 20),
                Size = new Size(400, 30)
            };

            var infoLabel = new Label
            {
                Text = "This form represents old code in the project, integrated via ExecutionRequest pattern",
                ForeColor = Color.Gray,
                Location = new Point(20, 50),
                Size = new Size(400, 20)
            };

            // Customer name
            var nameLabel = new Label
            {
                Text = "Customer Name:",
                Location = new Point(30, 90),
                Size = new Size(100, 20)
            };
            _customerNameTextBox = new TextBox
            {
                Location = new Point(140, 88),
                Size = new Size(270, 23)
            };

            // Email
            var emailLabel = new Label
            {
                Text = "Email:",
                Location = new Point(30, 125),
                Size = new Size(100, 20)
            };
            _emailTextBox = new TextBox
            {
                Location = new Point(140, 123),
                Size = new Size(270, 23)
            };

            // Age
            var ageLabel = new Label
            {
                Text = "Age:",
                Location = new Point(30, 160),
                Size = new Size(100, 20)
            };
            _ageNumericUpDown = new NumericUpDown
            {
                Location = new Point(140, 158),
                Size = new Size(100, 23),
                Minimum = 18,
                Maximum = 120,
                Value = 30
            };

            // Buttons
            _saveButton = new Button
            {
                Text = "Save",
                Location = new Point(240, 210),
                Size = new Size(80, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _saveButton.FlatAppearance.BorderSize = 0;

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(330, 210),
                Size = new Size(80, 35)
            };

            // Add controls
            this.Controls.AddRange(new Control[] {
                titleLabel, infoLabel,
                nameLabel, _customerNameTextBox,
                emailLabel, _emailTextBox,
                ageLabel, _ageNumericUpDown,
                _saveButton, _cancelButton
            });
        }

        private void SetupEventHandlers()
        {
            _saveButton.Click += OnSaveClick;
            _cancelButton.Click += OnCancelClick;
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            // Simple validation
            if (string.IsNullOrWhiteSpace(_customerNameTextBox.Text))
            {
                MessageBox.Show("Please enter customer name", "Validation Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_emailTextBox.Text))
            {
                MessageBox.Show("Please enter email", "Validation Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save data to public properties
            CustomerName = _customerNameTextBox.Text;
            Email = _emailTextBox.Text;
            Age = (int)_ageNumericUpDown.Value;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
