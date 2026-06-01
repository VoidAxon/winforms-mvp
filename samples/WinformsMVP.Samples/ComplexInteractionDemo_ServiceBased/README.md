# ComplexInteractionDemo - Service-Based Pattern

This example demonstrates the **Service-Based Pattern** for managing complex interactions between multiple presenters using a shared service layer.

## Architecture Overview

### Pattern Diagram

```
┌────────────────────────────────────────────────────────────────────────────┐
│                          Service-Based Pattern                              │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────┐                                               │
│  │ ProductSelectorPresenter │                                              │
│  └───────────┬──────────────┘                                              │
│              │ Calls:                                                       │
│              │ - AddProduct(product, qty)                                   │
│              │                                                              │
│              ▼                                                              │
│  ┌─────────────────────────────────┐                                       │
│  │   IOrderManagementService        │  ← Single Source of Truth            │
│  │   (OrderManagementService)       │                                      │
│  │                                  │                                       │
│  │  State:                          │                                      │
│  │  - OrderItems: List<OrderItem>   │                                      │
│  │                                  │                                       │
│  │  Methods:                        │                                      │
│  │  - AddProduct(product, qty)      │                                      │
│  │  - RemoveItem(item)              │                                      │
│  │  - ClearOrder()                  │                                      │
│  │  - GetTotal()                    │                                      │
│  │                                  │                                       │
│  │  Events:                         │                                      │
│  │  - ProductAdded                  │                                      │
│  │  - ItemRemoved                   │                                      │
│  │  - OrderCleared                  │                                      │
│  │  - TotalChanged                  │                                      │
│  └─────────────────────────────────┘                                       │
│              ▲                  ▲                                           │
│              │                  │                                           │
│              │ Subscribes       │ Subscribes                                │
│              │ to events        │ to events                                 │
│              │                  │                                           │
│  ┌───────────┴──────────┐  ┌───┴──────────────────┐                       │
│  │ OrderSummaryPresenter │  │ OrderManagementPresenter │                   │
│  └──────────────────────┘  └──────────────────────┘                        │
│                                                                             │
│  Calls:                        Calls:                                       │
│  - RemoveItem(item)            - ClearOrder()                               │
│                                - GetTotal()                                 │
│                                                                             │
└────────────────────────────────────────────────────────────────────────────┘
```

## Key Principles

### 1. **Single Source of Truth**

The `IOrderManagementService` is the **only** place that stores order state:
- `OrderItems` collection (List<OrderItem>)
- Total calculation logic

All presenters query the service for current state - they don't maintain their own copies.

### 2. **Service as Mediator**

The service acts as a **mediator** between presenters:
- ProductSelectorPresenter calls `service.AddProduct()`
- Service raises `ProductAdded` event
- OrderSummaryPresenter receives event and updates View
- OrderManagementPresenter receives event and updates status

**Result:** Presenters never talk to each other directly!

### 3. **Event-Driven Updates**

When service state changes, it raises events:
- `ProductAdded` - fired when a product is added
- `ItemRemoved` - fired when an item is removed
- `OrderCleared` - fired when the order is cleared
- `TotalChanged` - fired when the total amount changes

Presenters subscribe to these events and update their Views accordingly.

### 4. **Decoupled Presenters**

Each presenter only knows about:
- Its View interface (IProductSelectorView, IOrderSummaryView, etc.)
- The shared service (IOrderManagementService)

Presenters are **completely unaware** of each other's existence.

## Benefits of Service-Based Pattern

| Benefit | Description |
|---------|-------------|
| **Loose Coupling** | Presenters don't reference each other - only the service |
| **Single Source of Truth** | Service manages state - no synchronization issues |
| **Scalability** | Easy to add new presenters (just subscribe to service events) |
| **Testability** | Mock the service to test presenters in isolation |
| **Maintainability** | Business logic is centralized in the service |
| **Reusability** | Service can be reused across multiple forms/applications |

