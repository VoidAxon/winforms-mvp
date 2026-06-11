# Presenter Disposables Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `CompositeDisposable` / `Disposable.Create` / `DisposeWith` primitives plus a lazily-created `Disposables` bag on `PresenterBase` that the framework disposes automatically (after `Cleanup()`), removing the hand-rolled `List<IDisposable>` / `_bind.Dispose()` boilerplate — dogfooded in `CascadeDemo`.

**Architecture:** Three small types in `WinformsMVP.Common` (pure BCL, net40-safe, single-threaded by design). `PresenterBase.Dispose()` runs `Cleanup()` first, then sweeps the bag — no reliance on overrides calling base. There is NO detach/re-attach model in this framework: one bag, disposed once, never rebuilt; `Add` after disposal disposes the item immediately.

**Tech Stack:** .NET Framework (net40;net48), C#, xUnit 2.9.3. Test baseline on this branch: **445 passed**.

**Spec:** `docs/superpowers/specs/2026-06-11-presenter-disposables-design.md`

---

### Task 1: `CompositeDisposable` + `Disposable.Create` + `DisposeWith` (TDD)

**Files:**
- Create: `src/WinformsMVP/Common/CompositeDisposable.cs`
- Create: `src/WinformsMVP/Common/Disposable.cs`
- Create: `src/WinformsMVP/Common/DisposableExtensions.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Common/CompositeDisposableTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class CompositeDisposableTests
    {
        private sealed class Tracked : IDisposable
        {
            private readonly List<string> _log;
            private readonly string _name;
            public int DisposeCount;
            public Tracked(List<string> log, string name) { _log = log; _name = name; }
            public void Dispose() { DisposeCount++; _log.Add(_name); }
        }

        [Fact]
        public void Dispose_ReleasesMembersInReverseOrder()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            bag.Add(new Tracked(log, "a"));
            bag.Add(new Tracked(log, "b"));
            bag.Add(new Tracked(log, "c"));

            bag.Dispose();

            Assert.Equal(new[] { "c", "b", "a" }, log);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var log = new List<string>();
            var item = new Tracked(log, "x");
            var bag = new CompositeDisposable();
            bag.Add(item);

            bag.Dispose();
            bag.Dispose();

            Assert.Equal(1, item.DisposeCount);
        }

        [Fact]
        public void Add_AfterDispose_DisposesImmediately()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            bag.Dispose();

            var late = new Tracked(log, "late");
            bag.Add(late);

            Assert.Equal(1, late.DisposeCount);
        }

        [Fact]
        public void Add_Null_IsIgnored()
        {
            var bag = new CompositeDisposable();
            bag.Add(null);
            bag.Dispose();   // must not throw
        }

        [Fact]
        public void DisposeWith_RegistersAndReturnsSameInstance()
        {
            var log = new List<string>();
            var bag = new CompositeDisposable();
            var item = new Tracked(log, "x");

            var returned = item.DisposeWith(bag);

            Assert.Same(item, returned);
            bag.Dispose();
            Assert.Equal(1, item.DisposeCount);
        }

        [Fact]
        public void DisposableCreate_RunsActionOnceOnly()
        {
            int calls = 0;
            var d = Disposable.Create(() => calls++);
            d.Dispose();
            d.Dispose();
            Assert.Equal(1, calls);
        }

        [Fact]
        public void DisposableCreate_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Disposable.Create(null));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~CompositeDisposableTests"`
Expected: FAIL (types do not exist — compile error).

- [ ] **Step 3: Implement the three types**

`src/WinformsMVP/Common/CompositeDisposable.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// A bag of <see cref="IDisposable"/>s released together: disposing the bag disposes every
    /// member in reverse insertion order ("last built, first torn down"). Designed for the
    /// presenter lifecycle — see <c>PresenterBase.Disposables</c> — but usable anywhere.
    /// </summary>
    /// <remarks>
    /// Not thread-safe by design: presenter subscriptions are created and released on the UI
    /// thread. Disposal is idempotent; adding to an already-disposed bag disposes the item
    /// immediately instead of silently leaking it. The names <c>CompositeDisposable</c> and
    /// <c>DisposeWith</c> intentionally mirror the Rx / ReactiveUI idiom (without taking a
    /// dependency); alias the type name if you co-import System.Reactive.
    /// </remarks>
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();
        private bool _disposed;

        /// <summary>Adds a disposable to the bag. Null is ignored; if the bag is already
        /// disposed the item is disposed immediately.</summary>
        public void Add(IDisposable disposable)
        {
            if (disposable == null) return;
            if (_disposed) { disposable.Dispose(); return; }
            _items.Add(disposable);
        }

        /// <summary>Disposes all members in reverse insertion order. Idempotent.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                _items[i].Dispose();
            }
            _items.Clear();
        }
    }
}
```

