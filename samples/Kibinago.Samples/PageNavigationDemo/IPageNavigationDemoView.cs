using System;
using System.Collections.Generic;
using Kibinago.Views;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// The page host: a breadcrumb over the current page. The view creates each page control (shown on top,
    /// the one below hidden, a crumb added) and removes it when the presenter says so; it never removes a page
    /// on its own. A crumb click is the typed action <see cref="PageNavigationDemoActions.Breadcrumb"/>
    /// (payload: the crumb's level, 0 = 顧客一覧).
    /// </summary>
    public interface IPageNavigationDemoView : IControlView
    {
        ICustomerListView PushCustomerList();
        ICustomerDetailView PushCustomerDetail(string title);
        IOrderEditView PushOrderEdit(string title);

        /// <summary>Removes the page control and its crumb and shows the page below. Tolerates a control that
        /// is already disposed.</summary>
        void RemovePage(IControlView page);

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
