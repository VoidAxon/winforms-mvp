using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MasterDetailDemo
{
    /// <summary>
    /// Master-Detail pattern view - shows a list of customers with order details.
    /// Demonstrates parent-child data relationships and coordinated UI updates.
    /// </summary>
    public interface IMasterDetailView : IWindowView
    {
        /// <summary>
        /// Gets or sets the list of customers (Master data).
        /// </summary>
        IEnumerable<CustomerModel> Customers { get; set; }

        /// <summary>
        /// Gets the currently selected customer.
        /// </summary>
        CustomerModel SelectedCustomer { get; }

        /// <summary>
        /// Gets whether a customer is currently selected.
        /// </summary>
        bool HasSelectedCustomer { get; }

        /// <summary>
        /// Gets or sets the orders for the selected customer (Detail data).
        /// </summary>
        IEnumerable<OrderModel> Orders { get; set; }

        /// <summary>
        /// Gets the currently selected order.
        /// </summary>
        OrderModel SelectedOrder { get; }

        /// <summary>
        /// Gets whether an order is currently selected.
        /// </summary>
        bool HasSelectedOrder { get; }

        /// <summary>
        /// Sets the total order amount for the selected customer.
        /// </summary>
        string TotalAmount { set; }

        /// <summary>
        /// Raised when the selected customer changes.
        /// </summary>
        event EventHandler CustomerSelectionChanged;

        /// <summary>
        /// Raised when the selected order changes.
        /// </summary>
        event EventHandler OrderSelectionChanged;
    }

    public class CustomerModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public override string ToString() => $"{Name} ({Email})";
    }

    public class OrderModel
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }

        public override string ToString() =>
            $"Order #{Id} - {OrderDate:yyyy-MM-dd} - ${Amount:N2} ({Status})";
    }
}
