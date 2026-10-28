using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.MasterDetailDemo
{
    public partial class MasterDetailControl : UserControl, IMasterDetailView
    {
        private ViewActionBinder _binder;

        public MasterDetailControl()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private Button CreateButton(string text, int x, int y, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 35),
                BackColor = Color.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat
            };
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            _binder.Add(MasterDetailActions.AddCustomer, _addCustomerButton);
            _binder.Add(MasterDetailActions.EditCustomer, _editCustomerButton);
            _binder.Add(MasterDetailActions.DeleteCustomer, _deleteCustomerButton);
            _binder.Add(MasterDetailActions.AddOrder, _addOrderButton);
            _binder.Add(MasterDetailActions.EditOrder, _editOrderButton);
            _binder.Add(MasterDetailActions.DeleteOrder, _deleteOrderButton);
            _binder.Add(MasterDetailActions.Refresh, _refreshButton);
        }

        #region IMasterDetailView Implementation

        public IViewActionBinder ActionBinder => _binder;

        public IEnumerable<CustomerModel> Customers
        {
            get => _customerListBox.DataSource as IEnumerable<CustomerModel>;
            set
            {
                _customerListBox.DataSource = value?.ToList();
            }
        }

        public CustomerModel SelectedCustomer =>
            _customerListBox.SelectedItem as CustomerModel;

        public bool HasSelectedCustomer =>
            _customerListBox.SelectedItem != null;

        public IEnumerable<OrderModel> Orders
        {
            get => _orderListBox.DataSource as IEnumerable<OrderModel>;
            set
            {
                _orderListBox.DataSource = value?.ToList();
            }
        }

        public OrderModel SelectedOrder =>
            _orderListBox.SelectedItem as OrderModel;

        public bool HasSelectedOrder =>
            _orderListBox.SelectedItem != null;

        public string TotalAmount
        {
            set => _totalAmountLabel.Text = value;
        }

        public event EventHandler CustomerSelectionChanged;
        public event EventHandler OrderSelectionChanged;

        #endregion

    }
}
