using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary
{
    /// <summary>
    /// UserControl for displaying order summary
    /// </summary>
    public partial class OrderSummaryView : UserControl, IOrderSummaryView
    {
        private ViewActionBinder _binder;
        private List<OrderItem> _orderItems = new List<OrderItem>();

        public IViewActionBinder ActionBinder => _binder;

        public event EventHandler<OrderItemRemovedEventArgs> ItemRemoved;
        public event EventHandler<TotalChangedEventArgs> TotalChanged;
        public event EventHandler SelectionChanged;

        public OrderSummaryView()
        {
            InitializeComponent();
            InitializeActionBindings();

            // Wire up events
            listOrderItems.SelectedIndexChanged += (s, e) => SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();
            _binder.Add(OrderSummaryActions.RemoveItem, btnRemove);
            _binder.Add(OrderSummaryActions.ClearAll, btnClearAll);
            // Framework will automatically bind
        }

        public IList<OrderItem> OrderItems
        {
            get => _orderItems;
            set
            {
                _orderItems = value?.ToList() ?? new List<OrderItem>();
                RefreshOrderList();
            }
        }

        public decimal TotalAmount
        {
            get
            {
                if (decimal.TryParse(lblTotalValue.Text.Replace("$", ""), out decimal total))
                    return total;
                return 0;
            }
            set
            {
                lblTotalValue.Text = $"${value:F2}";
            }
        }

        public OrderItem SelectedItem => listOrderItems.SelectedItem as OrderItem;

        public bool HasSelection => listOrderItems.SelectedItem != null;

        public bool HasItems => listOrderItems.Items.Count > 0;

        public void RaiseItemRemoved(OrderItem item)
        {
            ItemRemoved?.Invoke(this, new OrderItemRemovedEventArgs(item));
        }

        public void RaiseTotalChanged(decimal oldTotal, decimal newTotal)
        {
            TotalChanged?.Invoke(this, new TotalChangedEventArgs(oldTotal, newTotal));
        }

        private void RefreshOrderList()
        {
            listOrderItems.Items.Clear();
            if (_orderItems != null)
            {
                foreach (var item in _orderItems)
                {
                    listOrderItems.Items.Add(item);
                }
            }
        }
    }
}
