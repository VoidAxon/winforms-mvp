using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MasterDetailDemo
{
    public partial class MasterDetailForm : Form, IMasterDetailView
    {
        private ListBox _customerListBox;
        private ListBox _orderListBox;
        private Label _customerLabel;
        private Label _orderLabel;
        private Label _totalLabel;
        private Label _totalAmountLabel;
        private Button _addCustomerButton;
        private Button _editCustomerButton;
        private Button _deleteCustomerButton;
        private Button _addOrderButton;
        private Button _editOrderButton;
        private Button _deleteOrderButton;
        private Button _refreshButton;
        private ViewActionBinder _binder;

        public MasterDetailForm()
        {
            InitializeComponent();
            InitializeActionBindings();
        }

        private void InitializeComponent()
        {
            this.Text = "Master-Detail Demo - Customer Orders";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);

            // Customer section (Master)
            _customerLabel = new Label
            {
                Text = "Customers (Master)",
                Location = new Point(20, 20),
                Size = new Size(400, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            _customerListBox = new ListBox
            {
                Location = new Point(20, 45),
                Size = new Size(400, 300),
                DisplayMember = nameof(CustomerModel.Name)
            };
            _customerListBox.SelectedIndexChanged += (s, e) =>
            {
                CustomerSelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            // Customer buttons
            int btnY = 355;
            _addCustomerButton = CreateButton("Add Customer", 20, btnY, 120);
            _editCustomerButton = CreateButton("Edit Customer", 150, btnY, 120);
            _deleteCustomerButton = CreateButton("Delete Customer", 280, btnY, 135);

            // Order section (Detail)
            _orderLabel = new Label
            {
                Text = "Orders (Detail)",
                Location = new Point(450, 20),
                Size = new Size(400, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold)
            };

            _orderListBox = new ListBox
            {
                Location = new Point(450, 45),
                Size = new Size(400, 300)
            };
            _orderListBox.SelectedIndexChanged += (s, e) =>
            {
                OrderSelectionChanged?.Invoke(this, EventArgs.Empty);
            };

            // Order buttons
            _addOrderButton = CreateButton("Add Order", 450, btnY, 120);
            _editOrderButton = CreateButton("Edit Order", 580, btnY, 120);
            _deleteOrderButton = CreateButton("Delete Order", 710, btnY, 135);

            // Total section
            _totalLabel = new Label
            {
                Text = "Total Orders:",
                Location = new Point(450, 400),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _totalAmountLabel = new Label
            {
                Text = "$0.00",
                Location = new Point(560, 400),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };

            // Refresh button
            _refreshButton = new Button
            {
                Text = "Refresh All Data",
                Location = new Point(20, 500),
                Size = new Size(150, 40),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _refreshButton.FlatAppearance.BorderSize = 0;

            // Add all controls
            this.Controls.AddRange(new Control[]
            {
                _customerLabel,
                _customerListBox,
                _addCustomerButton,
                _editCustomerButton,
                _deleteCustomerButton,
                _orderLabel,
                _orderListBox,
                _addOrderButton,
                _editOrderButton,
                _deleteOrderButton,
                _totalLabel,
                _totalAmountLabel,
                _refreshButton
            });
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

        #region IWindowView Implementation

        bool IWindowView.IsDisposed => base.IsDisposed;

        void IWindowView.Activate()
        {
            this.Activate();
        }

        private EventHandler<WindowClosingEventArgs> _closing;
        event EventHandler<WindowClosingEventArgs> IWindowView.Closing
        {
            add => _closing += value;
            remove => _closing -= value;
        }
        void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);

        #endregion
    }
}
