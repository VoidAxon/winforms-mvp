# Service-Based Pattern - Quick Start Guide

This guide will help you understand and implement the Service-Based pattern in **5 minutes**.

## Core Concept

Instead of presenters talking to each other:

```
ProductSelector → (event) → Parent → (method call) → OrderSummary
```

All presenters talk to a **shared service**:

```
ProductSelector → Service → (events) → OrderSummary
                    ↑                → OrderManagement
                    └─ OrderManagement
```

## Step-by-Step Implementation

### Step 1: Define the Service Interface

**File: `ComplexInteractionDemo/Services/IOrderManagementService.cs`**

```csharp
public interface IOrderManagementService
{
    // State (read-only)
    IReadOnlyList<OrderItem> OrderItems { get; }

    // Operations
    void AddProduct(Product product, int quantity);
    void RemoveItem(OrderItem item);
    void ClearOrder();
    decimal GetTotal();

    // Events (state change notifications)
    event EventHandler<ProductAddedEventArgs> ProductAdded;
    event EventHandler<ItemRemovedEventArgs> ItemRemoved;
    event EventHandler<TotalChangedEventArgs> TotalChanged;
    event EventHandler OrderCleared;
}
```

**Key Points:**
- ✅ Read-only state properties (`IReadOnlyList<OrderItem>`)
- ✅ Methods for operations (`AddProduct`, `RemoveItem`, etc.)
- ✅ Events for state changes (`ProductAdded`, `ItemRemoved`, etc.)

### Step 2: Implement the Service

**File: `ComplexInteractionDemo/Services/OrderManagementService.cs`**

```csharp
public class OrderManagementService : IOrderManagementService
{
    private readonly List<OrderItem> _orderItems = new List<OrderItem>();
    private readonly object _lock = new object();

    public IReadOnlyList<OrderItem> OrderItems
    {
        get
        {
            lock (_lock)
            {
                return _orderItems.ToList().AsReadOnly();
            }
        }
    }

    public event EventHandler<ProductAddedEventArgs> ProductAdded;
    public event EventHandler<ItemRemovedEventArgs> ItemRemoved;
    public event EventHandler<TotalChangedEventArgs> TotalChanged;
    public event EventHandler OrderCleared;

    public void AddProduct(Product product, int quantity)
    {
        decimal oldTotal;
        decimal newTotal;

        lock (_lock)
        {
            oldTotal = CalculateTotal();

            // Add or update product
            var existingItem = _orderItems.FirstOrDefault(i => i.Product.Id == product.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                _orderItems.Add(new OrderItem { Product = product, Quantity = quantity });
            }

            newTotal = CalculateTotal();
        }

        // Raise events outside of lock
        ProductAdded?.Invoke(this, new ProductAddedEventArgs(product, quantity));
        RaiseTotalChanged(oldTotal, newTotal);
    }

    // Similar implementations for RemoveItem, ClearOrder, GetTotal...
}
```

**Key Points:**
- ✅ Thread-safe (lock-based synchronization)
- ✅ Raise events **after** state changes
- ✅ Raise events **outside** of locks (prevent deadlocks)

### Step 3: Update Presenters to Use the Service

#### ProductSelector: Call Service Methods

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

        // Call service method - service handles the rest
        _orderService.AddProduct(selectedProduct, quantity);
    }
}
```

**Key Points:**
- ✅ Inject `IOrderManagementService` in constructor
- ✅ Call service methods when user performs actions
- ✅ NO event raising needed

#### OrderSummary: Subscribe to Service Events

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
        // Call service method
        _orderService.RemoveItem(selectedItem);
    }

    protected override void OnDispose()
    {
        // Unsubscribe from service events
        _orderService.ProductAdded -= OnServiceProductAdded;
        _orderService.ItemRemoved -= OnServiceItemRemoved;
        _orderService.TotalChanged -= OnServiceTotalChanged;

        base.OnDispose();
    }
}
```

**Key Points:**
- ✅ Subscribe to service events in `OnInitialize()`
- ✅ Update View when service events fire
- ✅ Get state from service (single source of truth)
- ✅ Unsubscribe in `OnDispose()`

