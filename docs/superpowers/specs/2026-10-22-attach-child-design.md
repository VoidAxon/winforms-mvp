# Child presenters owned by the parent presenter (`AttachChild`) — design

Date: 2026-10-22 · Status: approved by the owner · Source: real-screen validation V-20 · Reviewed with codex (two rounds)

## Why

A window opened by `WindowNavigator` is created through the view mapping (public parameterless constructor).
When part of that window is a UserControl with its own presenter, and that presenter needs business services
(repository, clock, …), today's documented recipe — "the parent Form creates the child presenter and calls
`Connect(control, param)`" — breaks down: the Form has no access to services. The validation sample worked around
it by re-registering the view mapping with a factory that hands the Form a `Func<ChildPresenter>`:

- dependencies wired by hand at startup, a second (overriding) registration;
- composition (which child, with which input) lives in the Form;
- the parent presenter's tests cannot cover the composition.

Research: in MVP practice, a child component **with external dependencies** is composed by the parent presenter,
which hands the child presenter the child view taken from the parent view (GWT "Wave way", Thomas Broyer);
Caliburn.Micro conductors own child screens; GWTP injects child presenter widgets into the parent. Self-contained
widgets without dependencies keep creating their own presenter (our `Connect`). Fowler's Supervising Controller /
Passive View do not address composition, so "the parent view must not expose child views" (current master-detail
wiki) is a local rule, not an MVP principle.

## Goals

- A parent presenter composes child panels explicitly: it creates the child presenter (dependencies from its own
  constructor or `IPresenterFactory`) and attaches it to a child view taken from the parent view interface.
- Works for **fixed** panels (placed in the designer) and **dynamic** panels (order lines, tabs; added/removed at
  runtime) with the same API.
- The parent presenter's tests cover composition with ordinary fake views through `PresenterTestHost`.
- No WinForms type in Core; presenters' public surface unchanged (the API is `protected`).
- One lifetime model shared with `Connect`.

## Non-goals (deliberately not added)

Framework child collections (`ChildPresenterCollection<TKey>`), slots / regions / conductors, view creation through
the view mapping, attributes or conventions that attach children, automatic model deletion, generic save prompts,
child close vetoes, validation merging, presenter or view pooling/reuse, async disposal. Keys, removal policy and
model synchronization differ per screen (order lines vs. document tabs) and stay in application code.

## API

On `PresenterBase<TView>` (Core):

```csharp
/// Attaches and initializes a child presenter owned by this presenter. Returns an idempotent handle that
/// permanently releases (disposes) the child; ignore it for fixed panels — the parent releases remaining
/// children when it is disposed.
protected IDisposable AttachChild<TChildView>(ControlPresenterBase<TChildView> child, TChildView view)
    where TChildView : IViewBase;

protected IDisposable AttachChild<TChildView, TParam>(
    ControlPresenterBase<TChildView, TParam> child, TChildView view, TParam parameters)
    where TChildView : IViewBase;
```

- Only control presenters: window presenters (close gate, results) cannot be embedded — rejected at compile time.
- `TChildView` / `TParam` are inferred from `child`; a wrong view or parameter type, or the parameterless overload
  for a presenter that needs parameters, does not compile.
- The parent view interface exposes the child **view interface**, never a control:
  `IPatientPanelView PatientPanel { get; }` (fixed) or `IOrderLineView AddLine(); void RemoveLine(IOrderLineView);`
  plus request events (dynamic). Rule 2 holds: interfaces of data and behavior only.

### Usage

```csharp
// Fixed panel — in the parent's OnInitialize (parameters are available; OnViewAttached is too early)
protected override void OnInitialize(OrderEditInput input)
{
    AttachChild(new CustomerPanelPresenter(_customers, _clock), View.CustomerPanel, input);
}

// Dynamic panels — the presenter decides; the view never removes a child view on its own
private readonly Dictionary<IOrderLineView, (OrderLine Line, IDisposable Handle)> _lines = ...;

private void OnAddLineRequested()
{
    var line = _input.AddLine();
    var view = View.AddLine();
    try { _lines[view] = (line, AttachChild(new OrderLinePresenter(_products), view, line)); }
    catch { View.RemoveLine(view); _input.RemoveLine(line); throw; }   // compensate a failed attach
}

private void OnLineRemoveRequested(IOrderLineView view)
{
    var (line, handle) = _lines[view];
    // 1. ask / save / delete what can fail; 2. commit the model change; 3. release and remove
    _input.RemoveLine(line);
    _lines.Remove(view);
    try { handle.Dispose(); }
    finally { View.RemoveLine(view); }
}
```

