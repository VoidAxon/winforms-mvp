using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.OrderManagement;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.OrderSummary;
using WinformsMVP.Samples.ComplexInteractionDemo_EventBased.ProductSelector;

namespace WinformsMVP.Samples.ComplexInteractionDemo_EventBased
{
    /// <summary>
    /// Entry point for Event Aggregator-based Complex Interaction Demo.
    /// Demonstrates decoupled pub-sub messaging where presenters communicate
    /// through events without knowing about each other.
    /// </summary>
    public static class ComplexInteractionDemoEventBasedProgram
    {
        public static void Run()
        {
            // Sample product catalog
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Laptop", Price = 999.99m },
                new Product { Id = 2, Name = "Mouse", Price = 29.99m },
                new Product { Id = 3, Name = "Keyboard", Price = 79.99m },
                new Product { Id = 4, Name = "Monitor", Price = 299.99m },
                new Product { Id = 5, Name = "USB Cable", Price = 9.99m },
                new Product { Id = 6, Name = "Headphones", Price = 149.99m }
            };

            // Create shared Event Aggregator - THIS IS THE KEY!
            // All presenters use the SAME instance to communicate
            var eventAggregator = new EventAggregator();

            // Create the main form (reuses existing OrderManagementForm)
            var form = new OrderManagementForm();

            // Create all presenters with the shared event aggregator
            // Each presenter maintains its own state but communicates via messages

            // ProductSelector: Publishes ProductAddedMessage
            var productSelectorPresenter = new ProductSelector.ProductSelectorPresenter(
                form.ProductSelectorView,
                eventAggregator);

            // OrderSummary: Subscribes to ProductAddedMessage, ClearOrderMessage
            //               Publishes TotalChangedMessage, OrderItemRemovedMessage, OrderClearedMessage
            var orderSummaryPresenter = new OrderSummary.OrderSummaryPresenter(
                form.OrderSummaryView,
                eventAggregator);

            // OrderManagement: Subscribes to all messages for status updates
            //                  Publishes ClearOrderMessage, GetOrderSnapshotRequest
            var mainPresenter = new OrderManagement.OrderManagementPresenter(
                eventAggregator,
                products);

            // Initialize the main presenter
            mainPresenter.AttachView(form);
            mainPresenter.Initialize();

            // Set available products in ProductSelector
            productSelectorPresenter.SetAvailableProducts(mainPresenter.GetAvailableProducts());

            // Show the form
            form.ShowDialog();

            // Cleanup
            mainPresenter.Dispose();
            productSelectorPresenter.Dispose();
            orderSummaryPresenter.Dispose();
        }
    }
}
