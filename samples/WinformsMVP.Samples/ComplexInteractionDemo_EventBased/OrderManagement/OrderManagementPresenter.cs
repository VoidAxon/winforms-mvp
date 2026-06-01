using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.OrderManagement
{
    /// <summary>
    /// Static action keys for OrderManagement (reused from original demo)
    /// </summary>
    public static class OrderManagementActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("OrderManagement");

        public static readonly ViewAction SaveOrder = Factory.Create("SaveOrder");
        public static readonly ViewAction ClearOrder = Factory.Create("ClearOrder");
    }

    /// <summary>
    /// Main presenter for Order Management using Event Aggregator pattern.
    /// Subscribes to all messages for status updates and coordinates high-level actions.
    /// </summary>
    public class OrderManagementPresenter : WindowPresenterBase<IOrderManagementView>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly List<Product> _availableProducts;
        private IDisposable _productAddedSubscription;
        private IDisposable _orderItemRemovedSubscription;
        private IDisposable _totalChangedSubscription;
        private IDisposable _orderClearedSubscription;

        public OrderManagementPresenter(
            IEventAggregator eventAggregator,
            List<Product> availableProducts)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _availableProducts = availableProducts ?? new List<Product>();
        }

        protected override void OnViewAttached()
        {
            // No view events to subscribe to (using ViewActions instead)
        }

        protected override void OnInitialize()
        {
            // Subscribe to all relevant messages for status updates
            _productAddedSubscription = _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAddedMessage);
            _orderItemRemovedSubscription = _eventAggregator.Subscribe<OrderItemRemovedMessage>(OnOrderItemRemovedMessage);
            _totalChangedSubscription = _eventAggregator.Subscribe<TotalChangedMessage>(OnTotalChangedMessage);
            _orderClearedSubscription = _eventAggregator.Subscribe<OrderClearedMessage>(OnOrderClearedMessage);

            // Initial status
            View.StatusMessage = "Ready. Select products to add to the order.";
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(OrderManagementActions.ClearOrder, OnClearOrder);
            Dispatcher.Register(OrderManagementActions.SaveOrder, OnSaveOrder);
        }

        /// <summary>
        /// Gets the available products for the system.
        /// </summary>
        public List<Product> GetAvailableProducts()
        {
            return _availableProducts;
        }

        private void OnProductAddedMessage(ProductAddedMessage message)
        {
            // Update status when product is added
            View.StatusMessage = $"Added {message.Quantity}x {message.Product.Name} to order.";
        }

        private void OnOrderItemRemovedMessage(OrderItemRemovedMessage message)
        {
            // Update status when item is removed
            View.StatusMessage = $"Removed {message.Item.Product.Name} from order.";
        }

        private void OnTotalChangedMessage(TotalChangedMessage message)
        {
            // Update current total
            View.CurrentTotal = message.NewTotal;

            // Update status when total changes
            if (message.NewTotal > message.OldTotal)
            {
                View.StatusMessage += $" Total increased from {message.OldTotal:C} to {message.NewTotal:C}.";
            }
            else if (message.NewTotal < message.OldTotal)
            {
                View.StatusMessage += $" Total decreased from {message.OldTotal:C} to {message.NewTotal:C}.";
            }
        }

        private void OnOrderClearedMessage(OrderClearedMessage message)
        {
            // Update status when order is cleared
            View.StatusMessage = "Order cleared. Ready for new order.";
        }

        private void OnClearOrder()
        {
            // Confirm before clearing
            if (!Messages.ConfirmYesNo("Are you sure you want to clear the entire order?", "Confirm Clear"))
            {
                return;
            }

            // Publish ClearOrderMessage
            _eventAggregator.Publish(new ClearOrderMessage());
        }

        private void OnSaveOrder()
        {
            // Get current order snapshot via request message
            var request = new GetOrderSnapshotRequest();
            _eventAggregator.Publish(request);

            // Check if order has items
            if (request.OrderItems == null || request.OrderItems.Count == 0)
            {
                Messages.ShowWarning("Cannot save an empty order.", "Empty Order");
                return;
            }

            // Build order summary
            var orderSummary = string.Join("\n",
                request.OrderItems.Select(item =>
                    $"- {item.Product.Name} x {item.Quantity} = {item.Product.Price * item.Quantity:C}"));

            var message = $"Order saved successfully!\n\n" +
                         $"Items:\n{orderSummary}\n\n" +
                         $"Total: {request.Total:C}";

            Messages.ShowInfo(message, "Order Saved");

            View.StatusMessage = $"Order saved with {request.OrderItems.Count} items, total {request.Total:C}.";
        }

        protected override void Cleanup()
        {
            // Dispose subscriptions
            _productAddedSubscription?.Dispose();
            _orderItemRemovedSubscription?.Dispose();
            _totalChangedSubscription?.Dispose();
            _orderClearedSubscription?.Dispose();

            base.Cleanup();
        }
    }
}
