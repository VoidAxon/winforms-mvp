using System;
using System.Collections.Generic;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.ProductSelector;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.Messages;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased.ProductSelector
{
    /// <summary>
    /// Presenter for product selection using Event Aggregator pattern.
    /// Publishes ProductAddedMessage when a product is added to the order.
    /// </summary>
    public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
    {
        private readonly IEventAggregator _eventAggregator;
        private List<Product> _availableProducts;

        public ProductSelectorPresenter(
            IProductSelectorView view,
            IEventAggregator eventAggregator)
            : base(view)
        {
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        protected override void OnViewAttached()
        {
            // Subscribe to view events
            View.ProductAdded += OnProductAdded;
        }

        protected override void OnInitialize()
        {
            // Initialize with empty product list
            _availableProducts = new List<Product>();
        }

        /// <summary>
        /// Sets the available products for selection.
        /// </summary>
        public void SetAvailableProducts(List<Product> products)
        {
            _availableProducts = products ?? new List<Product>();
            View.Products = _availableProducts;
        }

        private void OnProductAdded(object sender, ProductAddedEventArgs e)
        {
            // Validate selection
            if (e.Product == null)
            {
                View.ShowError("Please select a product.");
                return;
            }

            if (e.Quantity <= 0)
            {
                View.ShowError("Please enter a valid quantity (greater than 0).");
                return;
            }

            // Publish ProductAddedMessage via Event Aggregator
            var message = new ProductAddedMessage(e.Product, e.Quantity);
            _eventAggregator.Publish(message);

            // Reset quantity after adding
            View.Quantity = 1;
        }

        protected override void Cleanup()
        {
            // Unsubscribe from view events
            if (View != null)
            {
                View.ProductAdded -= OnProductAdded;
            }

            base.Cleanup();
        }
    }
}
