using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary
{
    /// <summary>
    /// View interface for order summary control
    /// </summary>
    public interface IOrderSummaryView : IViewBase
    {
        /// <summary>
        /// Gets or sets the list of order items
        /// </summary>
        IList<OrderItem> OrderItems { get; set; }

        /// <summary>
        /// Gets or sets the total amount
        /// </summary>
        decimal TotalAmount { get; set; }

        /// <summary>
        /// Gets the currently selected order item
        /// </summary>
        OrderItem SelectedItem { get; }

        /// <summary>
        /// Gets whether an item is selected
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Gets whether the order has any items
        /// </summary>
        bool HasItems { get; }

        /// <summary>
        /// Event raised when an item is removed from the order
        /// </summary>
        event EventHandler<OrderItemRemovedEventArgs> ItemRemoved;

        /// <summary>
        /// Event raised when the total amount changes
        /// </summary>
        event EventHandler<TotalChangedEventArgs> TotalChanged;

        /// <summary>
        /// Event raised when selection changes
        /// </summary>
        event EventHandler SelectionChanged;

        /// <summary>
        /// Raises the ItemRemoved event (called by Presenter)
        /// </summary>
        void RaiseItemRemoved(OrderItem item);

        /// <summary>
        /// Raises the TotalChanged event (called by Presenter)
        /// </summary>
        void RaiseTotalChanged(decimal oldTotal, decimal newTotal);
    }

    /// <summary>
    /// Event args for ItemRemoved event
    /// </summary>
    public class OrderItemRemovedEventArgs : EventArgs
    {
        public OrderItem Item { get; set; }

        public OrderItemRemovedEventArgs(OrderItem item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Event args for TotalChanged event
    /// </summary>
    public class TotalChangedEventArgs : EventArgs
    {
        public decimal OldTotal { get; set; }
        public decimal NewTotal { get; set; }

        public TotalChangedEventArgs(decimal oldTotal, decimal newTotal)
        {
            OldTotal = oldTotal;
            NewTotal = newTotal;
        }
    }
}
