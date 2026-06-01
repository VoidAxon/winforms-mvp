# ComplexInteractionDemo - Service-Based Pattern - File Index

## Overview

This is the **Service-Based implementation** of the Order Management system. All presenters communicate through a shared `IOrderManagementService` instead of directly with each other.

## Directory Structure

```
ComplexInteractionDemo_ServiceBased/
├── ProductSelector/
│   └── ProductSelectorPresenter.cs
├── OrderSummary/
│   └── OrderSummaryPresenter.cs
├── OrderManagement/
│   └── OrderManagementPresenter.cs
├── Program.cs
├── README.md
├── COMPARISON.md
├── QUICKSTART.md
└── INDEX.md (this file)
```

## Files

### Core Implementation

#### 1. **Program.cs**
- Entry point for running the Service-Based demo
- Creates shared `OrderManagementService` instance
- Injects the SAME service into all presenters
- Demonstrates dependency injection pattern

**Key Code:**
```csharp
var orderService = new OrderManagementService();  // Single instance

var productSelectorPresenter = new ProductSelectorPresenter(view, orderService);
var orderSummaryPresenter = new OrderSummaryPresenter(view, orderService);
var mainPresenter = new OrderManagementPresenter(orderService, products);
```

#### 2. **ProductSelector/ProductSelectorPresenter.cs**
- Presenter for product selection UI
- Subscribes to View's `ProductAdded` event
- Calls `service.AddProduct()` when user adds a product
- **NO references to OrderSummaryPresenter or OrderManagementPresenter**

**Key Difference from Event-Based:**
```csharp
// Event-Based: Raise event for parent to handle
ProductSelected?.Invoke(this, new ProductSelectedEventArgs(product, quantity));

// Service-Based: Call service method
_orderService.AddProduct(product, quantity);
```

#### 3. **OrderSummary/OrderSummaryPresenter.cs**
- Presenter for order summary display
- Subscribes to Service events (`ProductAdded`, `ItemRemoved`, `TotalChanged`)
- Updates View when service state changes
- Calls `service.RemoveItem()` when user removes an item
- **NO references to ProductSelectorPresenter or OrderManagementPresenter**

**Key Difference from Event-Based:**
```csharp
// Event-Based: Parent calls AddProductToOrder() method
public void AddProductToOrder(Product product, int quantity) { ... }

// Service-Based: Subscribe to service events
_orderService.ProductAdded += OnServiceProductAdded;
_orderService.ItemRemoved += OnServiceItemRemoved;
```

#### 4. **OrderManagement/OrderManagementPresenter.cs**
- Main presenter for the order management window
- Subscribes to Service events to update status messages
- Calls `service.GetOrderItems()` and `service.GetTotal()` for save operation
- **NO child presenter references**

**Key Difference from Event-Based:**
```csharp
// Event-Based: Holds references to child presenters
private readonly ProductSelectorPresenter _productSelectorPresenter;
private readonly OrderSummaryPresenter _orderSummaryPresenter;

// Service-Based: NO child presenter references, only service
private readonly IOrderManagementService _orderService;
```

### Documentation

#### 5. **README.md**
- Comprehensive guide to the Service-Based pattern
- Architecture diagrams
- Implementation details for each presenter
- Benefits and when to use this pattern
- Comparison with Event-Based pattern

#### 6. **COMPARISON.md**
- Side-by-side comparison of Event-Based vs Service-Based patterns
- Architecture diagrams for both patterns
- Code examples showing differences
- Pros/cons of each pattern
- When to use which pattern

#### 7. **QUICKSTART.md**
- Quick 5-minute guide to implementing Service-Based pattern
- Step-by-step implementation guide
- Common patterns and best practices
- Checklist for implementation
- Testing examples

#### 8. **INDEX.md** (this file)
- File directory and structure
- Quick reference for each file
- Shared dependencies explanation

## Shared Dependencies (Reused from ComplexInteractionDemo)