`src/WinformsMVP/Common/Disposable.cs`:

```csharp
using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Factory for ad-hoc disposables. The main use is turning a <c>+=</c> event subscription
    /// into something a <see cref="CompositeDisposable"/> can manage:
    /// <code>
    /// View.SelectionChanged += handler;
    /// Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);
    /// </code>
    /// </summary>
    public static class Disposable
    {
        /// <summary>Creates a disposable that invokes <paramref name="onDispose"/> exactly once.</summary>
        public static IDisposable Create(Action onDispose)
        {
            if (onDispose == null) throw new ArgumentNullException(nameof(onDispose));
            return new AnonymousDisposable(onDispose);
        }

        private sealed class AnonymousDisposable : IDisposable
        {
            private Action _onDispose;
            public AnonymousDisposable(Action onDispose) { _onDispose = onDispose; }

            public void Dispose()
            {
                var action = _onDispose;
                _onDispose = null;   // idempotent: the action runs at most once
                if (action != null) action();
            }
        }
    }
}
```

`src/WinformsMVP/Common/DisposableExtensions.cs`:

```csharp
using System;

namespace WinformsMVP.Common
{
    /// <summary>Fluent registration into a <see cref="CompositeDisposable"/>.</summary>
    public static class DisposableExtensions
    {
        /// <summary>
        /// Registers <paramref name="disposable"/> into <paramref name="bag"/> and returns it,
        /// so the lifetime is declared on the same line that creates the subscription.
        /// </summary>
        public static T DisposeWith<T>(this T disposable, CompositeDisposable bag) where T : IDisposable
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            bag.Add(disposable);
            return disposable;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test ... --filter "FullyQualifiedName~CompositeDisposableTests"` → PASS (7 tests). `dotnet build src/WinformsMVP/WinformsMVP.csproj` → 0 errors (net40+net48).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Common/CompositeDisposable.cs src/WinformsMVP/Common/Disposable.cs src/WinformsMVP/Common/DisposableExtensions.cs tests/WinformsMVP.Samples.Tests/Common/CompositeDisposableTests.cs
git commit -m "feat(common): add CompositeDisposable, Disposable.Create, DisposeWith"
```

---

### Task 2: `PresenterBase.Disposables` (lazy; swept after `Cleanup`)

**Files:**
- Modify: `src/WinformsMVP/MVP/Presenters/PresenterBase.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Presenters/PresenterDisposablesTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class PresenterDisposablesTests
    {
        private sealed class StubView : IViewBase { }

        private sealed class TestPresenter : PresenterBase<IViewBase>
        {
            public readonly List<string> Log = new List<string>();
            public bool BagTouched;

            protected override void OnViewAttached() { }
            protected override void Cleanup() { Log.Add("cleanup"); }

            public void AddToBag(IDisposable d) { BagTouched = true; Disposables.Add(d); }
            public CompositeDisposable Bag => Disposables;
        }

        [Fact]
        public void Dispose_SweepsBag_AfterCleanup()
        {
            var p = new TestPresenter();
            p.AddToBag(Disposable.Create(() => p.Log.Add("bag")));

            p.Dispose();

            Assert.Equal(new[] { "cleanup", "bag" }, p.Log);
        }

        [Fact]
        public void Dispose_Twice_SweepsOnce()
        {
            var p = new TestPresenter();
            int swept = 0;
            p.AddToBag(Disposable.Create(() => swept++));

            p.Dispose();
            p.Dispose();

            Assert.Equal(1, swept);
        }

        [Fact]
        public void Disposables_IsLazy_SameInstanceAcrossAccesses()
        {
            var p = new TestPresenter();
            Assert.Same(p.Bag, p.Bag);
        }

        [Fact]
        public void Dispose_WithUntouchedBag_DoesNotThrow()
        {
            var p = new TestPresenter();
            p.Dispose();   // _disposables stays null; must not throw
            Assert.Equal(new[] { "cleanup" }, p.Log);
        }
    }
}
```

(If `PresenterBase<TView>`'s constructor or generic constraint blocks this stub shape, adapt minimally — the presenter base is `public abstract ... where TView : IViewBase` with a protected parameterless ctor, so a test subclass over `IViewBase` should work. `Dispose` is public via `IPresenter`.)

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test ... --filter "FullyQualifiedName~PresenterDisposablesTests"` → FAIL (`Disposables` does not exist).

- [ ] **Step 3: Implement in `PresenterBase`**

