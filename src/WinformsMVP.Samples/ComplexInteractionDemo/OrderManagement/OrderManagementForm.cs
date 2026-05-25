using System;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement
{
    /// <summary>
    /// Main order management form that hosts ProductSelector and OrderSummary controls
    /// </summary>
    public partial class OrderManagementForm : Form, IOrderManagementView
    {
        private ViewActionBinder _binder;

        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler SaveRequested;

        // Child control views (public for presenter access)
        public ProductSelectorView ProductSelectorView { get; private set; }
        public OrderSummaryView OrderSummaryView { get; private set; }

        public OrderManagementForm()
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
        }

        private void InitializeChildControls()
        {
            // Create child controls
            ProductSelectorView = new ProductSelectorView
            {
                Dock = DockStyle.Fill
            };

            OrderSummaryView = new OrderSummaryView
            {
                Dock = DockStyle.Fill
            };

            // Add to panels
            panelProductSelector.Controls.Add(ProductSelectorView);
            panelOrderSummary.Controls.Add(OrderSummaryView);
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

        public bool CanSaveOrder => OrderSummaryView?.HasItems ?? false;

        public void ShowSuccessMessage(string message)
        {
            MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
    }
}