The Service-Based implementation **reuses** the following from the original `ComplexInteractionDemo`:

### Models (Shared)
- `ComplexInteractionDemo/Models/Product.cs` - Product entity
- `ComplexInteractionDemo/Models/OrderItem.cs` - Order item entity

### Services (Shared)
- `ComplexInteractionDemo/Services/IOrderManagementService.cs` - Service interface
- `ComplexInteractionDemo/Services/OrderManagementService.cs` - Service implementation

### View Interfaces (Shared)
- `ComplexInteractionDemo/ProductSelector/IProductSelectorView.cs`
- `ComplexInteractionDemo/OrderSummary/IOrderSummaryView.cs`
- `ComplexInteractionDemo/OrderManagement/IOrderManagementView.cs`

### View Implementations (Shared)
- `ComplexInteractionDemo/ProductSelector/ProductSelectorView.cs`
- `ComplexInteractionDemo/OrderSummary/OrderSummaryView.cs`
- `ComplexInteractionDemo/OrderManagement/OrderManagementForm.cs`

## How to Run

### From Visual Studio
1. Open `WinformsMVP.Samples.csproj`
2. In `Program.cs` (main entry point), call:
   ```csharp
   ComplexInteractionDemoServiceBasedProgram.Run();
   ```
3. Press F5 to run

### From Command Line
```bash
dotnet run --project samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj
```

Then in the application, navigate to the Service-Based demo.

## Key Architecture Principles

### 1. Single Source of Truth
- All order state lives in `OrderManagementService`
- Presenters NEVER store order state locally
- Presenters always query service for current state

### 2. Event-Driven Updates
- Service raises events when state changes
- Presenters subscribe to service events
- Views update automatically when service state changes

### 3. Presenter Decoupling
- Presenters only know about:
  - Their View interface
  - The shared Service interface
- Presenters DON'T know about:
  - Other presenters
  - How Views are implemented
  - How Service is implemented

### 4. Dependency Injection
- All presenters receive service via constructor
- Same service instance injected into all presenters
- Makes testing easy (inject mock service)

## Testing Strategy

### Unit Testing Presenters
```csharp
[Fact]
public void ProductSelector_AddToOrder_CallsServiceAddProduct()
{
    var mockService = new MockOrderManagementService();
    var presenter = new ProductSelectorPresenter(mockView, mockService);

    // Test calls service method
    Assert.Equal(1, mockService.AddProductCallCount);
}
```

### Unit Testing Service
```csharp
[Fact]
public void OrderManagementService_AddProduct_RaisesProductAddedEvent()
{
    var service = new OrderManagementService();
    var eventRaised = false;
    service.ProductAdded += (s, e) => eventRaised = true;

    service.AddProduct(product, 1);

    Assert.True(eventRaised);
}
```

## Comparison with Event-Based Implementation

| Aspect | Event-Based | Service-Based |
|--------|-------------|---------------|
| **Files** | 4 presenters + Program.cs | 3 presenters + Program.cs + Service |
| **Coupling** | Parent → Children | All → Service |
| **State** | Distributed | Centralized in Service |
| **Event Source** | Presenters | Service |
| **Testability** | Medium | High |
| **Scalability** | Low | High |
| **Complexity** | Low | Medium |

## Related Documentation

- [README.md](README.md) - Full pattern documentation
- [COMPARISON.md](COMPARISON.md) - Detailed comparison with Event-Based pattern
- [QUICKSTART.md](QUICKSTART.md) - Quick implementation guide
- [../ComplexInteractionDemo/README.md](../ComplexInteractionDemo/README.md) - Event-Based pattern documentation

## Summary

The Service-Based pattern provides:
- ✅ Maximum presenter decoupling
- ✅ Single source of truth for state
- ✅ Easy testing (mock service)
- ✅ Scalable architecture (easy to add new presenters)
- ✅ Reusable business logic

This is the **recommended pattern** for complex applications with shared business logic across multiple presenters.
