# Complex Interaction Demo - Event Aggregator Pattern

This implementation demonstrates the **Event Aggregator pattern** for managing complex interactions between presenters. Presenters communicate through a shared message bus without direct dependencies on each other.

## Architecture Overview

### Pattern: Event Aggregator (Pub-Sub Messaging)

```
┌─────────────────────────────────────────────────────────────┐
│                     Event Aggregator                         │
│              (Shared Message Bus)                            │
└─────────────────────────────────────────────────────────────┘
         ▲                    ▲                    ▲
         │ Publish            │ Subscribe          │ Publish/Subscribe
         │                    │                    │
┌────────┴────────┐  ┌────────┴────────┐  ┌────────┴────────┐
│  ProductSelector │  │  OrderSummary   │  │ OrderManagement │
│   Presenter      │  │   Presenter     │  │   Presenter     │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

### Key Principle

**All presenters depend on `IEventAggregator` - they communicate via messages without knowing about each other.**

- **ProductSelector**: Publishes `ProductAddedMessage`
- **OrderSummary**: Subscribes to `ProductAddedMessage`, `ClearOrderMessage`, `GetOrderSnapshotRequest`
  - Publishes `TotalChangedMessage`, `OrderItemRemovedMessage`, `OrderClearedMessage`
- **OrderManagement**: Subscribes to all messages for status updates
  - Publishes `ClearOrderMessage`, `GetOrderSnapshotRequest`

## Message Flow Diagrams

### Adding a Product

```
User clicks "Add to Order"
         │
         ▼
┌────────────────────────┐
│ ProductSelectorPresenter│
│  Validates input       │
│  Publishes:            │
│  ProductAddedMessage   │
└───────────┬────────────┘
            │
            │ (Event Aggregator routes message)
            ▼
┌────────────────────────┐
│ OrderSummaryPresenter  │
│  Receives message      │
│  Updates local state   │
│  Publishes:            │
│  TotalChangedMessage   │
└───────────┬────────────┘
            │
            │ (Event Aggregator routes message)
            ▼
┌────────────────────────┐
│ OrderManagementPresenter│
│  Receives message      │
│  Updates status text   │
└────────────────────────┘
```

### Clearing the Order

```
User clicks "Clear Order"
         │
         ▼
┌────────────────────────┐
│ OrderManagementPresenter│
│  Shows confirmation    │
│  Publishes:            │
│  ClearOrderMessage     │
└───────────┬────────────┘
            │
            │ (Event Aggregator routes message)
            ▼
┌────────────────────────┐
│ OrderSummaryPresenter  │
│  Receives message      │
│  Clears local state    │
│  Publishes:            │
│  - OrderClearedMessage │
│  - TotalChangedMessage │
└───────────┬────────────┘
            │
            │ (Event Aggregator routes messages)
            ▼
┌────────────────────────┐
│ OrderManagementPresenter│
│  Receives messages     │
│  Updates status text   │
└────────────────────────┘
```

### Saving the Order

```
User clicks "Save Order"
         │
         ▼
┌────────────────────────┐
│ OrderManagementPresenter│
│  Publishes:            │
│  GetOrderSnapshotRequest│
└───────────┬────────────┘
            │
            │ (Event Aggregator routes request)
            ▼
┌────────────────────────┐
│ OrderSummaryPresenter  │
│  Receives request      │
│  Fills response with   │
│  current order data    │
└───────────┬────────────┘
            │
            │ (Response available in request object)
            ▼
┌────────────────────────┐
│ OrderManagementPresenter│
│  Reads response        │
│  Validates order       │
│  Shows save dialog     │
│  Updates status        │
└────────────────────────┘
```

## Message Types

### Command Messages (Request Actions)

| Message | Publisher | Purpose |
|---------|-----------|---------|
| `ClearOrderMessage` | OrderManagement | Request that the order be cleared |
| `GetOrderSnapshotRequest` | OrderManagement | Request current order data for saving |

### Event Messages (Notify State Changes)

| Message | Publisher | Purpose |
|---------|-----------|---------|
| `ProductAddedMessage` | ProductSelector | Product added to order |
| `OrderItemRemovedMessage` | OrderSummary | Item removed from order |
| `TotalChangedMessage` | OrderSummary | Order total changed |
| `OrderClearedMessage` | OrderSummary | Order was cleared |

## State Management

### Each Presenter Maintains Its Own State

Unlike the Service-Based pattern (where a shared service owns the state), in the Event Aggregator pattern:

- **OrderSummaryPresenter** owns the order state (`List<OrderItem>`)
- Other presenters **don't access the state directly**
- State is synchronized through **messages**

**Example:**

```csharp
// OrderSummaryPresenter maintains local order state
private List<OrderItem> _orderItems;

private void OnProductAdded(ProductAddedMessage message)
{
    // Update local state
    _orderItems.Add(new OrderItem
    {
        Product = message.Product,
        Quantity = message.Quantity
    });

    // Notify others about the state change
    _eventAggregator.Publish(new TotalChangedMessage(oldTotal, newTotal));
}
```

## Benefits of Event Aggregator Pattern

### 1. Zero Direct Dependencies

Presenters don't reference each other:

```csharp
// ProductSelectorPresenter doesn't know about OrderSummary
_eventAggregator.Publish(new ProductAddedMessage(product, quantity));