Read `src/WinformsMVP/MVP/Presenters/PresenterBase.cs` first. Add a lazy field + property near the other protected members (the file already has `using WinformsMVP.Common;`):

```csharp
private CompositeDisposable _disposables;

/// <summary>
/// Subscriptions tied to this presenter's lifetime. Register anything that must be released
/// when the presenter is disposed — an <c>IEventAggregator.Subscribe</c> token, a
/// <c>Cascade.Bind</c> unsubscriber, a <c>Disposable.Create(() =&gt; view.Event -= handler)</c>
/// wrapper — via <c>.DisposeWith(Disposables)</c> at the creation line. The framework disposes
/// the bag automatically right after <see cref="Cleanup"/>, so no Cleanup override is needed
/// for these. Created lazily: presenters with no subscriptions allocate nothing.
/// </summary>
protected CompositeDisposable Disposables
{
    get { return _disposables ?? (_disposables = new CompositeDisposable()); }
}
```

And change `Dispose()` from:

```csharp
public void Dispose()
{
    if (!_isDisposed)
    {
        Cleanup();
        _isDisposed = true;
    }
    GC.SuppressFinalize(this);
}
```

to:

```csharp
public void Dispose()
{
    if (!_isDisposed)
    {
        // User teardown first (presenter state still intact), then the framework sweeps the
        // subscription bag. The sweep lives here — not in Cleanup — so an override that
        // forgets to call base cannot leak the bag.
        Cleanup();
        if (_disposables != null) _disposables.Dispose();
        _isDisposed = true;
    }
    GC.SuppressFinalize(this);
}
```

- [ ] **Step 4: Run to verify pass**

Run: the filtered tests → PASS (4 tests). Then the FULL suite → all pass (445 + 7 + 4 = 456).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/MVP/Presenters/PresenterBase.cs tests/WinformsMVP.Samples.Tests/Presenters/PresenterDisposablesTests.cs
git commit -m "feat(presenter): lazy Disposables bag swept automatically after Cleanup"
```

---

### Task 3: Dogfood — refactor `CascadeDemo` presenters

**Files:**
- Modify: `samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs`

- [ ] **Step 1: Read the file**, then for each of the three presenters:
  - Replace each `_bind` field + `if (_bind != null) _bind.Dispose();` (and the "guard against double-attach" dance) with `Cascade.Bind(...).DisposeWith(Disposables);` at the creation site.
  - Replace manual `View.XxxChanged -= handler;` in `Cleanup` with `Disposable.Create(() => View.XxxChanged -= handler).DisposeWith(Disposables);` next to the `+=`.
  - Delete each `Cleanup()` override that becomes empty. Behavior must be identical.

- [ ] **Step 2: Verify**

`dotnet build winforms-mvp.sln -c Debug` → 0 errors. Full test suite → all pass (CascadeDemo presenter tests, if any, unchanged and green).

- [ ] **Step 3: Commit**

```bash
git add samples/WinformsMVP.Samples/CascadeDemo/CascadePresenters.cs
git commit -m "refactor(samples): CascadeDemo presenters use Disposables/DisposeWith instead of manual Cleanup"
```

---

### Task 4: CLAUDE.md note + gate

- [ ] **Step 1:** In `CLAUDE.md`, in the presenter-lifecycle area (the "MVP Principles" rule-4 block or the presenter-hierarchy section — wherever `Cleanup` is mentioned), add one sentence: subscriptions registered via `.DisposeWith(Disposables)` are released automatically after `Cleanup()`; presenters normally need no `Cleanup` override for unsubscription.
- [ ] **Step 2 (gate):** `dotnet build winforms-mvp.sln -c Debug` → 0 errors (only pre-existing xUnit1031 warnings). Full test suite → all pass.
- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: note automatic Disposables sweep in presenter lifecycle"
```

> The wiki page (Japanese, adapted from the user's draft) is written by the controller after this plan lands — NOT part of this plan.

---

## Self-Review

- **Spec coverage:** primitives + behaviors (reverse order, idempotent, add-after-dispose, null-add, once-only action) — Task 1; lazy bag + sweep-after-Cleanup in `Dispose` (not `Cleanup`) — Task 2; no rebuild / no detach model — nothing in any task rebuilds the bag; dogfooding — Task 3; docs line — Task 4. Wiki page explicitly delegated to the controller (Japanese).
- **Placeholder scan:** Task 3 is a read-then-transform with precise rules on a single known file; all other code is complete.
- **Type consistency:** `CompositeDisposable.Add/Dispose`, `Disposable.Create`, `DisposeWith<T>(this T, CompositeDisposable)`, `PresenterBase.Disposables` — identical across tasks.
- **net40 safety:** `List<T>`, `Action`, generic extension methods only.
