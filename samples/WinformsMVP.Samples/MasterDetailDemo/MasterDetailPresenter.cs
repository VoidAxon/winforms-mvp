using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MasterDetailDemo
{
    /// <summary>
    /// Presenter for Master-Detail pattern demonstration.
    /// Shows how to coordinate parent-child data relationships.
    /// </summary>
    public class MasterDetailPresenter : WindowPresenterBase<IMasterDetailView>
    {
        private List<CustomerModel> _customers;
        private Dictionary<int, List<OrderModel>> _ordersByCustomer;

        protected override void OnViewAttached()
        {
            // Subscribe to selection changes
            View.CustomerSelectionChanged += OnCustomerSelectionChanged;
            View.OrderSelectionChanged += OnOrderSelectionChanged;
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(
                MasterDetailActions.AddCustomer,
                OnAddCustomer);

            _dispatcher.Register(
                MasterDetailActions.EditCustomer,
                OnEditCustomer,
                canExecute: () => View.HasSelectedCustomer);

            _dispatcher.Register(
                MasterDetailActions.DeleteCustomer,
                OnDeleteCustomer,
                canExecute: () => View.HasSelectedCustomer);

            _dispatcher.Register(
                MasterDetailActions.AddOrder,
                OnAddOrder,
                canExecute: () => View.HasSelectedCustomer);

            _dispatcher.Register(
                MasterDetailActions.EditOrder,
                OnEditOrder,
                canExecute: () => View.HasSelectedOrder);

            _dispatcher.Register(
                MasterDetailActions.DeleteOrder,
                OnDeleteOrder,
                canExecute: () => View.HasSelectedOrder);

            _dispatcher.Register(
                MasterDetailActions.Refresh,
                OnRefresh);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            LoadSampleData();
            View.Customers = _customers;
        }

        private void OnCustomerSelectionChanged(object sender, EventArgs e)
        {
            // Update detail view when master selection changes
            var customer = View.SelectedCustomer;
            if (customer != null && _ordersByCustomer.TryGetValue(customer.Id, out var orders))
            {
                View.Orders = orders;
                CalculateAndDisplayTotal(orders);
            }
            else
            {
                View.Orders = Enumerable.Empty<OrderModel>();
                View.TotalAmount = "$0.00";
            }

            // Notify that CanExecute state may have changed
            _dispatcher.RaiseCanExecuteChanged();
        }

        private void OnOrderSelectionChanged(object sender, EventArgs e)
        {
            // Update UI state when detail selection changes
            _dispatcher.RaiseCanExecuteChanged();
        }

        private void CalculateAndDisplayTotal(IEnumerable<OrderModel> orders)
        {
            var total = orders.Sum(o => o.Amount);
            View.TotalAmount = $"${total:N2}";
        }

        private void OnAddCustomer()
        {
            Messages.ShowInfo(
                "Add Customer dialog would appear here.\n\n" +
                "In a real application, you would use WindowNavigator to show a modal dialog.",
                "Add Customer");
        }

        private void OnEditCustomer()
        {
            var customer = View.SelectedCustomer;
            Messages.ShowInfo(
                $"Edit Customer dialog would appear here.\n\n" +
                $"Customer: {customer.Name}\n" +
                $"Email: {customer.Email}\n" +
                $"Phone: {customer.Phone}",
                "Edit Customer");
        }

        private void OnDeleteCustomer()
        {
            var customer = View.SelectedCustomer;
            if (!Messages.ConfirmYesNo(
                $"Are you sure you want to delete customer '{customer.Name}'?\n\n" +
                "This will also delete all associated orders.",
                "Confirm Delete"))
            {
                return;
            }

            // Remove customer and their orders
            _customers.Remove(customer);
            _ordersByCustomer.Remove(customer.Id);

            // Refresh view
            View.Customers = _customers;
            View.Orders = Enumerable.Empty<OrderModel>();
            View.TotalAmount = "$0.00";

            Messages.ShowInfo($"Customer '{customer.Name}' deleted.", "Success");
        }

        private void OnAddOrder()
        {
            var customer = View.SelectedCustomer;
            Messages.ShowInfo(
                $"Add Order dialog would appear here.\n\n" +
                $"Customer: {customer.Name}",
                "Add Order");
        }

        private void OnEditOrder()
        {
            var order = View.SelectedOrder;
            Messages.ShowInfo(
                $"Edit Order dialog would appear here.\n\n" +
                $"Order #{order.Id}\n" +
                $"Date: {order.OrderDate:yyyy-MM-dd}\n" +
                $"Amount: ${order.Amount:N2}\n" +
                $"Status: {order.Status}",
                "Edit Order");
        }

        private void OnDeleteOrder()
        {
            var order = View.SelectedOrder;
            var customer = View.SelectedCustomer;

            if (!Messages.ConfirmYesNo(
                $"Are you sure you want to delete Order #{order.Id}?",
                "Confirm Delete"))
            {
                return;
            }

            // Remove order
            var orders = _ordersByCustomer[customer.Id];
            orders.Remove(order);

            // Refresh detail view
            View.Orders = orders.ToList();  // Create new list to trigger UI update
            CalculateAndDisplayTotal(orders);

            Messages.ShowInfo($"Order #{order.Id} deleted.", "Success");
        }

        private void OnRefresh()
        {
            LoadSampleData();
            View.Customers = _customers;
            View.Orders = Enumerable.Empty<OrderModel>();
            View.TotalAmount = "$0.00";

            Messages.ShowInfo("Data refreshed!", "Refresh");
        }

        private void LoadSampleData()
        {
            _customers = new List<CustomerModel>
            {
                new CustomerModel { Id = 1, Name = "Acme Corporation", Email = "contact@acme.com", Phone = "555-0101" },
                new CustomerModel { Id = 2, Name = "TechStart Inc.", Email = "info@techstart.com", Phone = "555-0102" },
                new CustomerModel { Id = 3, Name = "Global Solutions", Email = "support@globalsolutions.com", Phone = "555-0103" },
                new CustomerModel { Id = 4, Name = "Blue Ocean Ltd.", Email = "sales@blueocean.com", Phone = "555-0104" },
                new CustomerModel { Id = 5, Name = "Innovate Systems", Email = "hello@innovate.com", Phone = "555-0105" }
            };

            _ordersByCustomer = new Dictionary<int, List<OrderModel>>
            {
                [1] = new List<OrderModel>
                {
                    new OrderModel { Id = 1001, CustomerId = 1, OrderDate = DateTime.Now.AddDays(-30), Amount = 1250.00m, Status = "Completed" },
                    new OrderModel { Id = 1002, CustomerId = 1, OrderDate = DateTime.Now.AddDays(-15), Amount = 3400.50m, Status = "Completed" },
                    new OrderModel { Id = 1003, CustomerId = 1, OrderDate = DateTime.Now.AddDays(-5), Amount = 850.25m, Status = "Pending" }
                },
                [2] = new List<OrderModel>
                {
                    new OrderModel { Id = 1004, CustomerId = 2, OrderDate = DateTime.Now.AddDays(-20), Amount = 5600.00m, Status = "Completed" },
                    new OrderModel { Id = 1005, CustomerId = 2, OrderDate = DateTime.Now.AddDays(-2), Amount = 2100.75m, Status = "Processing" }
                },
                [3] = new List<OrderModel>
                {
                    new OrderModel { Id = 1006, CustomerId = 3, OrderDate = DateTime.Now.AddDays(-45), Amount = 8900.00m, Status = "Completed" },
                    new OrderModel { Id = 1007, CustomerId = 3, OrderDate = DateTime.Now.AddDays(-10), Amount = 4200.50m, Status = "Completed" }
                },
                [4] = new List<OrderModel>
                {
                    new OrderModel { Id = 1008, CustomerId = 4, OrderDate = DateTime.Now.AddDays(-7), Amount = 1500.00m, Status = "Pending" }
                },
                [5] = new List<OrderModel>
                {
                    new OrderModel { Id = 1009, CustomerId = 5, OrderDate = DateTime.Now.AddDays(-60), Amount = 12000.00m, Status = "Completed" },
                    new OrderModel { Id = 1010, CustomerId = 5, OrderDate = DateTime.Now.AddDays(-25), Amount = 3300.00m, Status = "Completed" },
                    new OrderModel { Id = 1011, CustomerId = 5, OrderDate = DateTime.Now.AddDays(-8), Amount = 7800.00m, Status = "Processing" }
                }
            };
        }
    }

    public static class MasterDetailActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("MasterDetail");

        // Master (Customer) actions
        public static readonly ViewAction AddCustomer = Factory.Create("AddCustomer");
        public static readonly ViewAction EditCustomer = Factory.Create("EditCustomer");
        public static readonly ViewAction DeleteCustomer = Factory.Create("DeleteCustomer");

        // Detail (Order) actions
        public static readonly ViewAction AddOrder = Factory.Create("AddOrder");
        public static readonly ViewAction EditOrder = Factory.Create("EditOrder");
        public static readonly ViewAction DeleteOrder = Factory.Create("DeleteOrder");

        // General actions
        public static readonly ViewAction Refresh = Factory.Create("Refresh");
    }
}