// OrderSummaryPresenter doesn't know about ProductSelector
_eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
```

### 2. Decoupled Communication

Adding a new presenter that listens to messages is easy:

```csharp
// New analytics presenter can subscribe to all messages
_eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
_eventAggregator.Subscribe<OrderClearedMessage>(OnOrderCleared);
// No changes needed to existing presenters!
```

### 3. Testability

Mock the event aggregator to verify message publishing/subscribing:

```csharp
[Fact]
public void OnAddToOrder_PublishesProductAddedMessage()
{
    var mockEventAggregator = new MockEventAggregator();
    var presenter = new ProductSelectorPresenter(view, mockEventAggregator);

    presenter.OnAddToOrder();

    Assert.True(mockEventAggregator.PublishedMessages.Contains<ProductAddedMessage>());
}
```

### 4. Dynamic Runtime Behavior

Subscribers can be added/removed at runtime:

```csharp
// Subscribe when feature is enabled
var subscription = _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);

// Unsubscribe when feature is disabled
subscription.Dispose();
```

## Trade-offs vs Service-Based Pattern

| Aspect | Event Aggregator | Service-Based |
|--------|------------------|---------------|
| **Coupling** | Zero direct coupling | Presenters depend on shared service |
| **State Ownership** | Distributed (each presenter owns state) | Centralized (service owns state) |
| **Message Flow** | Explicit messages | Direct method calls |
| **Debugging** | Harder (trace message flow) | Easier (direct call stack) |
| **Testing** | Mock event aggregator | Mock service interface |
| **Complexity** | Higher (more message types) | Lower (direct API) |
| **Flexibility** | Very high (dynamic subscription) | Moderate (fixed dependencies) |
| **Use Case** | Complex, dynamic interactions | Simple, fixed interactions |

## When to Use Event Aggregator

### Good Fit

- ✅ Many presenters need to react to the same events
- ✅ Presenters have no clear "owner" relationship
- ✅ You need dynamic runtime subscription (plugins, features)
- ✅ You want to avoid circular dependencies
- ✅ You're building a complex dashboard/workspace UI

### Not a Good Fit

- ❌ Simple parent-child presenter relationships (use events instead)
- ❌ Only 2-3 presenters communicating (use shared service)
- ❌ State logic is complex and needs centralized management
- ❌ Debugging message flow is critical

## Subscription Cleanup

**CRITICAL:** Always dispose subscriptions to prevent memory leaks:

```csharp
private IDisposable _productAddedSubscription;

protected override void OnInitialize()
{
    _productAddedSubscription = _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
}

protected override void Cleanup()
{
    _productAddedSubscription?.Dispose();
    base.Cleanup();
}
```

## Request-Response Pattern

For queries, use request messages with mutable response properties:

```csharp
// Define request message
public class GetOrderSnapshotRequest
{
    public List<OrderItem> OrderItems { get; set; }
    public decimal Total { get; set; }
}

// Publisher requests data
var request = new GetOrderSnapshotRequest();
_eventAggregator.Publish(request);

// Subscriber fills response
_eventAggregator.Subscribe<GetOrderSnapshotRequest>(req =>
{
    req.OrderItems = new List<OrderItem>(_orderItems);
    req.Total = CalculateTotal();
});

// Publisher reads response
if (request.OrderItems.Count > 0)
{
    SaveOrder(request.OrderItems, request.Total);
}
```

## Comparison with Other Patterns

### vs. Direct Events

```csharp
// Direct Events (tight coupling)
ProductSelectorPresenter.ProductAdded += OrderSummary.OnProductAdded;

// Event Aggregator (loose coupling)
_eventAggregator.Publish(new ProductAddedMessage(product, quantity));
_eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
```

### vs. Shared Service

```csharp
// Shared Service (centralized state)
_orderService.AddProduct(product, quantity);
var order = _orderService.GetCurrentOrder();

// Event Aggregator (distributed state)
_eventAggregator.Publish(new ProductAddedMessage(product, quantity));
// OrderSummary updates its own state when receiving the message
```

## Running the Demo

```csharp
// From Program.cs or main menu
ComplexInteractionDemoEventBasedProgram.Run();
```

## Further Reading

- [Event Aggregator Pattern (Martin Fowler)](https://martinfowler.com/eaaDev/EventAggregator.html)
- [Prism Event Aggregator](https://prismlibrary.com/docs/event-aggregator.html)
- [MediatR (C# implementation)](https://github.com/jbogard/MediatR)

## Key Takeaways

1. **Event Aggregator = Message Bus** - All presenters communicate through a shared bus
2. **Zero Knowledge** - Presenters don't know about each other
3. **Each Presenter Owns State** - No centralized state service
4. **Messages Drive Behavior** - Commands and events flow through the aggregator
5. **Always Dispose Subscriptions** - Prevent memory leaks
6. **Request-Response for Queries** - Use mutable request messages for data retrieval
7. **Trade Complexity for Flexibility** - More messages, but more extensible

---

**Next Steps:**

- Compare with `ComplexInteractionDemo` (Service-Based pattern)
- Try adding a new presenter that listens to messages (e.g., order analytics)
- Experiment with dynamic subscription/unsubscription
- Benchmark message delivery performance for high-frequency events
