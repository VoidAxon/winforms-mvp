using System;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.ViewActions;

namespace Kibinago.Samples.LegacyPopupDemo
{
    /// <summary>
    /// The MVP screen. <see cref="EditCustomer"/> is where the legacy form is opened: creation, the
    /// values it takes and returns, and disposal all stay here.
    /// </summary>
    public sealed class LegacyPopupDemoForm : Form, ILegacyPopupDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();
        private readonly Label _customerLabel;
        private readonly Label _statusLabel;
        private readonly Button _editButton;

        public LegacyPopupDemoForm()
        {
            Text = "Legacy popup from an MVP screen";
            ClientSize = new Size(460, 170);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Controls.Add(new Label
            {
                Text = "The 編集 button opens LegacyCustomerForm (not MVP) through a view method.",
                Location = new Point(16, 16), Size = new Size(430, 20),
            });
            _customerLabel = new Label { Text = "(no customer yet)", Location = new Point(16, 48), Size = new Size(430, 24), Font = new Font(Font, FontStyle.Bold) };
            _editButton = new Button { Text = "編集", Location = new Point(16, 84), Size = new Size(100, 30) };
            _statusLabel = new Label { Location = new Point(16, 128), Size = new Size(430, 24), ForeColor = Color.DimGray };
            Controls.Add(_customerLabel);
            Controls.Add(_editButton);
            Controls.Add(_statusLabel);

            _binder.Add(LegacyPopupDemoActions.EditCustomer, _editButton);
        }

        public IViewActionBinder ActionBinder => _binder;

        public InteractionResult<CustomerData> EditCustomer(CustomerData current)
        {
            using (var legacy = new LegacyCustomerForm())
            {
                // The legacy form takes and returns its values through public properties.
                legacy.CustomerName = current?.Name;
                legacy.Email = current?.Email;
                legacy.Age = current?.Age ?? 30;

                if (legacy.ShowDialog(this) != DialogResult.OK)
                    return InteractionResult<CustomerData>.Cancel();

                return InteractionResult<CustomerData>.Ok(new CustomerData
                {
                    Name = legacy.CustomerName, Email = legacy.Email, Age = legacy.Age,
                });
            }
        }

        public void ShowCustomer(CustomerData customer) => _customerLabel.Text = customer?.ToString() ?? "(no customer yet)";

        public void ShowStatus(string message) => _statusLabel.Text = message;
    }
}
