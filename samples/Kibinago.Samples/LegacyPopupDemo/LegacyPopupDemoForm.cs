using System;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.ViewActions;

namespace Kibinago.Samples.LegacyPopupDemo
{
    /// <summary>
    /// The MVP screen. <see cref="EditCustomer"/> is where the legacy form is opened: creation, the
    /// values it takes and returns, and disposal all stay here.
    /// </summary>
    public sealed partial class LegacyPopupDemoForm : Form, ILegacyPopupDemoView
    {
        private readonly ViewActionBinder _binder = new ViewActionBinder();

        public LegacyPopupDemoForm()
        {
            InitializeComponent();
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
