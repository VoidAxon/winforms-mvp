using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.OrderSummary
{
    /// <summary>
    /// Presenter for order summary using Event Aggregator pattern.
    /// Maintains local order state and publishes TotalChangedMessage when items change.
    /// </summary>
    public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
    {
        private readonly IEventAggregator _eventAggregator;
        private List<OrderItem> _orderItems;
        private IDisposable _productAddedSubscription;
        private IDisposable _clearOrderSubscription;
        private IDisposable _orderSnapshotSubscription;

        public OrderSummaryPresenter(
            IOrderSummaryView view,
            IEventAggregator eventAggregator)
            : base(view)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _orderItems = new List<OrderItem>();
        }

        protected override void OnViewAttached()
        {
            // Subscribe to view events
            View.ItemRemoved += OnItemRemoved;
        }

        protected override void OnInitialize()
        {
            // Subscribe to ProductAddedMessage
            _productAddedSubscription = _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);

            // Subscribe to ClearOrderMessage
            _clearOrderSubscription = _eventAggregator.Subscribe<ClearOrderMessage>(OnClearOrder);

            // Subscribe to GetOrderSnapshotRequest
            _orderSnapshotSubscription = _eventAggregator.Subscribe<GetOrderSnapshotRequest>(OnGetOrderSnapshot);

            // Initialize view with empty order
            UpdateView();
        }

        private void OnProductAdded(ProductAddedMessage message)
        {
            decimal oldTotal = CalculateTotal();

            // Check if product already exists in order
            var existingItem = _orderItems.FirstOrDefault(i => i.Product.Id == message.Product.Id);

            if (existingItem != null)
            {
                // Update existing item quantity
                existingItem.Quantity += message.Quantity;
            }
            else
            {
                // Add new item
                var newItem = new OrderItem
                {
                    Product = message.Product,
                    Quantity = message.Quantity
                };
                _orderItems.Add(newItem);
            }

            // Update view
            UpdateView();

            // Publish TotalChangedMessage
            decimal newTotal = CalculateTotal();
            _eventAggregator.Publish(new TotalChangedMessage(oldTotal, newTotal));
        }

        private void OnClearOrder(ClearOrderMessage message)
        {
            decimal oldTotal = CalculateTotal();

            // Clear local state
            _orderItems.Clear();

            // Update view
            UpdateView();

            // Publish OrderClearedMessage
            _eventAggregator.Publish(new OrderClearedMessage());

            // Publish TotalChangedMessage
            decimal newTotal = 0m;
            _eventAggregator.Publish(new TotalChangedMessage(oldTotal, newTotal));
        }

        private void OnGetOrderSnapshot(GetOrderSnapshotRequest request)
        {
            // Provide snapshot of current order
            request.OrderItems = new List<OrderItem>(_orderItems);
            request.Total = CalculateTotal();
        }

        private void OnItemRemoved(object sender, OrderItemRemovedEventArgs e)
        {
            if (e.Item == null) return;

            decimal oldTotal = CalculateTotal();

            // Remove item from local state
            _orderItems.Remove(e.Item);

            // Update view
            UpdateView();

            // Publish OrderItemRemovedMessage
            _eventAggregator.Publish(new OrderItemRemovedMessage(e.Item));

            // Publish TotalChangedMessage
            decimal newTotal = CalculateTotal();
            _eventAggregator.Publish(new TotalChangedMessage(oldTotal, newTotal));
        }

        private void UpdateView()
        {
            View.OrderItems = _orderItems;
            View.TotalAmount = CalculateTotal();
        }

        private decimal CalculateTotal()
        {
            return _orderItems.Sum(item => item.Product.Price * item.Quantity);
        }

        protected override void Cleanup()
        {
            // Dispose subscriptions
            _productAddedSubscription?.Dispose();
            _clearOrderSubscription?.Dispose();
            _orderSnapshotSubscription?.Dispose();

            // Unsubscribe from view events
            if (View != null)
            {
                View.ItemRemoved -= OnItemRemoved;
            }

            base.Cleanup();
        }
    }
}