#### OrderManagement: NO Child Presenter References

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
        // Subscribe to service events
        _orderService.ProductAdded += OnServiceProductAdded;
        _orderService.ItemRemoved += OnServiceItemRemoved;

        View.StatusMessage = "Ready. Select products and add to order.";
    }

    private void OnServiceProductAdded(object sender, ProductAddedEventArgs e)
    {
        View.StatusMessage = $"Added {e.Quantity}x {e.Product.Name} to order.";
    }

    private void OnClearOrderClicked(object sender, EventArgs e)
    {
        _orderService.ClearOrder();
    }

    private void OnSaveOrderClicked(object sender, EventArgs e)
    {
        var orderItems = _orderService.OrderItems;
        var total = _orderService.GetTotal();

        // Save logic...
    }
}
```

**Key Points:**
- ✅ NO child presenter references
- ✅ Subscribe to service events for status updates
- ✅ Call service methods for operations

### Step 4: Dependency Injection Setup

**File: `Program.cs`**

```csharp
public static void Run()
{
    // 1. Create shared service instance
    var orderService = new OrderManagementService();

    var form = new OrderManagementForm();

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

    mainPresenter.AttachView(form);
    mainPresenter.Initialize();
    form.ShowDialog();
}
```

**Key Points:**
- ✅ Create **one** service instance
- ✅ Inject **same** instance into all presenters
- ✅ Presenters don't know about each other

## Common Patterns

### Pattern 1: Service Method → Event Chain

```
User clicks "Add to Order"
    ↓
ProductSelectorPresenter calls service.AddProduct()
    ↓
Service updates state (adds item to _orderItems)
    ↓
Service raises ProductAdded event
    ↓
OrderSummaryPresenter receives event
    ↓
OrderSummaryPresenter calls RefreshView()
    ↓
OrderSummaryPresenter gets state from service.OrderItems
    ↓
View updates
```

### Pattern 2: State Query

```csharp
// Always get state from service (single source of truth)
var orderItems = _orderService.OrderItems;
var total = _orderService.GetTotal();

// Never store state in presenter!
```

### Pattern 3: Event Subscription/Unsubscription

```csharp
protected override void OnInitialize()
{
    // Subscribe to service events
    _orderService.ProductAdded += OnServiceProductAdded;
    _orderService.TotalChanged += OnServiceTotalChanged;
}

protected override void OnDispose()
{
    // Unsubscribe to prevent memory leaks
    _orderService.ProductAdded -= OnServiceProductAdded;
    _orderService.TotalChanged -= OnServiceTotalChanged;

    base.OnDispose();
}
```

## Testing

### Testing Presenters with Mock Service

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
```

### Testing Service Logic

```csharp
[Fact]
public void OrderManagementService_AddProduct_RaisesProductAddedEvent()
{
    // Arrange
    var service = new OrderManagementService();
    var eventRaised = false;
    Product addedProduct = null;

    service.ProductAdded += (s, e) =>
    {
        eventRaised = true;
        addedProduct = e.Product;
    };

    var product = new Product { Name = "Mouse", Price = 29.99m };

    // Act
    service.AddProduct(product, 1);

    // Assert
    Assert.True(eventRaised);
    Assert.Equal("Mouse", addedProduct.Name);
    Assert.Equal(1, service.OrderItems.Count);
}
```

## Checklist

When implementing Service-Based pattern:

- [ ] Service interface defines state (read-only), methods, and events
- [ ] Service implementation is thread-safe (uses locks)
- [ ] Service raises events **after** state changes
- [ ] Service raises events **outside** of locks
- [ ] Presenters inject `IOrderManagementService` in constructor
- [ ] Presenters subscribe to service events in `OnInitialize()`
- [ ] Presenters unsubscribe from service events in `OnDispose()`
- [ ] Presenters call service methods for operations
- [ ] Presenters get state from service (never store locally)
- [ ] Program.cs creates **one** service instance
- [ ] Program.cs injects **same** instance into all presenters
- [ ] NO presenter-to-presenter references

## Summary

**Service-Based Pattern in 3 Rules:**

1. **Service owns the state** - All presenters query service for state
2. **Service raises events** - All presenters subscribe to service events
3. **Presenters don't know each other** - All communication via service

**Benefits:**
- ✅ Maximum decoupling
- ✅ Single source of truth
- ✅ Easy to test
- ✅ Scalable (easy to add new presenters)

**When to use:**
- Complex shared business logic
- 3+ presenters that need to interact
- Reusable business logic across forms
