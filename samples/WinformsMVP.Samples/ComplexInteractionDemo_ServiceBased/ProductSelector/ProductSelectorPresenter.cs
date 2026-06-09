using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector;
using WinformsMVP.Samples.ComplexInteractionDemo.Services;

namespace WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.ProductSelector
{
    /// <summary>
    /// Service-Based ProductSelector Presenter.
    ///
    /// Key differences from Event-Based pattern:
    /// - Depends on IOrderManagementService instead of raising events to parent
    /// - When View raises ProductAdded event, calls service.AddProduct()
    /// - NO ProductSelected event for parent coordination needed
    /// - Completely decoupled from OrderSummaryPresenter and OrderManagementPresenter
    /// </summary>
    public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
    {
        private readonly IOrderManagementService _orderService;

        public ProductSelectorPresenter(IOrderManagementService orderService)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        }

        /// <summary>
        /// Initializes the presenter with available products.
        /// </summary>
        public void LoadProducts(IList<Product> products)
        {
            View.Products = products ?? new List<Product>();
        }

        protected override void RegisterViewActions()
        {
            // The "Add to Order" button is bound to this action in the View
            // (ProductSelectorView.InitializeActionBindings). Raise the semantic
            // ProductAdded event from the current selection.
            Dispatcher.Register(ProductSelectorActions.AddToOrder, OnAddToOrderRequested);
        }

        private void OnAddToOrderRequested()
        {
            if (View.SelectedProduct == null)
            {
                View.ShowError("Please select a product.");
                return;
            }

            View.RaiseProductAdded(View.SelectedProduct, View.Quantity);
        }

        protected override void OnViewAttached()
        {
            // Subscribe to View's ProductAdded event
            View.ProductAdded += OnViewProductAdded;
            View.SelectionChanged += (s, e) => { }; // No-op for compatibility
        }

        protected override void OnInitialize()
        {
            // Initialize default quantity
            View.Quantity = 1;
        }

        private void OnViewProductAdded(object sender, ComplexInteractionDemo.ProductSelector.ProductAddedEventArgs e)
        {
            // KEY DIFFERENCE: Call service method instead of raising event to parent
            // Service will handle:
            // - Adding the product to the order
            // - Raising ProductAdded event to subscribers (OrderSummary, OrderManagement)
            // - Updating total
            // - Notifying all subscribers
            _orderService.AddProduct(e.Product, e.Quantity);

            // Reset quantity after adding
            View.Quantity = 1;
        }

        protected override void Cleanup()
        {
            // Unsubscribe from View events
            View.ProductAdded -= OnViewProductAdded;

            base.Cleanup();
        }
    }
}
