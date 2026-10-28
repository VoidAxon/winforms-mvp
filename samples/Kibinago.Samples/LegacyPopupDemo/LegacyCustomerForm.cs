using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kibinago.Samples.LegacyPopupDemo
{
    /// <summary>
    /// Legacy old form - Not MVP pattern
    ///
    /// This represents old code in the project that cannot or is inconvenient to modify to MVP architecture.
    /// An MVP screen opens it from a semantic view method (ILegacyPopupDemoView.EditCustomer); the
    /// presenter never sees this type. Like many legacy forms, it takes and returns its values
    /// through public properties.
    /// </summary>
    public partial class LegacyCustomerForm : Form
    {
        // Public properties for callers to retrieve results
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }

        public LegacyCustomerForm()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // The values the caller set before ShowDialog.
            _customerNameTextBox.Text = CustomerName ?? "";
            _emailTextBox.Text = Email ?? "";
            _ageNumericUpDown.Value = Math.Max(_ageNumericUpDown.Minimum, Math.Min(_ageNumericUpDown.Maximum, Age));
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
