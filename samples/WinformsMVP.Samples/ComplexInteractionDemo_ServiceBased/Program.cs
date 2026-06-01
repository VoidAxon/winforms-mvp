using System.Collections.Generic;
using WinformsMVP.Samples.ComplexInteractionDemo.Models;
using WinformsMVP.Samples.ComplexInteractionDemo.OrderManagement;
using WinformsMVP.Samples.ComplexInteractionDemo.Services;
using ServiceBasedOrderManagementPresenter = WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.OrderManagement.OrderManagementPresenter;
using ServiceBasedOrderSummaryPresenter = WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.OrderSummary.OrderSummaryPresenter;
using ServiceBasedProductSelectorPresenter = WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased.ProductSelector.ProductSelectorPresenter;

namespace WinformsMVP.Samples.ComplexInteractionDemo_ServiceBased
{
    /// <summary>
    /// Entry point for the Service-Based ComplexInteractionDemo.
    ///
    /// Key Architecture:
    /// ┌─────────────────────────────────────────────────────────────────┐
    /// │                   Service-Based Pattern                          │
    /// ├─────────────────────────────────────────────────────────────────┤
    /// │                                                                  │
    /// │  ProductSelectorPresenter ────┐                                 │
    /// │                               │                                  │
    /// │                               ├──► IOrderManagementService       │
    /// │  OrderSummaryPresenter ───────┤         (Shared Service)        │
    /// │                               │                                  │
    /// │  OrderManagementPresenter ────┘                                 │
    /// │                                                                  │
    /// └─────────────────────────────────────────────────────────────────┘
    ///
    /// Benefits:
    /// - All Presenters depend on the SAME service instance
    /// - NO Presenter-to-Presenter coupling
    /// - Service acts as the single source of truth
    /// - Service raises events when state changes
    /// - Easy to add new Presenters (just subscribe to service events)
    /// - Testable (inject mock service)
    ///
    /// How it works:
    /// 1. Create shared OrderManagementService instance
    /// 2. Inject the SAME instance into all presenters
    /// 3. ProductSelector calls service.AddProduct()
    /// 4. Service raises ProductAdded event
    /// 5. OrderSummary receives event and updates View
    /// 6. OrderManagement receives event and updates status
    /// 7. NO direct communication between presenters!
    /// </summary>
    public static class ComplexInteractionDemoServiceBasedProgram
    {
        public static void Run()
        {
            // Sample products
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Laptop", Price = 999.99m },
                new Product { Id = 2, Name = "Mouse", Price = 29.99m },
                new Product { Id = 3, Name = "Keyboard", Price = 79.99m },
                new Product { Id = 4, Name = "Monitor", Price = 299.99m },
                new Product { Id = 5, Name = "Headphones", Price = 149.99m }
            };

            // KEY: Create shared service instance
            // This is the single source of truth for order state
            var orderService = new OrderManagementService();

            // Create the main form (reuse from original ComplexInteractionDemo)
            var form = new OrderManagementForm();

            // KEY DIFFERENCE: All presenters depend on the SAME service instance
            // NO references to each other - Service acts as mediator

            // Create ProductSelector presenter
            var productSelectorPresenter = new ServiceBasedProductSelectorPresenter(
                form.ProductSelectorView,
                orderService);  // ← Inject shared service

            productSelectorPresenter.LoadProducts(products);

            // Create OrderSummary presenter
            var orderSummaryPresenter = new ServiceBasedOrderSummaryPresenter(
                form.OrderSummaryView,
                orderService);  // ← Inject shared service

            // Create OrderManagement presenter
            var mainPresenter = new ServiceBasedOrderManagementPresenter(
                orderService,   // ← Inject shared service
                products);

            mainPresenter.AttachView(form);
            mainPresenter.Initialize();

            // Show the form
            form.ShowDialog();

            // Note: Presenters will be disposed automatically when the form closes
        }
    }
}
