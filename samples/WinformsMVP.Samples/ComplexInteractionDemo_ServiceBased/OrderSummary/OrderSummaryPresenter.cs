using System;
using System.Linq;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderSummary;
using WinformsMVP.Samples.ComplexInteractionDemo.Services;

namespace WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.OrderSummary
{
    /// <summary>
    /// Service-Based OrderSummary Presenter.
    ///
    /// Key differences from Event-Based pattern:
    /// - Subscribes to IOrderManagementService events instead of parent Presenter events
    /// - Updates View when service state changes
    /// - When View raises ItemRemoved event, calls service.RemoveItem()
    /// - NO knowledge of ProductSelectorPresenter or OrderManagementPresenter
    /// - Service acts as the single source of truth for order state
    /// </summary>
    public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
    {
        private readonly IOrderManagementService _orderService;

        public OrderSummaryPresenter(
            IOrderSummaryView view,
            IOrderManagementService orderService)
            : base(view)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        }

        protected override void OnViewAttached()
        {
            // Subscribe to View events
            View.ItemRemoved += OnViewItemRemoved;
            View.SelectionChanged += (s, e) => { }; // No-op for compatibility
        }

        protected override void OnInitialize()
        {
            // KEY DIFFERENCE: Subscribe to Service events instead of Presenter events
            // Service is the single source of truth for order state
            _orderService.ProductAdded += OnServiceProductAdded;
            _orderService.ItemRemoved += OnServiceItemRemoved;
            _orderService.OrderCleared += OnServiceOrderCleared;
            _orderService.TotalChanged += OnServiceTotalChanged;

            // Initialize View with current service state
            RefreshView();
        }

        private void OnViewItemRemoved(object sender, OrderItemRemovedEventArgs e)
        {
            // KEY DIFFERENCE: Call service method instead of raising event to parent
            // Service will handle:
            // - Removing the item from the order
            // - Raising ItemRemoved event
            // - Raising TotalChanged event
            // - Notifying all subscribers
            _orderService.RemoveItem(e.Item);
        }

        // Service event handlers - Update View when service state changes

        private void OnServiceProductAdded(object sender, ProductAddedEventArgs e)
        {
            // Service added a product - refresh the View
            RefreshView();
        }

        private void OnServiceItemRemoved(object sender, ItemRemovedEventArgs e)
        {
            // Service removed an item - refresh the View
            RefreshView();
        }

        private void OnServiceOrderCleared(object sender, EventArgs e)
        {
            // Service cleared the order - refresh the View
            RefreshView();
        }

        private void OnServiceTotalChanged(object sender, WinformsMVP.Samples.ComplexInteractionDemo.Services.TotalChangedEventArgs e)
        {
            // Service total changed - update the total display
            View.TotalAmount = e.NewTotal;
        }

        private void RefreshView()
        {
            // Get current state from service (single source of truth)
            View.OrderItems = _orderService.OrderItems.ToList();
            View.TotalAmount = _orderService.GetTotal();
        }

        protected override void Cleanup()
        {
            // Unsubscribe from View events
            View.ItemRemoved -= OnViewItemRemoved;

            // Unsubscribe from Service events
            _orderService.ProductAdded -= OnServiceProductAdded;
            _orderService.ItemRemoved -= OnServiceItemRemoved;
            _orderService.OrderCleared -= OnServiceOrderCleared;
            _orderService.TotalChanged -= OnServiceTotalChanged;

            base.Cleanup();
        }
    }
}
