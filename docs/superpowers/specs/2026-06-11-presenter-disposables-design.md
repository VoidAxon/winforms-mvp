# Design: presenter subscription lifecycle — `CompositeDisposable` + `Disposables` bag

Date: 2026-06-11
Status: Approved (corrections to the original draft confirmed by the user)
Branch: `feature/presenter-disposables`

## Problem

Presenters accumulate things that must be released when the presenter dies: `IEventAggregator.Subscribe(...)` returns an `IDisposable` (the docs explicitly say "Dispose subscriptions in Cleanup()"), `Cascade.Bind(...)` returns an `IDisposable` unsubscriber, and `+=` view-event subscriptions need a matching `-=`. Today every presenter hand-rolls this: a `List<IDisposable>` or per-field `if (_bind != null) _bind.Dispose();` in `Cleanup()`, plus manual event unhooks. Our own `samples/.../CascadeDemo/CascadePresenters.cs` is the evidence — three presenters each carrying exactly this boilerplate (including a "guard against double-attach" comment).

The failure modes of the manual pattern: lifetime management lives far from the creation site; it is easy to forget to add or to dispose; a missed unhook is a leak or a dangling handler.

## Solution

A zero-dependency, net40-safe trio in `WinformsMVP.Common`, plus a lazily-created bag on `PresenterBase` that the framework disposes automatically:

```csharp
// WinformsMVP.Common
public sealed class CompositeDisposable : IDisposable
{
    public void Add(IDisposable d);   // null -> ignored; after Dispose -> disposes d immediately
    public void Dispose();            // reverse order; idempotent
}

public static class Disposable
{
    public static IDisposable Create(Action onDispose);   // idempotent wrapper for += / -= pairs
}

public static class DisposableExtensions
{
    // Registers and returns the same instance, so lifetime is declared on the creation line.
    public static T DisposeWith<T>(this T disposable, CompositeDisposable bag) where T : IDisposable;
}
```

```csharp
// PresenterBase<TView> — lazily created; a presenter with no subscriptions allocates nothing.
protected CompositeDisposable Disposables { get; }   // lazy backing field

public void Dispose()
{
    if (!_isDisposed)
    {
        Cleanup();                 // user hook first (presenter state still intact)
        _disposables?.Dispose();   // then the framework sweeps the bag
        _isDisposed = true;
    }
    GC.SuppressFinalize(this);
}
```

Usage — lifetime declared at the creation line, no `Cleanup` override needed:

```csharp
protected override void OnViewAttached()
{
    Cascade.Bind(_category, _sub, ...).DisposeWith(Disposables);
    Events.Subscribe<OrderShipped>(OnShipped).DisposeWith(Disposables);

    EventHandler h = (s, e) => _category.Select(View.SelectedCategory);
    View.CategorySelected += h;
    Disposable.Create(() => View.CategorySelected -= h).DisposeWith(Disposables);
}
```

## Decisions (corrections to the original draft)

1. **No detach/re-attach model.** This framework has no `OnViewDetached`; a presenter and its view share one lifetime and `Cleanup()` runs exactly once from `Dispose()`. The draft's "release and rebuild the bag per attach cycle" (imported from ReactiveUI's activate/deactivate) describes behavior that cannot occur here and is dropped: **one bag, disposed at `Dispose()`, never rebuilt.** `Add` after disposal still disposes the item immediately (defensive).
2. **The release point is `PresenterBase.Dispose()`, not `Cleanup()`.** `Cleanup` is an empty virtual with no base-call contract — relying on overrides to call base would be fragile. Order: `Cleanup()` first (user teardown with state intact), then the bag.
3. **Lazy initialization.** `Disposables` creates the bag on first access; presenters without subscriptions pay nothing and need no ceremony.
4. **Names collide with Rx/ReactiveUI on purpose.** `CompositeDisposable` and `DisposeWith` are the established idiom; familiarity outweighs the (unlikely, net40-WinForms) risk of co-importing `System.Reactive`. Type names can be aliased; documented as a note, not avoided.
5. **Not thread-safe by design.** Subscriptions are created and released on the UI thread; no locking. Documented.
6. **Reverse-order disposal, idempotent everywhere.** `CompositeDisposable.Dispose` releases in reverse insertion order and only once; `Disposable.Create` invokes its action at most once.

## Scope

- **Core:** `Common/CompositeDisposable.cs`, `Common/Disposable.cs`, `Common/DisposableExtensions.cs`; `PresenterBase` integration (lazy `Disposables`, dispose-after-Cleanup).
- **Dogfooding:** refactor the three `CascadeDemo` presenters to `.DisposeWith(Disposables)` (removes the `_bind` fields, null-guards, and manual `-=` from `Cleanup`), keeping behavior identical — existing tests must stay green.
- **Docs:** new wiki page (in **Japanese**, per the user's standing instruction for wiki content), adapted from the user's draft with the corrections above; the cascade cross-link is `HowTo-Handle-Cascading-Selection` (the draft's link name was wrong). One line in CLAUDE.md's presenter section.
- **Out of scope (YAGNI, per the draft's own boundaries):** a second "lifetime bag", fluent cascade builders, thread-safe variants, and any analyzer enforcement.

## Testability

All pure BCL — directly unit-testable: reverse order, idempotence, add-after-dispose, null-add, `DisposeWith` returns the same instance, `Disposable.Create` runs once. `PresenterBase` behavior via a test presenter subclass: bag disposed on `Dispose`, **after** `Cleanup` (order assertion), lazy (no allocation when untouched — observable via not throwing / a subclass probe).
