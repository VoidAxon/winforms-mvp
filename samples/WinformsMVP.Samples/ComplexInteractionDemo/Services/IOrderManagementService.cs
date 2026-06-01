using System;
using System.Collections.Generic;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.Services
{
    /// <summary>
    /// Service interface for managing order business logic.
    /// This is the shared layer that replaces direct Presenter-to-Presenter communication.
    /// </summary>
    public interface IOrderManagementService
    {
        /// <summary>
        /// Gets all items currently in the order
        /// </summary>
        IReadOnlyList<OrderItem> OrderItems { get; }

        /// <summary>
        /// Adds a product to the order
        /// </summary>
        /// <param name="product">Product to add</param>
        /// <param name="quantity">Quantity to add</param>
        void AddProduct(Product product, int quantity);

        /// <summary>
        /// Removes an item from the order
        /// </summary>
        /// <param name="item">Item to remove</param>
        void RemoveItem(OrderItem item);

        /// <summary>
        /// Clears all items from the order
        /// </summary>
        void ClearOrder();

        /// <summary>
        /// Gets the current total amount
        /// </summary>
        decimal GetTotal();

        /// <summary>
        /// Event raised when a product is added to the order
        /// </summary>
        event EventHandler<ProductAddedEventArgs> ProductAdded;

        /// <summary>
        /// Event raised when an item is removed from the order
        /// </summary>
        event EventHandler<ItemRemovedEventArgs> ItemRemoved;

        /// <summary>
        /// Event raised when the total amount changes
        /// </summary>
        event EventHandler<TotalChangedEventArgs> TotalChanged;

        /// <summary>
        /// Event raised when the order is cleared
        /// </summary>
        event EventHandler OrderCleared;
    }

    /// <summary>
    /// Event args for ProductAdded event
    /// </summary>
    public class ProductAddedEventArgs : EventArgs
    {
        public Product Product { get; }
        public int Quantity { get; }

        public ProductAddedEventArgs(Product product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Event args for ItemRemoved event
    /// </summary>
    public class ItemRemovedEventArgs : EventArgs
    {
        public OrderItem Item { get; }

        public ItemRemovedEventArgs(OrderItem item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Event args for TotalChanged event
    /// </summary>
    public class TotalChangedEventArgs : EventArgs
    {
        public decimal OldTotal { get; }
        public decimal NewTotal { get; }

        public TotalChangedEventArgs(decimal oldTotal, decimal newTotal)
        {
            OldTotal = oldTotal;
            NewTotal = newTotal;
        }
    }
}