Keys are the application's choice: the child view (recommended in docs — always distinct), the model object by
reference (new rows have no id), or a stable id. Never an index; never content equality.

## Contracts

| Situation | Behavior |
|---|---|
| `child` or `view` null | `ArgumentNullException`; nothing transferred |
| parent has no attached view | `InvalidOperationException` |
| parent disposing / disposed | `ObjectDisposedException`; nothing transferred |
| child already attached, initialized, owned or connected; child disposed | `InvalidOperationException` / `ObjectDisposedException`; its current owner undisturbed |
| view already has a live framework attachment (two presenters, one view) | `InvalidOperationException` |
| view is a Control already disposed / disposing | `ObjectDisposedException` |
| preflight passes | ownership recorded **before** any child hook runs |
| synchronous child attach / initialize throws | ownership record removed, child disposed, original exception rethrown (cleanup failures reported with it); the view is untouched (caller-owned) |
| child `OnInitializeAsync` faults | existing contract: logged, not an attach failure |
| `handle.Dispose()` first time | unlink, dispose the child synchronously (order below) |
| repeated / reentrant `handle.Dispose()`, or child already released by the parent | no-op |
| `handle.Dispose()` while the parent is disposing | releases the child if still alive, else no-op |
| child disposed directly (`child.Dispose()`) | its ownership record ends too (no retention until parent disposal) |
| released child | cannot be attached again |

Threading: attach and release run on the UI thread; no locks around user callbacks; state is marked before callbacks
and registry traversal tolerates reentrant release.

### Lifecycle

Attach (inside the call): observe view disposal (see safety net) → attach view → child `OnViewAttached` →
parameters → `RegisterViewActions` → bind the child view's `ActionBinder` → child `OnInitialize` → child
`OnInitializeAsync(child Lifetime)` started, not awaited. Children attached in the parent's `OnInitialize` are live
before the window is shown; parent and child async initialization may overlap (attachment order is not an async
barrier). `PresenterTestHost.Initialized` stays parent-only.

Child release (handle, parent teardown, or control disposal): mark released and unlink → child stops accepting
actions and guarded view events → unsubscribe the disposal observer → cancel child `Lifetime` → unbind its binder →
child `Cleanup()` → its own children (reverse order) → child `Disposables` (incl. a DI scope it owns). Every stage
runs even if an earlier one throws; failures are rethrown together at the end. The child view is never disposed.

Parent disposal: parent stops accepting actions; `Lifetime` cancelled → parent binder unbound → parent `Cleanup()`
→ remaining children in reverse attachment order → parent `Disposables`. Children are released before the parent's
own resources (they may still need the parent's DI scope). Descendants reject new dispatches as soon as an owner
starts disposing.

### Services

A child created by `IPresenterFactory` keeps the services (and scope) the factory bound. A child without bound
services inherits the parent's effective provider (borrowed, never disposed by the child), so a parent hosted with
`PresenterTestHost.Start(services, …)` gives its children the same fakes and nothing falls back to
`ServiceLocator.Current`. A child that resolved services in its constructor from another provider is rejected as
today. Note for docs: `IPresenterFactory` (M.E.DI) creates one scope per presenter, so parent and children share only
what is passed (the input model), not scoped services; a shared unit of work must be modelled explicitly.

### Close and validation

Children do not take part in the window's close decision (control presenters have no close gate; recursive vetoes
would mean prompt ordering, duplicate confirmations and partial saves). The parent owns the edit session and checks
the shared input in its `CanClose` / `CanCloseAsync`. Each presenter keeps its own `ValidationState` and display;
the parent validates the whole input (rules that matter for saving live in the model / parent, never only in a panel).
*(Confirmed by the owner, 2026-10-22.)*

## Lifecycle hardening found during review (needed by dynamic panels, fixed for everyone)

1. **Dispatcher stays live after dispose.** `PresenterBase.Dispose` cancels `Lifetime` and runs `Cleanup` but does
   not deactivate the dispatcher: a late click on a removed panel still runs a synchronous handler. Fix: an inactive
   dispatcher reports `CanDispatch == false`, `Dispatch` does nothing and `DispatchAsync` completes without running
   handlers — checked before middleware, payload validation and strict-mode errors; no `CanExecuteChanged`
   refreshes after deactivation.
2. **Binders cannot be unbound through the interface.** Add `void Unbind()` to `IViewActionBinder` (breaking; no
   compatibility needed). `ViewActionBinder.Unbind` also clears its dispatcher reference; `CapturingActionBinder`
   implements it (a late `Dispatch` after unbind is inert). The framework unbinds a presenter's binder when the
   presenter is disposed.