## Implementation Details

### ProductSelectorPresenter

```csharp
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IOrderManagementService _orderService;

    public ProductSelectorPresenter(
        IProductSelectorView view,
        IOrderManagementService orderService)
        : base(view)
    {
        _orderService = orderService;
    }

    private void OnAddToOrderClicked(object sender, EventArgs e)
    {
        // KEY: Call service method instead of raising event
        _orderService.AddProduct(selectedProduct, quantity);

        // NO need to notify anyone else - service handles it!
    }
}
```

**Responsibilities:**
- Display available products
- Capture user's product selection and quantity
- Call `service.AddProduct()` when user clicks "Add to Order"

**Dependencies:**
- `IProductSelectorView` (its View)
- `IOrderManagementService` (shared service)

**NO dependency on:** OrderSummaryPresenter, OrderManagementPresenter

### OrderSummaryPresenter

```csharp
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IOrderManagementService _orderService;

    public OrderSummaryPresenter(
        IOrderSummaryView view,
        IOrderManagementService orderService)
        : base(view)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        // KEY: Subscribe to service events instead of presenter events
        _orderService.ProductAdded += OnServiceProductAdded;
        _orderService.ItemRemoved += OnServiceItemRemoved;
        _orderService.TotalChanged += OnServiceTotalChanged;

        RefreshView();
    }

    private void OnServiceProductAdded(object sender, ProductAddedEventArgs e)
    {
        // Service added a product - refresh the View
        RefreshView();
    }

    private void RefreshView()
    {
        // Get current state from service (single source of truth)
        View.OrderItems = _orderService.OrderItems;
        View.Total = _orderService.GetTotal();
    }
}
```

**Responsibilities:**
- Display current order items and total
- Allow user to remove items
- Call `service.RemoveItem()` when user removes an item
- Update View when service events fire

**Dependencies:**
- `IOrderSummaryView` (its View)
- `IOrderManagementService` (shared service)

**NO dependency on:** ProductSelectorPresenter, OrderManagementPresenter

### OrderManagementPresenter

```csharp
public class OrderManagementPresenter : WindowPresenterBase<IOrderManagementView>
{
    private readonly IOrderManagementService _orderService;

    public OrderManagementPresenter(
        IOrderManagementService orderService,
        IEnumerable<Product> products)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        // KEY: Subscribe to service events to update status messages
        _orderService.ProductAdded += OnServiceProductAdded;
        _orderService.ItemRemoved += OnServiceItemRemoved;
        _orderService.OrderCleared += OnServiceOrderCleared;
    }

    private void OnClearOrderClicked(object sender, EventArgs e)
    {
        // KEY: Call service method instead of coordinating child presenters
        _orderService.ClearOrder();
    }

    private void OnSaveOrderClicked(object sender, EventArgs e)
    {
        // Get order items from service (single source of truth)
        var orderItems = _orderService.OrderItems;
        var total = _orderService.GetTotal();

        // Save logic...
    }
}
```

**Responsibilities:**
- Coordinate "Clear Order" and "Save Order" operations
- Display status messages when order state changes
- Host child UserControls (ProductSelector, OrderSummary)

**Dependencies:**
- `IOrderManagementView` (its View)
- `IOrderManagementService` (shared service)

**NO dependency on:** ProductSelectorPresenter, OrderSummaryPresenter

## Dependency Injection Setup

The key to this pattern is **injecting the SAME service instance** into all presenters:

```csharp
public static void Run()
{
    // 1. Create shared service instance
    var orderService = new OrderManagementService();

    // 2. Inject the SAME instance into all presenters
    var productSelectorPresenter = new ProductSelectorPresenter(
        form.ProductSelectorView,
        orderService);  // ← Same instance

    var orderSummaryPresenter = new OrderSummaryPresenter(
        form.OrderSummaryView,
        orderService);  // ← Same instance

    var mainPresenter = new OrderManagementPresenter(
        orderService,   // ← Same instance
        products);

    // 3. Presenters communicate via shared service
    //    - ProductSelector calls service.AddProduct()
    //    - Service raises ProductAdded event
    //    - OrderSummary receives event and updates View
    //    - OrderManagement receives event and updates status
}
```

