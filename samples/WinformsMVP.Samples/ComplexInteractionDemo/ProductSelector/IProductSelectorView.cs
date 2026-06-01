using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector
{
    /// <summary>
    /// View interface for product selection control
    /// </summary>
    public interface IProductSelectorView : IViewBase
    {
        /// <summary>
        /// Gets or sets the list of available products
        /// </summary>
        IList<Product> Products { get; set; }

        /// <summary>
        /// Gets the currently selected product (null if none selected)
        /// </summary>
        Product SelectedProduct { get; }

        /// <summary>
        /// Gets or sets the quantity to add
        /// </summary>
        int Quantity { get; set; }

        /// <summary>
        /// Gets whether a product is currently selected
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Gets whether the quantity is valid (> 0)
        /// </summary>
        bool HasValidQuantity { get; }

        /// <summary>
        /// Event raised when a product is added to the order
        /// </summary>
        event EventHandler<ProductAddedEventArgs> ProductAdded;

        /// <summary>
        /// Event raised when selection changes
        /// </summary>
        event EventHandler SelectionChanged;

        /// <summary>
        /// Displays an error message
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Raises the ProductAdded event (called by Presenter)
        /// </summary>
        void RaiseProductAdded(Product product, int quantity);
    }

    /// <summary>
    /// Event args for ProductAdded event
    /// </summary>
    public class ProductAddedEventArgs : EventArgs
    {
        public Product Product { get; set; }
        public int Quantity { get; set; }

        public ProductAddedEventArgs(Product product, int quantity)
        {
            Product = product;
            Quantity = quantity;
        }
    }
}
