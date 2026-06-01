# Event-Based vs Service-Based Pattern Comparison

This document provides a **side-by-side comparison** of the Event-Based and Service-Based patterns for managing complex presenter interactions.

## Quick Reference

| Pattern | Coupling | State Location | Event Source | Best For |
|---------|----------|----------------|--------------|----------|
| **Event-Based** | Parent → Child | Child Presenters | Child Presenters | Simple parent-child relationships |
| **Service-Based** | All → Service | Service | Service | Complex shared business logic |

## Architecture Diagrams

### Event-Based Pattern (ComplexInteractionDemo)

```
┌────────────────────────────────────────────────────────┐
│            OrderManagementPresenter (Parent)            │
│                                                         │
│  - Owns child presenter instances                      │
│  - Subscribes to child presenter events                │
│  - Coordinates interactions between children           │
│  - Updates status when children raise events           │
└───────────┬─────────────────────────┬──────────────────┘
            │                         │
            │ Creates & subscribes    │ Creates & subscribes
            │ to ProductSelected      │ to RemoveItemRequested
            ▼                         ▼
┌────────────────────────┐  ┌────────────────────────┐
│ ProductSelectorPresenter │  │ OrderSummaryPresenter   │
│                         │  │                         │
│ - Raises ProductSelected│  │ - Subscribes to parent's│
│   event when user adds  │  │   ProductAdded event    │
│ - Has NO knowledge of   │  │ - Raises RemoveItem     │
│   OrderSummaryPresenter │  │   event when user removes│
└────────────────────────┘  └────────────────────────┘

Event Flow:
User adds product → ProductSelector raises ProductSelected
→ Parent receives event → Parent raises ProductAdded
→ OrderSummary receives event → OrderSummary updates View
```

**Key Characteristics:**
- Parent presenter holds references to child presenters
- Parent subscribes to child events
- Parent acts as coordinator/mediator
- Children don't know about each other
- State distributed across presenters

### Service-Based Pattern (ComplexInteractionDemo_ServiceBased)

```
┌────────────────────────────────────────────────────────┐
│              IOrderManagementService                    │
│              (OrderManagementService)                   │
│                                                         │
│  State:                                                 │
│  - OrderItems: List<OrderItem>  ← Single Source of Truth│
│                                                         │
│  Methods:                                               │
│  - AddProduct(product, qty)                             │
│  - RemoveItem(item)                                     │
│  - ClearOrder()                                         │
│  - GetTotal()                                           │
│                                                         │
│  Events:                                                │
│  - ProductAdded                                         │
│  - ItemRemoved                                          │
│  - OrderCleared                                         │
│  - TotalChanged                                         │
└─────────┬────────────────┬────────────────┬────────────┘
          │                │                │
          │ Injects        │ Injects        │ Injects
          │                │                │
          ▼                ▼                ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│ ProductSelector  │ │ OrderSummary     │ │ OrderManagement  │
│ Presenter        │ │ Presenter        │ │ Presenter        │
│                  │ │                  │ │                  │
│ Calls:           │ │ Calls:           │ │ Calls:           │
│ - AddProduct()   │ │ - RemoveItem()   │ │ - ClearOrder()   │
│                  │ │                  │ │ - GetTotal()     │
│ Subscribes to:   │ │ Subscribes to:   │ │ Subscribes to:   │
│ (none)           │ │ - ProductAdded   │ │ - ProductAdded   │
│                  │ │ - ItemRemoved    │ │ - ItemRemoved    │
│                  │ │ - OrderCleared   │ │ - OrderCleared   │
│                  │ │ - TotalChanged   │ │ - TotalChanged   │
└──────────────────┘ └──────────────────┘ └──────────────────┘

Event Flow:
User adds product → ProductSelector calls service.AddProduct()
→ Service updates state → Service raises ProductAdded event
→ OrderSummary receives event → OrderSummary updates View
→ OrderManagement receives event → OrderManagement updates status
```

**Key Characteristics:**
- All presenters depend on the same service instance
- Service is the single source of truth for state
- Service raises events when state changes
- Presenters don't know about each other
- State centralized in service

## Code Comparison

### ProductSelector: Adding a Product

**Event-Based Pattern:**

```csharp
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    // Raise event for parent to handle
    public event EventHandler<ProductSelectedEventArgs> ProductSelected;

    private void OnAddToOrderClicked(object sender, EventArgs e)
    {
        var selectedProduct = View.SelectedProduct;
        var quantity = View.Quantity;

        // Raise event - parent will handle it
        ProductSelected?.Invoke(this, new ProductSelectedEventArgs(selectedProduct, quantity));
    }
}
```

