using System;
using System.Windows.Forms;
using Kibinago.Events;
using Kibinago.Views;
using Kibinago.ViewActions;
using Kibinago.Samples.ComplexInteractionDemo.ProductSelector;
using Kibinago.Samples.ComplexInteractionDemo.OrderSummary;

namespace Kibinago.Samples.ComplexInteractionDemo.OrderManagement
{
    /// <summary>
    /// Main order management form that hosts ProductSelector and OrderSummary controls
    /// </summary>
    public partial class OrderManagementControl : UserControl, IOrderManagementView
    {
        private ViewActionBinder _binder;

        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler SaveRequested;

        private ProductSelectorView _productSelector;
        private OrderSummaryView _orderSummary;

        // The panels as view interfaces: the parent presenter attaches a presenter to each.
        [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public IProductSelectorView ProductSelector => _productSelector;
        [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public IOrderSummaryView OrderSummary => _orderSummary;

        public OrderManagementControl()
        {
            InitializeComponent();
            InitializeActionBindings();
            InitializeChildControls();
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(OrderManagementActions.SaveOrder, btnSaveOrder);
            _binder.Add(OrderManagementActions.ClearOrder, btnClearOrder);
            // Framework will automatically bind

            // The service-based Presenter listens to SaveRequested rather than the
            // SaveOrder dispatch — raise the event here so both paths stay live.
            btnSaveOrder.Click += (s, e) => SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeChildControls()
        {
            // Create child controls
            _productSelector = new ProductSelectorView
            {
                Dock = DockStyle.Fill
            };

            _orderSummary = new OrderSummaryView
            {
                Dock = DockStyle.Fill
            };

            // Add to panels
            panelProductSelector.Controls.Add(_productSelector);
            panelOrderSummary.Controls.Add(_orderSummary);
        }

        public string StatusMessage
        {
            get => lblStatus.Text;
            set => lblStatus.Text = value ?? string.Empty;
        }

        public decimal CurrentTotal
        {
            get
            {
                if (decimal.TryParse(lblCurrentTotal.Text.Replace("Total: $", ""), out decimal total))
                    return total;
                return 0;
            }
            set
            {
                lblCurrentTotal.Text = $"Total: ${value:F2}";
            }
        }

        public bool CanSaveOrder => _orderSummary?.HasItems ?? false;

        public void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}
