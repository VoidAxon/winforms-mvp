using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement;
using WinformsMVP.Samples.ComplexInteractionDemo.Services;

namespace WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.OrderManagement
{
    /// <summary>
    /// Service-Based OrderManagement Presenter.
    ///
    /// Key differences from Event-Based pattern:
    /// - NO child presenter references (ProductSelectorPresenter, OrderSummaryPresenter)
    /// - Depends on IOrderManagementService instead of coordinating between presenters
    /// - Subscribes to service events to update status messages
    /// - Calls service methods for operations (ClearOrder, GetOrderItems)
    /// - Completely decoupled from child presenters
    ///
    /// Architecture:
    /// - Service acts as the mediator between all presenters
    /// - Each presenter depends only on the service, not on each other
    /// - Service manages order state and raises events when state changes
    /// </summary>
    public class OrderManagementPresenter : WindowPresenterBase<IOrderManagementView>
    {
        private readonly IOrderManagementService _orderService;
        private readonly List<Product> _products;

        public OrderManagementPresenter(
            IOrderManagementService orderService,
            IEnumerable<Product> products)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _products = products?.ToList() ?? new List<Product>();
        }

        protected override void OnViewAttached()
        {
            // Subscribe to View events
            View.SaveRequested += OnSaveRequested;
        }

        protected override void OnInitialize()
        {
            // KEY DIFFERENCE: Subscribe to Service events instead of child Presenter events
            // Service notifies us when order state changes
            _orderService.ProductAdded += OnServiceProductAdded;
            _orderService.ItemRemoved += OnServiceItemRemoved;
            _orderService.OrderCleared += OnServiceOrderCleared;
            _orderService.TotalChanged += OnServiceTotalChanged;

            // Set initial status
            View.StatusMessage = "Ready. Select products and add to order.";
            View.CurrentTotal = 0;
        }

        private void OnSaveRequested(object sender, EventArgs e)
        {
            // Get order items from service (single source of truth)
            var orderItems = _orderService.OrderItems;

            if (!orderItems.Any())
            {
                View.ShowErrorMessage("Cannot save an empty order.");
                return;
            }

            // Calculate total from service
            var total = _orderService.GetTotal();

            // In a real application, this would save to a database
            var summary = $"Order saved successfully!\n\n" +
                         $"Total Items: {orderItems.Count}\n" +
                         $"Total Amount: ${total:F2}";

            View.ShowSuccessMessage(summary);
            View.StatusMessage = $"Order saved. Total: ${total:F2}";
        }

        // Service event handlers - Update status when service state changes

        private void OnServiceProductAdded(object sender, ProductAddedEventArgs e)
        {
            // Service added a product - update status
            View.StatusMessage = $"Added {e.Quantity}x {e.Product.Name} to order.";
        }

        private void OnServiceItemRemoved(object sender, ItemRemovedEventArgs e)
        {
            // Service removed an item - update status
            View.StatusMessage = $"Removed {e.Item.Product.Name} from order.";
        }

        private void OnServiceOrderCleared(object sender, EventArgs e)
        {
            // Service cleared the order - update status
            View.StatusMessage = "Order cleared.";
        }

        private void OnServiceTotalChanged(object sender, TotalChangedEventArgs e)
        {
            // Service total changed - update status and total display
            View.CurrentTotal = e.NewTotal;
            View.StatusMessage = $"Order total: ${e.NewTotal:F2}";
        }

        /// <summary>
        /// Gets the available products for the ProductSelector.
        /// This is called by the Program.cs to pass products to the ProductSelector presenter.
        /// </summary>
        public IEnumerable<Product> GetAvailableProducts()
        {
            return _products;
        }

        protected override void Cleanup()
        {
            // Unsubscribe from View events
            View.SaveRequested -= OnSaveRequested;

            // Unsubscribe from Service events
            _orderService.ProductAdded -= OnServiceProductAdded;
            _orderService.ItemRemoved -= OnServiceItemRemoved;
            _orderService.OrderCleared -= OnServiceOrderCleared;
            _orderService.TotalChanged -= OnServiceTotalChanged;

            base.Cleanup();
        }
    }
}