**Service-Based Pattern:**

```csharp
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IOrderManagementService _orderService;

    public ProductSelectorPresenter(
        IProductSelectorView view,
        IOrderManagementService orderService)  // ← Inject service
        : base(view)
    {
        _orderService = orderService;
    }

    private void OnAddToOrderClicked(object sender, EventArgs e)
    {
        var selectedProduct = View.SelectedProduct;
        var quantity = View.Quantity;

        // Call service method - service will notify subscribers
        _orderService.AddProduct(selectedProduct, quantity);
    }
}
```

**Comparison:**
- Event-Based: Raises event, relies on parent to handle
- Service-Based: Calls service method, service handles notification

### OrderSummary: Updating the View

**Event-Based Pattern:**

```csharp
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    public event EventHandler<RemoveItemRequestedEventArgs> RemoveItemRequested;

    // Parent calls this method to add product
    public void AddProductToOrder(Product product, int quantity)
    {
        var existingItem = _orderItems.FirstOrDefault(i => i.Product.Id == product.Id);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            _orderItems.Add(new OrderItem { Product = product, Quantity = quantity });
        }

        UpdateView();
    }

    private void OnRemoveItemClicked(object sender, EventArgs e)
    {
        // Raise event for parent to handle
        RemoveItemRequested?.Invoke(this, new RemoveItemRequestedEventArgs(item));
    }
}
```

**Service-Based Pattern:**

```csharp
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IOrderManagementService _orderService;

    public OrderSummaryPresenter(
        IOrderSummaryView view,
        IOrderManagementService orderService)  // ← Inject service
        : base(view)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        // Subscribe to service events
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

    private void OnRemoveItemClicked(object sender, EventArgs e)
    {
        // Call service method - service will notify subscribers
        _orderService.RemoveItem(selectedItem);
    }
}
```

**Comparison:**
- Event-Based: Parent calls `AddProductToOrder()` method, state stored locally
- Service-Based: Subscribes to service events, state retrieved from service

### OrderManagement: Coordinating Children

**Event-Based Pattern:**

```csharp
public class OrderManagementPresenter : WindowPresenterBase<IOrderManagementView>
{
    private readonly ProductSelectorPresenter _productSelectorPresenter;
    private readonly OrderSummaryPresenter _orderSummaryPresenter;

    public OrderManagementPresenter(
        ProductSelectorPresenter productSelectorPresenter,
        OrderSummaryPresenter orderSummaryPresenter)
    {
        _productSelectorPresenter = productSelectorPresenter;
        _orderSummaryPresenter = orderSummaryPresenter;
    }

    protected override void OnInitialize()
    {
        // Subscribe to child presenter events
        _productSelectorPresenter.ProductSelected += OnProductSelected;
        _orderSummaryPresenter.RemoveItemRequested += OnRemoveItemRequested;
    }

    private void OnProductSelected(object sender, ProductSelectedEventArgs e)
    {
        // Coordinate: Tell OrderSummary to add the product
        _orderSummaryPresenter.AddProductToOrder(e.Product, e.Quantity);

        View.StatusMessage = $"Added {e.Quantity}x {e.Product.Name} to order.";
    }

    private void OnClearOrderClicked(object sender, EventArgs e)
    {
        // Coordinate: Tell OrderSummary to clear
        _orderSummaryPresenter.ClearOrder();
    }
}
```

**Service-Based Pattern:**

```csharp
public class OrderManagementPresenter : WindowPresenterBase<IOrderManagementView>
{
    private readonly IOrderManagementService _orderService;

    public OrderManagementPresenter(
        IOrderManagementService orderService,  // ← Inject service (no child presenters!)
        IEnumerable<Product> products)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        // Subscribe to service events (not child presenters!)
        _orderService.ProductAdded += OnServiceProductAdded;
        _orderService.ItemRemoved += OnServiceItemRemoved;
        _orderService.OrderCleared += OnServiceOrderCleared;
    }

    private void OnServiceProductAdded(object sender, ProductAddedEventArgs e)
    {
        // Service added a product - update status
        View.StatusMessage = $"Added {e.Quantity}x {e.Product.Name} to order.";
    }

    private void OnClearOrderClicked(object sender, EventArgs e)
    {
        // Call service method - service will notify all subscribers
        _orderService.ClearOrder();
    }

    private void OnSaveOrderClicked(object sender, EventArgs e)
    {
        // Get state from service (single source of truth)
        var orderItems = _orderService.OrderItems;
        var total = _orderService.GetTotal();

        // Save logic...
    }
}
```

**Comparison:**
- Event-Based: Holds child presenter references, coordinates between them
- Service-Based: NO child presenter references, coordinates via service