**Critical:** All presenters must share the **same instance** of the service. If you create multiple instances, they will have separate state and events won't propagate correctly.

## Comparison with Event-Based Pattern

| Aspect | Event-Based Pattern | Service-Based Pattern |
|--------|---------------------|----------------------|
| **Coupling** | Parent knows about child presenters | NO presenter-to-presenter coupling |
| **State Management** | State in child presenters | State in shared service |
| **Event Source** | Events raised by presenters | Events raised by service |
| **Coordination** | Parent subscribes to child events | All presenters subscribe to service events |
| **Testing** | Must create child presenters | Mock the service |
| **Scalability** | Hard to add new children | Easy to add new presenters |
| **Single Source of Truth** | No (each presenter has state) | Yes (service has state) |
| **Reusability** | Presenters tied to parent | Service can be reused anywhere |

## When to Use Service-Based Pattern

✅ **Use Service-Based Pattern when:**

- You have **complex business logic** that needs to be shared across multiple presenters
- You want a **single source of truth** for application state
- You need **scalability** (easy to add new presenters)
- You want **maximum decoupling** between presenters
- You plan to **reuse business logic** across different forms or applications
- You want to **centralize business rules** in one place

❌ **Don't use Service-Based Pattern when:**

- You have **simple parent-child relationships** (Event-Based is simpler)
- State is **local to one presenter** (no need for shared service)
- You have **no complex business logic** (YAGNI - You Aren't Gonna Need It)

## Testing

The Service-Based pattern makes testing **very easy**:

```csharp
[Fact]
public void ProductSelector_AddToOrder_CallsServiceAddProduct()
{
    // Arrange
    var mockService = new MockOrderManagementService();
    var mockView = new MockProductSelectorView
    {
        SelectedProduct = new Product { Name = "Laptop", Price = 999.99m },
        Quantity = 2
    };

    var presenter = new ProductSelectorPresenter(mockView, mockService);
    presenter.AttachView(mockView);
    presenter.Initialize();

    // Act
    mockView.RaiseAddToOrderClicked();

    // Assert
    Assert.Equal(1, mockService.AddProductCallCount);
    Assert.Equal("Laptop", mockService.LastAddedProduct.Name);
    Assert.Equal(2, mockService.LastAddedQuantity);
}

[Fact]
public void OrderSummary_ServiceProductAdded_UpdatesView()
{
    // Arrange
    var service = new OrderManagementService();
    var mockView = new MockOrderSummaryView();

    var presenter = new OrderSummaryPresenter(mockView, service);
    presenter.AttachView(mockView);
    presenter.Initialize();

    // Act
    service.AddProduct(new Product { Name = "Mouse", Price = 29.99m }, 1);

    // Assert
    Assert.Equal(1, mockView.OrderItems.Count);
    Assert.Equal(29.99m, mockView.Total);
}
```

## Related Patterns

- **Event-Based Pattern**: See `ComplexInteractionDemo` for direct presenter-to-presenter communication using events
- **Mediator Pattern**: Service acts as a mediator between presenters
- **Observer Pattern**: Presenters observe service state changes via events

## Summary

The **Service-Based Pattern** provides:

1. ✅ **Maximum decoupling** - Presenters don't know about each other
2. ✅ **Single source of truth** - Service manages all state
3. ✅ **Easy testing** - Mock the service instead of creating complex presenter hierarchies
4. ✅ **Scalability** - Add new presenters by subscribing to service events
5. ✅ **Reusability** - Service can be used across multiple forms/applications

This pattern is ideal for **complex applications** with **shared business logic** across multiple presenters.
