using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;

namespace WinformsMVP.Samples.ComplexInteractionDemo.Services
{
    /// <summary>
    /// Implementation of order management service.
    /// Centralizes all order-related business logic, eliminating the need for
    /// Presenter-to-Presenter communication.
    ///
    /// Thread-safe implementation with lock-based synchronization.
    /// </summary>
    public class OrderManagementService : IOrderManagementService
    {
        private readonly List<OrderItem> _orderItems = new List<OrderItem>();
        private readonly object _lock = new object();

        public IReadOnlyList<OrderItem> OrderItems
        {
            get
            {
                lock (_lock)
                {
                    return _orderItems.ToList().AsReadOnly();
                }
            }
        }

        public event EventHandler<ProductAddedEventArgs> ProductAdded;
        public event EventHandler<ItemRemovedEventArgs> ItemRemoved;
        public event EventHandler<TotalChangedEventArgs> TotalChanged;
        public event EventHandler OrderCleared;

        public void AddProduct(Product product, int quantity)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));

            decimal oldTotal;
            decimal newTotal;

            lock (_lock)
            {
                oldTotal = CalculateTotal();

                // Check if product already exists in the order
                var existingItem = _orderItems.FirstOrDefault(i => i.Product.Id == product.Id);

                if (existingItem != null)
                {
                    // Product already exists - increase quantity
                    existingItem.Quantity += quantity;
                }
                else
                {
                    // New product - add to order
                    _orderItems.Add(new OrderItem
                    {
                        Product = product,
                        Quantity = quantity
                    });
                }

                newTotal = CalculateTotal();
            }

            // Raise events outside of lock to prevent deadlocks
            ProductAdded?.Invoke(this, new ProductAddedEventArgs(product, quantity));
            RaiseTotalChanged(oldTotal, newTotal);
        }

        public void RemoveItem(OrderItem item)
        {
            if (item == null)
                return;

            decimal oldTotal;
            decimal newTotal;
            bool removed;

            lock (_lock)
            {
                oldTotal = CalculateTotal();

                removed = _orderItems.Remove(item);

                newTotal = removed ? CalculateTotal() : oldTotal;
            }

            if (removed)
            {
                // Raise events outside of lock
                ItemRemoved?.Invoke(this, new ItemRemovedEventArgs(item));
                RaiseTotalChanged(oldTotal, newTotal);
            }
        }

        public void ClearOrder()
        {
            decimal oldTotal;
            bool hadItems;

            lock (_lock)
            {
                hadItems = _orderItems.Count > 0;
                oldTotal = CalculateTotal();
                _orderItems.Clear();
            }

            if (hadItems)
            {
                // Raise events outside of lock
                OrderCleared?.Invoke(this, EventArgs.Empty);
                RaiseTotalChanged(oldTotal, 0);
            }
        }

        public decimal GetTotal()
        {
            lock (_lock)
            {
                return CalculateTotal();
            }
        }

        private decimal CalculateTotal()
        {
            // Assumes we're already in lock
            return _orderItems.Sum(i => i.Subtotal);
        }

        private void RaiseTotalChanged(decimal oldTotal, decimal newTotal)
        {
            if (oldTotal != newTotal)
            {
                TotalChanged?.Invoke(this, new TotalChangedEventArgs(oldTotal, newTotal));
            }
        }
    }
}