## Dependency Injection Setup

### Event-Based Pattern (Program.cs)

```csharp
public static void Run()
{
    var form = new OrderManagementForm();

    // Create child presenters first
    var productSelectorPresenter = new ProductSelectorPresenter(
        form.ProductSelectorView);
    productSelectorPresenter.LoadProducts(products);

    var orderSummaryPresenter = new OrderSummaryPresenter(
        form.OrderSummaryView);

    // Create parent presenter with references to children
    var mainPresenter = new OrderManagementPresenter(
        productSelectorPresenter,  // ← Parent knows about children
        orderSummaryPresenter,
        products);

    mainPresenter.AttachView(form);
    mainPresenter.Initialize();
    form.ShowDialog();
}
```

**Characteristics:**
- Create children first
- Pass children to parent constructor
- Parent subscribes to child events in `OnInitialize()`

### Service-Based Pattern (Program.cs)

```csharp
public static void Run()
{
    // Create shared service instance (single source of truth)
    var orderService = new OrderManagementService();

    var form = new OrderManagementForm();

    // All presenters depend on the SAME service instance
    var productSelectorPresenter = new ProductSelectorPresenter(
        form.ProductSelectorView,
        orderService);  // ← Inject shared service

    var orderSummaryPresenter = new OrderSummaryPresenter(
        form.OrderSummaryView,
        orderService);  // ← Inject shared service

    var mainPresenter = new OrderManagementPresenter(
        orderService,   // ← Inject shared service (no child presenters!)
        products);

    mainPresenter.AttachView(form);
    mainPresenter.Initialize();
    form.ShowDialog();
}
```

**Characteristics:**
- Create shared service first
- Inject same service instance into all presenters
- Presenters subscribe to service events in `OnInitialize()`

## Pros and Cons

### Event-Based Pattern

**Pros:**
- ✅ Simple to understand (parent coordinates children)
- ✅ Explicit control flow (easy to follow in debugger)
- ✅ Suitable for simple parent-child relationships
- ✅ Less abstraction (fewer layers)

**Cons:**
- ❌ Parent coupled to child presenters
- ❌ State distributed across presenters
- ❌ Hard to add new children (must update parent)
- ❌ Difficult to reuse business logic
- ❌ Testing requires creating child presenters

### Service-Based Pattern

**Pros:**
- ✅ Maximum decoupling (presenters don't know about each other)
- ✅ Single source of truth (service manages state)
- ✅ Easy to add new presenters (just subscribe to service events)
- ✅ Business logic centralized and reusable
- ✅ Easy to test (mock the service)
- ✅ Scalable (service can grow independently)

**Cons:**
- ❌ More abstraction (service layer)
- ❌ Slightly harder to debug (event flow through service)
- ❌ Overhead for simple scenarios (YAGNI)

## When to Use Which Pattern

### Use Event-Based Pattern When:

- You have **simple parent-child relationships**
- State is **local to one presenter**
- You have **2-3 child presenters at most**
- Business logic is **minimal**
- You value **simplicity over scalability**

**Example Scenarios:**
- Master-detail form (ProductList + ProductDetail)
- Wizard with sequential steps
- Form with validation summary panel

### Use Service-Based Pattern When:

- You have **complex shared business logic**
- You need **single source of truth** for state
- You have **3+ presenters that need to interact**
- You plan to **reuse business logic** across forms
- You want **maximum testability**
- You need **scalability** (easy to add new presenters)

**Example Scenarios:**
- Order management system (this example!)
- Shopping cart with inventory, pricing, discounts
- Multi-view dashboards with shared data
- Collaborative editing with multiple panels

## Migration Path

If you start with Event-Based and need to migrate to Service-Based:

1. **Create the service interface**
   - Define methods for business operations
   - Define events for state changes

2. **Implement the service**
   - Move business logic from presenters to service
   - Raise events when state changes

3. **Update presenters one by one**
   - Inject service instead of child presenters
   - Subscribe to service events
   - Call service methods instead of raising events

4. **Remove parent-child coupling**
   - Remove child presenter references from parent
   - Remove event handlers between presenters

## Summary

| Pattern | Complexity | Coupling | Scalability | Testability | Best For |
|---------|------------|----------|-------------|-------------|----------|
| **Event-Based** | Low | Medium | Low | Medium | Simple parent-child |
| **Service-Based** | Medium | Low | High | High | Complex shared logic |

**Recommendation:**
- Start with **Event-Based** for simple scenarios
- Migrate to **Service-Based** when complexity grows
- Use **Service-Based** from the start if you anticipate complex interactions
