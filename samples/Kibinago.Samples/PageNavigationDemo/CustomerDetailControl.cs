using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Kibinago.ViewActions;

namespace Kibinago.Samples.PageNavigationDemo
{
    public partial class CustomerDetailControl : UserControl, ICustomerDetailView
    {
        public CustomerDetailControl()
        {
            InitializeComponent();
            _orders.DoubleClick += (s, e) => OpenSelected();
            _openButton.Click += (s, e) => OpenSelected();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IViewActionBinder ActionBinder => null;

        public event EventHandler<int> OrderOpened;

        public void ShowCustomer(string name, string phone)
        {
            _nameLabel.Text = name;
            _phoneLabel.Text = "電話: " + phone;
        }

        public void ShowOrders(IReadOnlyList<DemoOrder> orders)
        {
            var selectedId = _orders.SelectedItems.Count > 0 ? (int?)((DemoOrder)_orders.SelectedItems[0].Tag).Id : null;
            _orders.BeginUpdate();
            _orders.Items.Clear();
            foreach (var order in orders)
            {
                var item = new ListViewItem(new[] { order.Id.ToString(), order.Product, order.Quantity.ToString(), order.Note }) { Tag = order };
                _orders.Items.Add(item);
                if (order.Id == selectedId) item.Selected = true;
            }
            _orders.EndUpdate();
        }

        private void OpenSelected()
        {
            var order = _orders.SelectedItems.Cast<ListViewItem>().Select(i => (DemoOrder)i.Tag).FirstOrDefault();
            if (order != null) OrderOpened?.Invoke(this, order.Id);
        }
    }
}