3. **`Connect` observes control disposal only after initialization.** Disposal during initialization is missed. Fix
   with the shared observer below, subscribed before any hook runs.

## Safety net: control disposed while attached

If a child view is a `Control` that is disposed while its presenter is attached (the Form removed it on its own, or
tears it down), the child presenter is released through the same idempotent path — no confirmation, no model change,
no `RemoveLine`; a debug-level log when the parent is still active. Only `Control.Disposed` counts (not
`ParentChanged`, visibility, handle recreation).

Implementation: an internal Core abstraction, e.g.

```csharp
internal interface IViewDisposalObserver
{
    IDisposable ObserveDisposal(IViewBase view, Action onDisposed);   // empty subscription for non-Control views
}
```

The WinForms implementation lives in `WinformsMVP` and is discovered through the same cached reflection lookup that
`ServiceLocator` already uses for `WinFormsPlatformServices` — not as a service registration, so a custom provider or
M.E.DI container cannot silently lose it. Core without the WinForms assembly gets the empty observer (fake views in
tests).

## Relationship with `Connect`

`Connect` stays for presenters hosted without a parent presenter: legacy Forms, independently hosted reusable
controls, controls without dependencies. Both entry points share one internal attachment mechanism (single-attach
checks, failure cleanup, binder/dispatcher teardown, disposal observation, idempotent release); they differ only in
what ends the attachment:

| Entry point | Ends when |
|---|---|
| `AttachChild` | handle disposed, parent disposed, or child control disposed |
| `Connect` | presenter disposed, or control disposed |

One owner per presenter: never `Connect` a presenter that is attached as a child (rejected), and vice versa.

## Testing

Parent tests use ordinary fakes; `PresenterTestHost` starts only the parent and the parent's real `AttachChild`
initializes the children:

```csharp
var fakes = TestServices.CreateFakes();
var view = new FakeOrderView();                 // AddLine() returns a new FakeOrderLineView and records it
using (PresenterTestHost.Start(fakes.Provider, new OrderPresenter(products), view, input))
{
    view.RaiseAddLineRequested();
    var line = Assert.Single(view.Lines);
    line.Binder.Dispatch(OrderLineActions.ChangeQuantity, 3);    // runs the real child presenter
    Assert.Equal(3, input.Lines.Single().Quantity);

    view.RaiseLineRemoveRequested(line);
    Assert.Empty(view.Lines);
    line.Binder.Dispatch(OrderLineActions.ChangeQuantity, 9);    // removed: inert
    Assert.Empty(input.Lines);
}
```

Framework tests: fixed and dynamic attach; parameter identity; services inherited (no ambient fallback) and
factory-bound services preserved; preflight rejections leave ownership untouched; second child failing disposes the
first via the parent; reverse-order teardown after `Cleanup`, before `Disposables`; handle idempotency, reentrancy,
cleanup exceptions; late dispatch inert after release (sync and async); dispatcher/binder hardening for ordinary
presenters; `Connect` disposal during initialization; control `Disposed` releases a child (WinForms integration test,
CI). Core-only tests run on the local net10 harness; the rest on CI (net48).

## Documentation

- `wiki/HowTo-Implement-Master-Detail.md`: replace "the parent Form owns both / the parent view exposes no child
  views" with the ownership rule: **an MVP parent presenter that owns an embedded presenter uses `AttachChild` and
  its view exposes the child view interface; a WinForms host (legacy Form, standalone control) uses `Connect`.**
  Add the dynamic-panel recipe (keys, removal order, compensation) and the factory-registration workaround as a
  migration note.
- `wiki/HowTo-Communicate-Between-Presenters.md` (shared input model, PR #33): point to `AttachChild`.
- `wiki/Reference-Presenter-Base-Classes.md`, `wiki/HowTo-Test-A-Presenter.md`, `wiki/Reference-ViewAction-System.md`
  (dispatcher inactive after dispose, `Unbind`), CLAUDE.md, FEATURES.md, CHANGELOG (Added `AttachChild`; BREAKING
  `IViewActionBinder.Unbind`; Fixed late dispatch after dispose, `Connect` disposal during initialization).
- Templates: `mvp-control` unchanged (still shows `Connect`); no template change required.

## Validation (local sample, not pushed)

Replace the factory registration in the sample's edit screen with `AttachChild` (both panels), drop the overriding
view-mapping registration, and add a parent test that drives a panel through the parent host. Optionally convert one
list of entries into dynamic panels to exercise the handle.
