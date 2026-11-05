using System;
using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// A breadcrumb over the current page. The pages are shown by a PageHost (it creates each page control from the
    /// view mapping — the three page controls are registered at startup); the view only draws the breadcrumb from the
    /// host's titles. A crumb click is the typed action <see cref="PageNavigationDemoActions.Breadcrumb"/> (payload:
    /// the crumb's level, 0 = 顧客一覧).
    /// </summary>
    public interface IPageNavigationDemoView : IControlView
    {
        /// <summary>Where the pages are shown.</summary>
        IPageHost Pages { get; }

        void ShowStatus(string text);
    }

    /// <summary>One row of the customer list.</summary>
    public sealed class CustomerRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public int TotalQuantity { get; set; }
    }

    public interface ICustomerListView : IControlView
    {
        void ShowCustomers(IReadOnlyList<CustomerRow> rows);

        /// <summary>A customer was opened (double click / 開く): its id.</summary>
        event EventHandler<int> CustomerOpened;
    }

    public interface ICustomerDetailView : IControlView
    {
        void ShowCustomer(string name, string phone);
        void ShowOrders(IReadOnlyList<DemoOrder> orders);

        /// <summary>An order was opened: its id.</summary>
        event EventHandler<int> OrderOpened;
    }

    public interface IOrderEditView : IControlView
    {
        string Product { get; set; }
        int Quantity { get; set; }
        string Note { get; set; }
        bool IsDirty { set; }

        /// <summary>The user changed one of the fields.</summary>
        event EventHandler Edited;
    }
}
