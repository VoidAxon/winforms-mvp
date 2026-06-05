# Window Closing Model Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the event-based window-closing model (`IWindowView.Closing`/`OnClosing` + `IRequestClose<T>.CloseRequested` event + `WindowCloseCoordinator`/`WindowClosingBridge`) with a Prism-style "framework calls the Presenter" model: Pull = a `protected virtual CanClose(...)` override, Push = an injected close sink, all wired once by a single internal `WindowCloseController`.

**Architecture:** The reason a window may/may not close is the Presenter's policy, so the View carries zero closing members. The framework drives both directions: it *calls* the Presenter's `CanClose` (Pull) and *injects* a sink the Presenter calls via `RequestClose` (Push). One internal `WindowCloseController` per window owns the single `FormClosing`/`FormClosed` bridge, the suppress-gate state, and the close-before-show / async-deferral / exception-block logic. Both hosting forms — `WindowNavigator` (Managed) and `presenter.Connect(form)` (Adopted) — funnel through the same controller. Result types stay off the Presenter base classes: `TParam` keeps base type-slot #2, and `TResult` is declared by the marker interface `IRequestClose<TResult>` plus the show-callsite — exactly as today.

**Tech Stack:** C# / .NET Framework (`net40;net48` for core `WinformsMVP`, `net48` elsewhere), SDK-style csproj, xUnit 2.9.3. No new dependencies (net40 zero-dependency constraint: callbacks use `Action<bool>`, never `Task`/`async`).

---

## Design decisions locked in (read before starting)

These resolve the review (A1–A4 / B1–B2 / C1–C2) and refine the review-response. **Where this plan differs from `窗口关闭模型-评审回应.md`, this plan wins** — the difference is noted inline.

1. **`ICloseSink` is non-generic and internal.** The review-response proposed `ICloseSink<in TResult>` plus a settable `CloseSink` property on `IRequestClose<TResult>`. That forces the framework to set a *typed* property from a *non-generic* controller — impossible without reflection/`dynamic`. **Instead:** the sink is non-generic (`void Close(object result, InteractionStatus status)`), the Presenter never references it, and the typed `RequestClose<TResult>` extension routes through an internal `ICloseParticipant.RequestCloseCore(object, ...)`. Result is boxed/upcast to `object` and cast back to `TResult` at the show boundary. Single non-generic controller; call-site type safety preserved; no reflection.

2. **`IRequestClose<TResult>` becomes a marker** (no members). Its only jobs: give the Presenter a compile-time-typed `RequestClose(TResult)` extension, and let the show call-site line up `TResult`. The presenter writes `, IRequestClose<UserDto>` and one push call `this.RequestClose(dto)` — no event, no `CloseSink` property, no `RaiseClose` helper.

3. **New internal seams:** `ICloseSink`, `ICloseParticipant` (both internal, in `WinformsMVP` assembly; the test project sees them via the existing `InternalsVisibleTo("WinformsMVP.Samples.Tests")`).

4. **New `WindowPresenterBaseCore<TView>`** (public abstract, "do not inherit directly") holds the closing machinery (sink field, `CanClose` virtuals, `RequestClose(status)`, explicit `ICloseParticipant` impl). Both `WindowPresenterBase<TView>` and `WindowPresenterBase<TView, TParam>` derive from it. A public class cannot derive from an internal base, so this intermediate is public.

5. **Sink injected before `Initialize` (A3).** Order on every path: `AttachView` → `BindSink` → `Initialize[(param)]` → wire `FormClosing`/`FormClosed` → show. So `RequestClose` is usable from `OnInitialize`.

6. **Close-before-show (A3 deeper).** If `Close()` is called before the form has a handle (e.g. `RequestClose` in `OnInitialize`), the controller does **not** call `Form.Close()`; it sets `CloseRequestedBeforeShow` + stashes the result. Managed: navigator checks the flag and converges without showing. Adopted: the controller subscribes to `Form.Shown` once and closes immediately on first show.

7. **Async deferral (B1+B2): no `_deferring` field.** `OnFormClosing` uses a `sync` closure local to tell synchronous answers (fast path: set `e.Cancel`) from deferred ones (`e.Cancel = true`, wait; the async `proceed(true)` sets `_suppressGate` and re-closes, which the gate's first line consumes). `proceed(false)` async just leaves the window open — nothing leaks.

8. **`CanClose` exceptions (C2): block + log.** The controller wraps `CanCloseGate` in try/catch; on throw it logs via `PlatformServices.Default` logger and treats the decision as **block** (`e.Cancel = true`) — safer than risking data loss.

9. **Composite windows (C1):** documentation only — the window Presenter aggregates child dirty state inside its single `CanClose`.

10. **Disposal ownership:** the controller disposes the Presenter on `FormClosed`. It disposes the *form* only when constructed with `disposeForm: true` (Managed modal, where the navigator used to; and Adopted). Non-modal `Show()` auto-disposes anyway; `Form.Dispose()`/`Presenter.Dispose()` are idempotent.

11. **`MapCloseReason`** moves from the public `WindowClosingBridge` into an internal `static CloseReasonMap.From(...)`. `WindowClosingBridge` (public, added in commit `ed87667`) is deleted — Adopted windows now use `Connect`, so no public mapping is needed.

### Final type/signature reference (used consistently by all tasks)

```csharp
// Common/Interactions — UNCHANGED: CloseReason, InteractionStatus, InteractionResult<T>

// MVP/Presenters/ICloseSink.cs  (NEW, internal)
internal interface ICloseSink
{
    void Close(object result, InteractionStatus status);
}

// MVP/Presenters/ICloseParticipant.cs  (NEW, internal)
internal interface ICloseParticipant
{
    void BindCloseSink(ICloseSink sink);
    void RequestCloseCore(object result, InteractionStatus status);
    void CanCloseGate(CloseReason reason, Action<bool> proceed);
}

// MVP/Presenters/IRequestClose.cs  (REWRITTEN, public marker)
public interface IRequestClose<TResult> { }

// MVP/Presenters/RequestCloseExtensions.cs  (NEW, public)
public static class RequestCloseExtensions
{
    public static void RequestClose<TResult>(this IRequestClose<TResult> presenter,
        TResult result, InteractionStatus status = InteractionStatus.Ok)
        => ((ICloseParticipant)presenter).RequestCloseCore(result, status);
}

// MVP/Presenters/WindowPresenterBaseCore.cs  (NEW, public abstract)
public abstract class WindowPresenterBaseCore<TView> : PresenterBase<TView>, ICloseParticipant
    where TView : IWindowView
{
    private ICloseSink _closeSink;

    protected virtual bool CanClose(CloseReason reason) => true;
    protected virtual void CanClose(CloseReason reason, Action<bool> proceed) => proceed(CanClose(reason));
    protected void RequestClose(InteractionStatus status = InteractionStatus.Ok) => _closeSink?.Close(null, status);

    void ICloseParticipant.BindCloseSink(ICloseSink sink) => _closeSink = sink;
    void ICloseParticipant.RequestCloseCore(object result, InteractionStatus status) => _closeSink?.Close(result, status);
    void ICloseParticipant.CanCloseGate(CloseReason reason, Action<bool> proceed) => CanClose(reason, proceed);
}

// MVP/Presenters/IViewAttachable.cs  (MODIFIED)
internal interface IViewAttachable
{
    void AttachView(IViewBase view);
    bool IsViewAttached { get; }   // NEW
}

// Services/Implementations/CloseReasonMap.cs  (NEW, internal static)  — body = old WindowClosingBridge.MapCloseReason

// Services/Implementations/WindowCloseController.cs  (NEW, internal sealed : ICloseSink)
//   ctor(IWindowView view, ICloseParticipant presenter, Action<object,InteractionStatus> onClosed, bool disposeForm)
//   internal void BindSink()
//   internal void WireFormEvents()
//   internal bool CloseRequestedBeforeShow { get; }
//   internal void ConvergeWithoutShow()
//   public void Close(object result, InteractionStatus status)   // ICloseSink

// MVP/Presenters/WindowPresenterConnectExtensions.cs  (NEW, public)
//   Connect<TView>(this WindowPresenterBase<TView>, TView, Action<InteractionResult> onClosed = null)
//   Connect<TView, TResult>(this WindowPresenterBase<TView>, TView, Action<InteractionResult<TResult>> onClosed)
//   Connect<TView, TParam>(this WindowPresenterBase<TView, TParam>, TView, TParam, Action<InteractionResult> onClosed = null)
//   Connect<TView, TParam, TResult>(this WindowPresenterBase<TView, TParam>, TView, TParam, Action<InteractionResult<TResult>> onClosed)
```

### Files to be created / modified / deleted

**Create (core):** `src/WinformsMVP/MVP/Presenters/ICloseSink.cs`, `ICloseParticipant.cs`, `RequestCloseExtensions.cs`, `WindowPresenterBaseCore.cs`, `WindowPresenterConnectExtensions.cs`; `src/WinformsMVP/Services/Implementations/CloseReasonMap.cs`, `WindowCloseController.cs`.

**Modify (core):** `IRequestClose.cs` (rewrite to marker), `IViewAttachable.cs` (+`IsViewAttached`), `PresenterBase.cs` (implement `IsViewAttached`), `WindowPresenterBase.cs` (+`WindowPresenterBaseCore`), `WindowPresenterBaseWithParams.cs` (+`WindowPresenterBaseCore`), `Services/Implementations/WindowNavigator.cs` (controller integration).

**Delete (core):** `MVP/Views/IWindowView.cs` closing members (keep file), `Common/Events/WindowClosingEventArgs.cs`, `Common/Events/CloseRequestedEventArgs.cs`, `Services/WindowClosingBridge.cs`, `Services/Implementations/WindowCloseCoordinator.cs`.

**Migrate (samples):** ~30 Forms (remove closing boilerplate), 7 Presenters implementing `IRequestClose<T>` (event → marker + `CanClose`).

**Tests:** rewrite `Presenters/WindowClosingTests.cs`; delete `Services/WindowCloseCoordinatorTests.cs` + `Services/WindowClosingBridgeTests.cs`; add `Services/WindowCloseControllerTests.cs`, `Presenters/CanCloseTests.cs`, `Presenters/ConnectTests.cs`; update `Services/WindowNavigatorTests.cs`, mocks.

**Docs:** `CLAUDE.md` (closing section), `wiki/Concept-Window-Closing-Model.md`, `wiki/HowTo-Handle-Window-Closing.md`, `wiki/Reference-Presenter-Base-Classes.md`, `wiki/Design-Rules.md`, `wiki/Reference-WindowNavigator.md`, plus references in `README.md`/`CHANGELOG.md`/`Glossary.md`/`_Sidebar.md`/`Home.md`/`FAQ.md`/`Troubleshooting.md`.

---

## Phase 1 — Core close seams (TDD)

### Task 1: `ICloseSink` + `ICloseParticipant` + `IRequestClose` marker + `RequestClose` extension

**Files:**
- Create: `src/WinformsMVP/MVP/Presenters/ICloseSink.cs`
- Create: `src/WinformsMVP/MVP/Presenters/ICloseParticipant.cs`
- Create: `src/WinformsMVP/MVP/Presenters/RequestCloseExtensions.cs`
- Modify: `src/WinformsMVP/MVP/Presenters/IRequestClose.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class CanCloseTests
    {
        public interface IFakeView : IWindowView
        {
            bool HasUnsavedChanges { get; set; }
        }

        private sealed class FakeView : IFakeView
        {
            public bool HasUnsavedChanges { get; set; }
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
        }

        // Records the captured sink so a test can assert Push routing without a Form.
        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object result, InteractionStatus status)> Closed
                = new List<(object, InteractionStatus)>();
            public void Close(object result, InteractionStatus status) => Closed.Add((result, status));
        }

        private sealed class EditPresenter : WindowPresenterBase<IFakeView>, IRequestClose<string>
        {
            protected override void OnViewAttached() { }
            protected override bool CanClose(CloseReason reason)
            {
                if (reason != CloseReason.Normal) return true;
                return !View.HasUnsavedChanges;
            }
            public void PushSave(string r) => this.RequestClose(r, InteractionStatus.Ok);
        }

        private static EditPresenter Attached(FakeView view)
        {
            var p = new EditPresenter();
            p.AttachView(view);
            p.Initialize();
            return p;
        }

        [Fact]
        public void CanClose_Default_Allows()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = false });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);
            Assert.True(allow);
        }

        [Fact]
        public void CanClose_DirtyNormal_Blocks()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);
            Assert.False(allow);
        }

        [Fact]
        public void CanClose_SystemShutdown_BypassesDirty()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.SystemShutdown, ok => allow = ok);
            Assert.True(allow);
        }

        [Fact]
        public void RequestClose_RoutesResultThroughInjectedSink()
        {
            var p = Attached(new FakeView());
            var sink = new RecordingSink();
            ((ICloseParticipant)p).BindCloseSink(sink);

            p.PushSave("hello");

            Assert.Single(sink.Closed);
            Assert.Equal("hello", sink.Closed[0].result);
            Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
        }

        [Fact]
        public void Presenter_IsAssignableToIRequestClose()
        {
            Assert.IsAssignableFrom<IRequestClose<string>>(new EditPresenter());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails (does not compile)**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~CanCloseTests`
Expected: BUILD FAIL — `ICloseSink`, `ICloseParticipant`, `RequestClose` extension, and the new `CanClose` override do not exist yet. (`WindowPresenterBaseCore`/`CanClose` arrive in Task 2; this test compiles only after Tasks 1–2.)

- [ ] **Step 3: Create `ICloseSink.cs`**

```csharp
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Framework-internal close sink injected into a window Presenter. The Presenter pushes a
    /// close by calling <see cref="RequestCloseExtensions.RequestClose{TResult}"/> (typed) or the
    /// base <c>RequestClose(status)</c> (untyped); both route here. The result is carried as
    /// <see cref="object"/> and cast back to the concrete result type at the show boundary, so a
    /// single non-generic controller can serve presenters of any result type.
    /// </summary>
    internal interface ICloseSink
    {
        void Close(object result, InteractionStatus status);
    }
}
```

- [ ] **Step 4: Create `ICloseParticipant.cs`**

```csharp
using System;
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Framework-internal hooks the <c>WindowCloseController</c> uses to drive a Presenter's
    /// close lifecycle without referencing its <c>TView</c> or <c>TResult</c>. Implemented
    /// explicitly by <see cref="WindowPresenterBaseCore{TView}"/>.
    /// </summary>
    internal interface ICloseParticipant
    {
        /// <summary>Injects the close sink (Push). Called once, before <c>Initialize</c>.</summary>
        void BindCloseSink(ICloseSink sink);

        /// <summary>Routes a typed/untyped <c>RequestClose</c> to the injected sink.</summary>
        void RequestCloseCore(object result, InteractionStatus status);

        /// <summary>Runs the Pull gate. <paramref name="proceed"/>(true) allows, (false) blocks.
        /// Called synchronously for sync deciders; may be deferred for async ones.</summary>
        void CanCloseGate(CloseReason reason, Action<bool> proceed);
    }
}
```

- [ ] **Step 5: Rewrite `IRequestClose.cs` to a marker**

```csharp
namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Marker implemented by window presenters that return a typed business result when they
    /// actively request close (the Push direction). It declares the result type once so the
    /// <see cref="RequestCloseExtensions.RequestClose{TResult}"/> extension is compile-time typed
    /// and lines up with <c>WindowNavigator.ShowWindowAsModal&lt;TPresenter, TResult&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Must be implemented on a type deriving from <see cref="WindowPresenterBaseCore{TView}"/>
    /// (i.e. any <c>WindowPresenterBase</c>); the framework injects the close sink there. Push:
    /// <c>this.RequestClose(result)</c>. Pull: override <c>CanClose(CloseReason)</c>.
    /// </remarks>
    /// <typeparam name="TResult">The business result type.</typeparam>
    public interface IRequestClose<TResult>
    {
    }
}
```

- [ ] **Step 6: Create `RequestCloseExtensions.cs`**

```csharp
using WinformsMVP.Common;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Gives <see cref="IRequestClose{TResult}"/> presenters a strongly-typed Push call. The
    /// result is forwarded through the internal close sink (boxed to <see cref="object"/>) and
    /// cast back to <typeparamref name="TResult"/> at the show boundary.
    /// </summary>
    public static class RequestCloseExtensions
    {
        public static void RequestClose<TResult>(this IRequestClose<TResult> presenter,
            TResult result, InteractionStatus status = InteractionStatus.Ok)
            => ((ICloseParticipant)presenter).RequestCloseCore(result, status);
    }
}
```

- [ ] **Step 7: Run the test (still failing on `WindowPresenterBaseCore`/`CanClose`)**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: PASS (core compiles). The test still won't compile until Task 2 adds `CanClose`/`WindowPresenterBaseCore`. Proceed to Task 2 before re-running the test.

- [ ] **Step 8: Commit**

```bash
git add src/WinformsMVP/MVP/Presenters/ICloseSink.cs src/WinformsMVP/MVP/Presenters/ICloseParticipant.cs src/WinformsMVP/MVP/Presenters/RequestCloseExtensions.cs src/WinformsMVP/MVP/Presenters/IRequestClose.cs tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs
git commit -m "feat(closing): add close sink + participant seams, IRequestClose marker"
```

---

### Task 2: `WindowPresenterBaseCore` + re-parent the two window base classes

**Files:**
- Create: `src/WinformsMVP/MVP/Presenters/WindowPresenterBaseCore.cs`
- Modify: `src/WinformsMVP/MVP/Presenters/WindowPresenterBase.cs:13-17`
- Modify: `src/WinformsMVP/MVP/Presenters/WindowPresenterBaseWithParams.cs:14-18`
- Test: `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs` (from Task 1)

- [ ] **Step 1: Create `WindowPresenterBaseCore.cs`**

```csharp
using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Shared base for window presenters carrying the close machinery: the Pull gate
    /// (<see cref="CanClose(CloseReason)"/> / its callback overload) and the Push sink
    /// (<see cref="RequestClose(InteractionStatus)"/>). Do not inherit directly — use
    /// <see cref="WindowPresenterBase{TView}"/> or <see cref="WindowPresenterBase{TView, TParam}"/>.
    /// </summary>
    /// <remarks>
    /// Closing is the Presenter's policy, so it lives here, not on the View. The framework calls
    /// <see cref="ICloseParticipant.CanCloseGate"/> (Pull) and injects the sink via
    /// <see cref="ICloseParticipant.BindCloseSink"/> (Push) — there are no events to subscribe to.
    /// </remarks>
    public abstract class WindowPresenterBaseCore<TView> : PresenterBase<TView>, ICloseParticipant
        where TView : IWindowView
    {
        private ICloseSink _closeSink;

        /// <summary>
        /// Pull gate (synchronous). Override to veto a close. Return <c>true</c> to allow.
        /// Inspect <paramref name="reason"/> — never block <see cref="CloseReason.SystemShutdown"/>
        /// or <see cref="CloseReason.TaskManager"/> with a modal prompt. Default allows.
        /// </summary>
        protected virtual bool CanClose(CloseReason reason) => true;

        /// <summary>
        /// Pull gate (asynchronous). Override when the decision needs a callback (async save /
        /// server check). Call <paramref name="proceed"/>(true) to allow, (false) to block — from
        /// inside a continuation if needed. Uses <see cref="Action{T}"/>, never <c>Task</c>, so it
        /// is net40-safe. Default forwards to the synchronous <see cref="CanClose(CloseReason)"/>.
        /// </summary>
        protected virtual void CanClose(CloseReason reason, Action<bool> proceed)
            => proceed(CanClose(reason));

        /// <summary>Push a close with no result. Use the <c>RequestClose(result, status)</c>
        /// extension (from <see cref="IRequestClose{TResult}"/>) when returning a typed result.</summary>
        protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)
            => _closeSink?.Close(null, status);

        void ICloseParticipant.BindCloseSink(ICloseSink sink) => _closeSink = sink;
        void ICloseParticipant.RequestCloseCore(object result, InteractionStatus status)
            => _closeSink?.Close(result, status);
        void ICloseParticipant.CanCloseGate(CloseReason reason, Action<bool> proceed)
            => CanClose(reason, proceed);
    }
}
```

- [ ] **Step 2: Re-parent `WindowPresenterBase<TView>`**

In `src/WinformsMVP/MVP/Presenters/WindowPresenterBase.cs`, change the class declaration (lines 13-17) from:

```csharp
    public abstract class WindowPresenterBase<TView> :
        PresenterBase<TView>,
        IViewAttacher<TView>,
        IInitializable
        where TView : IWindowView
```

to:

```csharp
    public abstract class WindowPresenterBase<TView> :
        WindowPresenterBaseCore<TView>,
        IViewAttacher<TView>,
        IInitializable
        where TView : IWindowView
```

(Leave the rest of the file unchanged.)

- [ ] **Step 3: Re-parent `WindowPresenterBase<TView, TParam>`**

In `src/WinformsMVP/MVP/Presenters/WindowPresenterBaseWithParams.cs`, change the class declaration (lines 14-18) from `PresenterBase<TView>,` to `WindowPresenterBaseCore<TView>,` (same edit as Step 2).

- [ ] **Step 4: Run the CanCloseTests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~CanCloseTests`
Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/MVP/Presenters/WindowPresenterBaseCore.cs src/WinformsMVP/MVP/Presenters/WindowPresenterBase.cs src/WinformsMVP/MVP/Presenters/WindowPresenterBaseWithParams.cs
git commit -m "feat(closing): add WindowPresenterBaseCore with CanClose/RequestClose"
```

---

### Task 3: `IViewAttachable.IsViewAttached`

**Files:**
- Modify: `src/WinformsMVP/MVP/Presenters/IViewAttachable.cs:18-22`
- Modify: `src/WinformsMVP/MVP/Presenters/PresenterBase.cs:110`
- Test: `tests/WinformsMVP.Samples.Tests/Presenters/ConnectTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/WinformsMVP.Samples.Tests/Presenters/ConnectTests.cs` (only the `IsViewAttached` test for now; `Connect` tests are added in Task 6):

```csharp
using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class ConnectTests
    {
        public interface IFakeView : IWindowView { }

        private sealed class FakeView : IFakeView
        {
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
        }

        private sealed class FakePresenter : WindowPresenterBase<IFakeView>
        {
            protected override void OnViewAttached() { }
        }

        [Fact]
        public void IsViewAttached_FalseBeforeAttach_TrueAfter()
        {
            var p = new FakePresenter();
            Assert.False(((IViewAttachable)p).IsViewAttached);

            p.AttachView(new FakeView());

            Assert.True(((IViewAttachable)p).IsViewAttached);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~ConnectTests`
Expected: BUILD FAIL — `IViewAttachable` has no `IsViewAttached`.

- [ ] **Step 3: Add `IsViewAttached` to the interface**

In `src/WinformsMVP/MVP/Presenters/IViewAttachable.cs`, change the interface body from:

```csharp
    internal interface IViewAttachable
    {
        void AttachView(IViewBase view);
    }
```

to:

```csharp
    internal interface IViewAttachable
    {
        void AttachView(IViewBase view);

        /// <summary>True once a view has been attached. Lets the <c>Connect</c> extension stay
        /// idempotent without touching the protected <c>View</c> property.</summary>
        bool IsViewAttached { get; }
    }
```

- [ ] **Step 4: Implement it in `PresenterBase`**

In `src/WinformsMVP/MVP/Presenters/PresenterBase.cs`, immediately after line 110 (`void IViewAttachable.AttachView(IViewBase view) => SetView((TView)view);`), add:

```csharp
        bool IViewAttachable.IsViewAttached => View != null;
```

- [ ] **Step 5: Run the test**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~ConnectTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/WinformsMVP/MVP/Presenters/IViewAttachable.cs src/WinformsMVP/MVP/Presenters/PresenterBase.cs tests/WinformsMVP.Samples.Tests/Presenters/ConnectTests.cs
git commit -m "feat(closing): add IViewAttachable.IsViewAttached for Connect idempotency"
```

---

## Phase 2 — The controller (TDD)

### Task 4: `CloseReasonMap` (internal) extracted from `WindowClosingBridge`

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/CloseReasonMap.cs`
- Test: covered indirectly by Task 5 controller tests (the mapping was already tested via `WindowClosingBridgeTests`, deleted in Task 9).

- [ ] **Step 1: Create `CloseReasonMap.cs`** (body copied verbatim from the current `WindowClosingBridge.MapCloseReason`)

```csharp
using WinFormsCloseReason = System.Windows.Forms.CloseReason;
using MvpCloseReason = WinformsMVP.Common.CloseReason;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Single source of the WinForms-to-framework close-reason mapping. Internal because the
    /// only callers are the <see cref="WindowCloseController"/> and <c>WindowNavigator</c>;
    /// presenters see only the framework <see cref="MvpCloseReason"/>.
    /// </summary>
    internal static class CloseReasonMap
    {
        public static MvpCloseReason From(WinFormsCloseReason reason)
        {
            switch (reason)
            {
                case WinFormsCloseReason.UserClosing:
                    return MvpCloseReason.Normal;
                case WinFormsCloseReason.WindowsShutDown:
                    return MvpCloseReason.SystemShutdown;
                case WinFormsCloseReason.TaskManagerClosing:
                    return MvpCloseReason.TaskManager;
                case WinFormsCloseReason.FormOwnerClosing:
                case WinFormsCloseReason.MdiFormClosing:
                    return MvpCloseReason.ParentClosing;
                case WinFormsCloseReason.ApplicationExitCall:
                    return MvpCloseReason.Normal;
                default:
                    return MvpCloseReason.Unknown;
            }
        }
    }
}
```

- [ ] **Step 2: Build core**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: PASS (note: `WindowClosingBridge` still exists at this point — both compile; the bridge is deleted in Task 9).

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/CloseReasonMap.cs
git commit -m "refactor(closing): extract CloseReasonMap from WindowClosingBridge"
```

---

### Task 5: `WindowCloseController`

**Files:**
- Create: `src/WinformsMVP/Services/Implementations/WindowCloseController.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/WindowCloseControllerTests.cs`

The controller touches `System.Windows.Forms.Form`, so tests use a real `Form` subclass (the test project is net48, WinForms-capable, as the deleted `WindowClosingBridgeTests` already were). The Presenter side is exercised through a tiny `ICloseParticipant` fake so the tests stay focused on the controller's state machine.

- [ ] **Step 1: Write the failing tests**

Create `tests/WinformsMVP.Samples.Tests/Services/WindowCloseControllerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class WindowCloseControllerTests
    {
        // A Form that also satisfies IWindowView so the controller's `view is Form` check passes.
        private sealed class FakeWindow : Form, IWindowView
        {
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            bool IWindowView.IsDisposed => base.IsDisposed;
            void IWindowView.Activate() => base.Activate();
        }

        // Scriptable Pull gate + sink capture, standing in for a real presenter.
        private sealed class FakeParticipant : ICloseParticipant, IDisposable
        {
            public Func<CloseReason, bool> SyncDecision;          // null => allow
            public Action<CloseReason, Action<bool>> AsyncGate;   // overrides SyncDecision when set
            public ICloseSink BoundSink;
            public bool Disposed;

            public void BindCloseSink(ICloseSink sink) => BoundSink = sink;
            public void RequestCloseCore(object result, InteractionStatus status)
                => BoundSink.Close(result, status);
            public void CanCloseGate(CloseReason reason, Action<bool> proceed)
            {
                if (AsyncGate != null) { AsyncGate(reason, proceed); return; }
                proceed(SyncDecision?.Invoke(reason) ?? true);
            }
            public void Dispose() => Disposed = true;
        }

        private static (FakeWindow form, FakeParticipant presenter, List<(object, InteractionStatus)> results, WindowCloseController controller)
            Build(bool disposeForm = true)
        {
            var form = new FakeWindow();
            var presenter = new FakeParticipant();
            var results = new List<(object, InteractionStatus)>();
            var controller = new WindowCloseController(
                form, presenter, (r, s) => results.Add((r, s)), disposeForm);
            controller.BindSink();
            controller.WireFormEvents();
            return (form, presenter, results, controller);
        }

        [Fact]
        public void UserClose_GateAllows_FormClosesAndConverges()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => true;

            form.Show();              // create handle so Close() is a real close
            form.Close();

            Assert.Single(results);
            Assert.Equal(InteractionStatus.Cancel, results[0].Item2); // user close => default Cancel
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void UserClose_GateBlocks_StaysOpen()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = r => r != CloseReason.Normal; // block normal

            form.Show();
            form.Close();             // gate vetoes

            Assert.True(form.Visible);
            Assert.Empty(results);
            form.Dispose();           // cleanup (gate would block forever otherwise)
        }

        [Fact]
        public void PresenterPush_SkipsGate_ConvergesWithResult()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => false; // gate would block — push must bypass it

            form.Show();
            presenter.RequestCloseCore("payload", InteractionStatus.Ok);

            Assert.Single(results);
            Assert.Equal("payload", results[0].Item1);
            Assert.Equal(InteractionStatus.Ok, results[0].Item2);
        }

        [Fact]
        public void AsyncGate_DeferThenAllow_ClosesWithoutReRunningGate()
        {
            var (form, presenter, results, _) = Build();
            Action<bool> stored = null;
            int gateRuns = 0;
            presenter.AsyncGate = (reason, proceed) => { gateRuns++; stored = proceed; }; // defer

            form.Show();
            form.Close();                 // deferred: cancelled, window stays open
            Assert.True(form.Visible);

            stored(true);                 // async allow => controller re-closes with suppress

            Assert.False(form.Visible);
            Assert.Equal(1, gateRuns);    // the suppressed re-close must NOT run the gate again
            Assert.Single(results);
        }

        [Fact]
        public void AsyncGate_DeferThenBlock_StaysOpen_NextCloseRunsGateAgain()
        {
            var (form, presenter, results, _) = Build();
            var stored = new List<Action<bool>>();
            int gateRuns = 0;
            presenter.AsyncGate = (reason, proceed) => { gateRuns++; stored.Add(proceed); };

            form.Show();
            form.Close();
            stored[stored.Count - 1](false);  // async block
            Assert.True(form.Visible);
            Assert.Equal(1, gateRuns);

            form.Close();                      // second close must run the gate again (B1 regression)
            Assert.Equal(2, gateRuns);
            form.Dispose();
        }

        [Fact]
        public void GateThrows_BlocksClose_AndDoesNotConverge()
        {
            var (form, presenter, results, _) = Build();
            presenter.AsyncGate = (reason, proceed) => throw new InvalidOperationException("boom");

            form.Show();
            form.Close();

            Assert.True(form.Visible);   // exception => safe default = block
            Assert.Empty(results);
            form.Dispose();
        }

        [Fact]
        public void CloseBeforeShow_DoesNotTouchForm_SetsFlag()
        {
            var (form, presenter, results, controller) = Build();

            presenter.RequestCloseCore("early", InteractionStatus.Ok); // no Show() yet

            Assert.True(controller.CloseRequestedBeforeShow);
            controller.ConvergeWithoutShow();
            Assert.Single(results);
            Assert.Equal("early", results[0].Item1);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~WindowCloseControllerTests`
Expected: BUILD FAIL — `WindowCloseController` does not exist.

- [ ] **Step 3: Create `WindowCloseController.cs`**

```csharp
using System;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Per-window controller that owns the entire WinForms close bridge for one Form: it is the
    /// Push sink, the Pull gate's bridge to <c>FormClosing</c>, and the result-converging
    /// <c>FormClosed</c> handler. The only place in the framework that knows <see cref="Form"/>.
    /// One instance per window, so all suppress/defer state is plain fields — no shared tables.
    /// </summary>
    internal sealed class WindowCloseController : ICloseSink
    {
        private readonly Form _form;
        private readonly ICloseParticipant _presenter;
        private readonly Action<object, InteractionStatus> _onClosed;
        private readonly bool _disposeForm;
        private readonly ILogger _logger;

        private object _pendingResult;
        private InteractionStatus _pendingStatus = InteractionStatus.Cancel; // user X => Cancel
        private bool _suppressGate;            // self-initiated / re-close: skip the gate once
        private bool _closeRequestedBeforeShow;

        /// <param name="disposeForm">True when this controller owns the Form lifetime (Managed
        /// modal, Adopted). False for non-modal Managed, where WinForms disposes on close.</param>
        internal WindowCloseController(IWindowView view, ICloseParticipant presenter,
            Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            if (!(view is Form form))
                throw new ArgumentException(
                    "Window closing requires a Form-backed view.", nameof(view));
            _form = form;
            _presenter = presenter;
            _onClosed = onClosed;
            _disposeForm = disposeForm;
            _logger = PlatformServices.Default.LoggerFactory.CreateLogger(typeof(WindowCloseController));
        }

        /// <summary>Injects the Push sink. Call BEFORE <c>Initialize</c> so a Presenter can
        /// <c>RequestClose</c> from <c>OnInitialize</c>.</summary>
        internal void BindSink() => _presenter.BindCloseSink(this);

        /// <summary>Wires the Pull bridge and result converger. Call AFTER <c>Initialize</c>.</summary>
        internal void WireFormEvents()
        {
            _form.FormClosing += OnFormClosing;
            _form.FormClosed += OnFormClosed;
            // Adopted close-before-show: the caller shows the form itself, so we cannot skip the
            // Show; close it the instant it first appears.
            _form.Shown += OnFormShown;
        }

        /// <summary>True if a Push arrived before the form had a handle (e.g. RequestClose in
        /// OnInitialize). Managed callers check this and skip ShowDialog, calling
        /// <see cref="ConvergeWithoutShow"/> instead.</summary>
        internal bool CloseRequestedBeforeShow => _closeRequestedBeforeShow;

        /// <summary>Converge the pending result without ever showing the form. Managed-only path
        /// for close-before-show.</summary>
        internal void ConvergeWithoutShow()
        {
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.Shown -= OnFormShown;
            Converge();
        }

        // ── Push (ICloseSink) ───────────────────────────────────────────────────────
        public void Close(object result, InteractionStatus status)
        {
            _pendingResult = result;
            _pendingStatus = status;
            _suppressGate = true;

            if (!_form.IsHandleCreated && !_form.Visible)
            {
                _closeRequestedBeforeShow = true; // defer to ConvergeWithoutShow / OnFormShown
                return;
            }
            _form.Close();
        }

        private void OnFormShown(object sender, EventArgs e)
        {
            if (_closeRequestedBeforeShow)
                _form.Close(); // _suppressGate already true => gate skipped, converges normally
        }

        // ── Pull (FormClosing) ──────────────────────────────────────────────────────
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_suppressGate) { _suppressGate = false; return; } // self-initiated / re-close

            var reason = CloseReasonMap.From(e.CloseReason);
            bool sync = true;
            bool? decision = null;
            try
            {
                _presenter.CanCloseGate(reason, ok =>
                {
                    if (sync) decision = ok;                                  // synchronous answer
                    else if (ok) { _suppressGate = true; _form.Close(); }     // async allow => re-close
                    // async block: leave the window open, nothing to reset (no _deferring field)
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CanClose threw; blocking the close as a safe default.");
                decision = false;
            }
            sync = false;

            if (decision.HasValue) e.Cancel = !decision.Value; // fast path
            else e.Cancel = true;                              // deferring: wait for callback
        }

        // ── Converge (FormClosed) ───────────────────────────────────────────────────
        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            _form.FormClosing -= OnFormClosing;
            _form.FormClosed -= OnFormClosed;
            _form.Shown -= OnFormShown;
            Converge();
        }

        private void Converge()
        {
            _onClosed?.Invoke(_pendingResult, _pendingStatus);
            (_presenter as IDisposable)?.Dispose();
            if (_disposeForm) _form.Dispose();
        }
    }
}
```

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~WindowCloseControllerTests`
Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/WindowCloseController.cs tests/WinformsMVP.Samples.Tests/Services/WindowCloseControllerTests.cs
git commit -m "feat(closing): add WindowCloseController (Pull bridge + Push sink + deferral)"
```

---

## Phase 3 — Hosting integration

### Task 6: `Connect` extension (Adopted) — TDD

**Files:**
- Create: `src/WinformsMVP/MVP/Presenters/WindowPresenterConnectExtensions.cs`
- Modify: `tests/WinformsMVP.Samples.Tests/Presenters/ConnectTests.cs` (add cases)

- [ ] **Step 1: Add failing tests to `ConnectTests.cs`**

Append these members to the `ConnectTests` class (and add `using System.Windows.Forms; using WinformsMVP.Common; using WinformsMVP.Services.Implementations;` to the file's usings):

```csharp
        private sealed class FakeWindowForm : Form, IFakeView
        {
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            bool IWindowView.IsDisposed => base.IsDisposed;
            void IWindowView.Activate() => base.Activate();
        }

        private sealed class ResultPresenter : WindowPresenterBase<IFakeView>, IRequestClose<string>
        {
            public bool Initialized;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => Initialized = true;
            public void PushDone(string r) => this.RequestClose(r, InteractionStatus.Ok);
        }

        [Fact]
        public void Connect_AttachesAndInitializes_WhenNotYetAttached()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();

            presenter.Connect<IFakeView, string>(form, _ => { });

            Assert.True(((IViewAttachable)presenter).IsViewAttached);
            Assert.True(presenter.Initialized);
            form.Dispose();
        }

        [Fact]
        public void Connect_IsIdempotent_DoesNotReinitialize()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();
            presenter.AttachView(form);
            presenter.Initialize();           // caller already attached + initialized
            presenter.Initialized = false;    // observe whether Connect re-runs OnInitialize

            presenter.Connect<IFakeView, string>(form, _ => { });

            Assert.False(presenter.Initialized); // Connect must NOT re-initialize
            form.Dispose();
        }

        [Fact]
        public void Connect_Push_DeliversResultToOnClosed()
        {
            var form = new FakeWindowForm();
            var presenter = new ResultPresenter();
            InteractionResult<string> captured = null;
            presenter.Connect<IFakeView, string>(form, r => captured = r);

            form.Show();
            presenter.PushDone("hi");

            Assert.NotNull(captured);
            Assert.True(captured.IsOk);
            Assert.Equal("hi", captured.Value);
        }

        [Fact]
        public void Connect_NonFormView_Throws()
        {
            var presenter = new ResultPresenter();
            Assert.Throws<ArgumentException>(
                () => presenter.Connect<IFakeView, string>(new FakeView(), _ => { }));
        }
```

(`FakeView` here is the non-Form fake from Task 3, which must fail the `view is Form` check.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~ConnectTests`
Expected: BUILD FAIL — no `Connect` extension.

- [ ] **Step 3: Create `WindowPresenterConnectExtensions.cs`**

```csharp
using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Adopted hosting: connect a window Presenter to a Form you create and <c>Show</c> yourself
    /// (legacy migration, the application shell, or a Form that owns its own Presenter). Does the
    /// same wiring as <c>WindowNavigator</c> — attach + initialize + Pull bridge + Push sink +
    /// result converging — but the caller owns the <c>Show</c>. Idempotent: a Presenter the caller
    /// already attached/initialized is only given the close controller.
    /// </summary>
    /// <remarks>
    /// Single-owner rule: a Form connected here must NOT also be shown through
    /// <see cref="Services.IWindowNavigator"/>. The <c>view is Form</c> check happens in the
    /// controller constructor (the framework's one runtime view→Form boundary).
    /// </remarks>
    public static class WindowPresenterConnectExtensions
    {
        public static void Connect<TView>(this WindowPresenterBase<TView> presenter,
            TView view, Action<InteractionResult> onClosed = null) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(),
                (res, status) => onClosed?.Invoke(BuildResult(status)), disposeForm: true);

        public static void Connect<TView, TResult>(this WindowPresenterBase<TView> presenter,
            TView view, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(),
                (res, status) => onClosed?.Invoke(BuildResult<TResult>(res, status)), disposeForm: true);

        public static void Connect<TView, TParam>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult> onClosed = null) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(param),
                (res, status) => onClosed?.Invoke(BuildResult(status)), disposeForm: true);

        public static void Connect<TView, TParam, TResult>(this WindowPresenterBase<TView, TParam> presenter,
            TView view, TParam param, Action<InteractionResult<TResult>> onClosed) where TView : IWindowView
            => ConnectCore(presenter, view, () => presenter.Initialize(param),
                (res, status) => onClosed?.Invoke(BuildResult<TResult>(res, status)), disposeForm: true);

        private static void ConnectCore(object presenter, IWindowView view,
            Action initialize, Action<object, InteractionStatus> onClosed, bool disposeForm)
        {
            // Construct first: validates `view is Form` before any side effect (no half-attach).
            var controller = new WindowCloseController(
                view, (ICloseParticipant)presenter, onClosed, disposeForm);

            var attachable = (IViewAttachable)presenter;
            if (!attachable.IsViewAttached)
            {
                attachable.AttachView(view);
                controller.BindSink();   // A3: sink before Initialize
                initialize();
            }
            else
            {
                controller.BindSink();   // already attached/initialized: just add the sink
            }
            controller.WireFormEvents();
        }

        private static InteractionResult BuildResult(InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult.Ok();
                case InteractionStatus.Error: return InteractionResult.Error("Operation failed");
                default: return InteractionResult.Cancel();
            }
        }

        private static InteractionResult<TResult> BuildResult<TResult>(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult<TResult>.Ok((TResult)result);
                case InteractionStatus.Error: return InteractionResult<TResult>.Error("Operation failed");
                default: return InteractionResult<TResult>.Cancel();
            }
        }
    }
}
```

> **Verify before implementing:** confirm `InteractionResult` (non-generic) exposes `Ok()`, `Cancel()`, `Error(string)`. If only the generic `InteractionResult<T>` exists, change the non-generic `Connect`/`BuildResult` overloads to `InteractionResult<object>`. Check `src/WinformsMVP/Common/Interactions/`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~ConnectTests`
Expected: All ConnectTests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/MVP/Presenters/WindowPresenterConnectExtensions.cs tests/WinformsMVP.Samples.Tests/Presenters/ConnectTests.cs
git commit -m "feat(closing): add presenter.Connect for Adopted hosting"
```

---

### Task 7: Integrate the controller into `WindowNavigator`

**Files:**
- Modify: `src/WinformsMVP/Services/Implementations/WindowNavigator.cs` (`AttachModalCloseHandlers`, `AttachNonModalCloseHandlers`, `WireCloseGate`, `CreateAndBindForm` init ordering, the four `ShowWindowAsModal`/`ShowWindow` entry points)
- Test: `tests/WinformsMVP.Samples.Tests/Services/WindowNavigatorTests.cs` (verify after Task 10 rewires fakes)

This is the largest single edit. The new shape replaces the `IRequestClose<TResult>.CloseRequested` event subscription and `WindowCloseCoordinator` with one `WindowCloseController` per window, and moves Initialize to **after** `BindSink`.

- [ ] **Step 1: Replace the two close-handler methods + `WireCloseGate` with controller wiring**

In `src/WinformsMVP/Services/Implementations/WindowNavigator.cs`, delete `AttachModalCloseHandlers<TResult>` (lines ~254-321), `AttachNonModalCloseHandlers<TResult>` (lines ~323-410), and `WireCloseGate` (lines ~478-487). Replace with:

```csharp
        // ── Close wiring via WindowCloseController ──────────────────────────────────
        // The controller is the single owner of the FormClosing/FormClosed bridge and the Push
        // sink. Initialize is run by the caller AFTER BindSink (see CreateFormForPresenter usage).

        private WindowCloseController WireController<TResult>(
            IPresenter presenter, Form form, Action<InteractionResult<TResult>> onClosed, bool disposeForm)
        {
            var controller = new WindowCloseController(
                (IWindowView)form,
                (ICloseParticipant)presenter,
                (res, status) => onClosed(BuildResult<TResult>(res, status)),
                disposeForm);
            controller.BindSink();
            return controller;
        }

        private static InteractionResult<TResult> BuildResult<TResult>(object result, InteractionStatus status)
        {
            switch (status)
            {
                case InteractionStatus.Ok: return InteractionResult<TResult>.Ok((TResult)result);
                case InteractionStatus.Error: return InteractionResult<TResult>.Error("Operation failed");
                case InteractionStatus.Cancel:
                default: return InteractionResult<TResult>.Cancel();
            }
        }
```

Add `using WinformsMVP.MVP.Presenters;` if not present (it is, line 7) and ensure `WindowCloseController`/`ICloseParticipant` resolve (same assembly).

- [ ] **Step 2: Rewrite the modal entry points to the new ordering**

Replace `ShowWindowAsModal<TPresenter, TResult>` (lines ~35-54) body with:

```csharp
        public InteractionResult<TResult> ShowWindowAsModal<TPresenter, TResult>(TPresenter presenter, IWin32Window owner = null) where TPresenter : IPresenter
        {
            var form = CreateFormForPresenter(presenter, callInitialize: false); // attach only
            InteractionResult<TResult> result = InteractionResult<TResult>.Cancel();

            var controller = WireController<TResult>(presenter, form, r => result = r, disposeForm: true);
            if (presenter is IInitializable initializable) initializable.Initialize(); // after BindSink
            controller.WireFormEvents();

            if (controller.CloseRequestedBeforeShow)
            {
                controller.ConvergeWithoutShow();
                return result;
            }

            if (owner != null) form.ShowDialog(owner); else form.ShowDialog();
            return result; // presenter + form disposed by the controller on FormClosed
        }
```

Modal passes `disposeForm: true` (the controller now disposes the form the navigator used to dispose). The non-modal path passes `disposeForm: false`.

Apply the same transformation to `ShowWindowAsModal<TPresenter, TParam, TResult>` (lines ~64-87), calling `presenter.Initialize(parameters)` instead of the parameterless initialize, after `WireController` (which calls `BindSink` internally). Remove the now-redundant trailing `(presenter as IDisposable)?.Dispose();` (the controller disposes the presenter).

- [ ] **Step 3: Rewrite the non-modal core (`ShowWindowInternal`, `ShowWindow<...,TParam,...>`)**

For non-modal, the form is shown with `Show()` (WinForms auto-disposes on close), so pass `disposeForm: false`. Move `Initialize`/`Initialize(param)` to after `BindSink`. The `_openForms` registration and key handling stay. Replace the `AttachNonModalCloseHandlers` call sites with:

```csharp
            var controller = WireController<TResult>(presenter, newForm, safeOnClosed, disposeForm: false);
            RegisterOpenForm(instanceKey, newForm);
            presenter.Initialize(parameters);     // or initializable.Initialize() in ShowWindowInternal
            controller.WireFormEvents();

            // Non-modal: a close-before-show is handled by the controller's Shown handler when
            // the caller's Show() runs below.
            if (owner != null) newForm.Show(owner); else newForm.Show();
            return (IWindowView)newForm;
```

Add the helper for the `_openForms` bookkeeping and the FormClosed-driven removal (previously inside `AttachNonModalCloseHandlers`). Since the controller now owns FormClosed, add an `onClosed`-side removal: in the non-modal path, wrap `safeOnClosed` so it also removes the key:

```csharp
            Action<InteractionResult<TResult>> safeOnClosed = r =>
            {
                if (instanceKey != null) { lock (_lock) { _openForms.Remove(instanceKey); } }
                (onClosed ?? (x => { })).Invoke(r);
            };
```

Make `WireController` overload accept `disposeForm`. Update both modal callers to `disposeForm: true`.

> **Note for the implementer:** the non-modal `_openForms` add originally lived inside `AttachNonModalCloseHandlers`; relocate it to a small `RegisterOpenForm(instanceKey, form)` private method (thread-safe `lock (_lock)`), and do the removal in the wrapped `safeOnClosed` as shown. This preserves singleton-per-key behavior. Update both non-modal entry points (`ShowWindowInternal` and `ShowWindow<TPresenter, TParam, TResult>`).

- [ ] **Step 4: Update `CreateAndBindForm` comment**

In `CreateAndBindForm` (lines ~435-468), the comment at lines ~452-454 ("The FormClosing → IWindowView.OnClosing bridge is wired by WireCloseGate …") is now stale. Replace with:

```csharp
            // The FormClosing/FormClosed bridge + Push sink are wired by WindowCloseController
            // after this returns (it must inject the sink before Initialize runs), so nothing is
            // set up here beyond attaching the view.
```

- [ ] **Step 5: Build core**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: PASS. (Tests still reference deleted types until Phase 4; full test run happens after Task 10.)

- [ ] **Step 6: Commit**

```bash
git add src/WinformsMVP/Services/Implementations/WindowNavigator.cs
git commit -m "feat(closing): drive WindowNavigator through WindowCloseController"
```

---

## Phase 4 — Remove the old model

### Task 8: Strip closing members from `IWindowView`

**Files:**
- Modify: `src/WinformsMVP/MVP/Views/IWindowView.cs`

- [ ] **Step 1: Replace the file with the slim interface**

```csharp
using System.Windows.Forms;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Interface for views that represent top-level windows (Forms / dialogs).
    /// </summary>
    /// <remarks>
    /// Closing is NOT a View concern. The framework drives it through the Presenter:
    /// override <c>CanClose(CloseReason)</c> to veto (Pull), call <c>RequestClose(...)</c> to
    /// close actively (Push). A Form implementing this interface writes zero closing code.
    /// </remarks>
    public interface IWindowView : IActionableView, IWin32Window
    {
        bool IsDisposed { get; }
        void Activate();
    }
}
```

- [ ] **Step 2: Build core (expected to FAIL where old members are referenced)**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: FAIL only if any remaining core file references `IWindowView.Closing`/`OnClosing` — there should be none after Task 7 (the navigator no longer calls them). If it builds, good. Samples/tests still reference them and are fixed in Phase 5–6.

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/MVP/Views/IWindowView.cs
git commit -m "feat(closing)!: remove Closing/OnClosing from IWindowView (breaking)"
```

---

### Task 9: Delete dead types

**Files:**
- Delete: `src/WinformsMVP/Common/Events/WindowClosingEventArgs.cs`
- Delete: `src/WinformsMVP/Common/Events/CloseRequestedEventArgs.cs`
- Delete: `src/WinformsMVP/Services/WindowClosingBridge.cs`
- Delete: `src/WinformsMVP/Services/Implementations/WindowCloseCoordinator.cs`
- Delete: `tests/WinformsMVP.Samples.Tests/Services/WindowCloseCoordinatorTests.cs`
- Delete: `tests/WinformsMVP.Samples.Tests/Services/WindowClosingBridgeTests.cs`

- [ ] **Step 1: Delete the files**

```bash
git rm src/WinformsMVP/Common/Events/WindowClosingEventArgs.cs \
       src/WinformsMVP/Common/Events/CloseRequestedEventArgs.cs \
       src/WinformsMVP/Services/WindowClosingBridge.cs \
       src/WinformsMVP/Services/Implementations/WindowCloseCoordinator.cs \
       tests/WinformsMVP.Samples.Tests/Services/WindowCloseCoordinatorTests.cs \
       tests/WinformsMVP.Samples.Tests/Services/WindowClosingBridgeTests.cs
```

- [ ] **Step 2: Build core**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git commit -m "chore(closing): delete WindowClosingBridge/Coordinator + event args"
```

---

## Phase 5 — Migrate samples

The transform is mechanical and identical everywhere. Two transforms:

**Transform F (every Form implementing `IWindowView`):** delete the closing-boilerplate region — the `_closing` field, the explicit `event ... IWindowView.Closing { add/remove }`, and `void IWindowView.OnClosing(...)`. Keep `IsDisposed`/`Activate` (explicit or implicit). Remove now-unused `using WinformsMVP.Common.Events;` if nothing else needs it.

**Transform P (every Presenter implementing `IRequestClose<T>`):** (1) keep `, IRequestClose<T>` on the class; (2) delete `public event EventHandler<CloseRequestedEventArgs<T>> CloseRequested;`; (3) delete the private `RaiseClose` helper; (4) replace `RaiseClose(x, status)` calls with `this.RequestClose(x, status)`; (5) move the dirty-check from the `View.Closing` subscription / `OnViewClosing` handler into a `protected override bool CanClose(CloseReason reason)`; (6) delete the `View.Closing += OnViewClosing;` line and the `OnViewClosing` method; (7) drop unused usings.

### Task 10: Migrate the Window Closing demo (the worked example) + update its tests

**Files:**
- Modify: `samples/WinformsMVP.Samples/WindowClosingDemo/WindowClosingDemoForm.cs`
- Modify: `samples/WinformsMVP.Samples/WindowClosingDemo/WindowClosingDemoPresenter.cs`
- Modify: `tests/WinformsMVP.Samples.Tests/Presenters/WindowClosingDemoPresenterTests.cs`

- [ ] **Step 1: Apply Transform F to `WindowClosingDemoForm.cs`**

Delete the entire `// ─── IWindowView Closing ───` region (lines ~115-132): the comment block, `bool IWindowView.IsDisposed => base.IsDisposed;` (Form already satisfies `IsDisposed`), `void IWindowView.Activate() => this.Activate();` (Form already satisfies `Activate`), the `_closing` field, the explicit `Closing` event, and `OnClosing`. Remove `using WinformsMVP.Common.Events;` (line 4). The class keeps only its real contract (`Text`, `StatusMessage`, `EditChanged`, `ActionBinder`).

> If removing the explicit `IsDisposed`/`Activate` causes an interface-satisfaction error (it should not — `Form` provides both publicly), re-add them as implicit members. Verify by building.

- [ ] **Step 2: Apply Transform P to `WindowClosingDemoPresenter.cs`**

Replace the whole class body (keep the `WindowClosingDemoActions` class and usings, drop `using WinformsMVP.Common.Events;`):

```csharp
    public class WindowClosingDemoPresenter : WindowPresenterBase<IWindowClosingDemoView>,
                                               IRequestClose<string>
    {
        private string _baseline;
        private bool IsDirty => View.Text != _baseline;

        protected override void OnViewAttached()
            => View.EditChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();

        protected override void OnInitialize()
        {
            _baseline = "(type something here)";
            View.Text = _baseline;
            View.StatusMessage = "Ready. Edit the text, then Save / Cancel / close the window.";
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(WindowClosingDemoActions.Save, OnSave, canExecute: () => IsDirty);
            Dispatcher.Register(WindowClosingDemoActions.Cancel, OnCancel);
        }

        // Pull: the single place dirty state gates a close.
        protected override bool CanClose(CloseReason reason)
        {
            if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
                return true;
            if (!IsDirty) return true;
            bool discard = Messages.ConfirmYesNo(
                "You have unsaved changes. Discard and close?", "Unsaved Changes");
            if (!discard) View.StatusMessage = "Close cancelled. Continue editing.";
            return discard;
        }

        // Push: finalize the dirty flag, then request close.
        private void OnSave()
        {
            var saved = View.Text;
            _baseline = saved;
            View.StatusMessage = "Saving and closing…";
            this.RequestClose(saved, InteractionStatus.Ok);
        }

        private void OnCancel()
        {
            _baseline = View.Text;
            View.StatusMessage = "Cancelled.";
            this.RequestClose(null, InteractionStatus.Cancel);
        }
    }
```

- [ ] **Step 3: Rewrite `WindowClosingDemoPresenterTests.cs` to the new API**

Read the existing file first. Replace any `view.RaiseClosing(...)` / `View.Closing` driving with `((ICloseParticipant)presenter).CanCloseGate(reason, ok => allow = ok)`, and any `CloseRequested`-event capture with a `RecordingSink` bound via `((ICloseParticipant)presenter).BindCloseSink(sink)` (pattern from `CanCloseTests`). Update the mock view (`MockWindowClosingDemoView` or inline fake) to drop `Closing`/`OnClosing`. Keep the behavioral assertions (dirty blocks normal close, shutdown bypasses, save pushes "Ok"+result, cancel pushes "Cancel").

> Concrete driving snippet for the dirty-blocks test:
> ```csharp
> bool? allow = null;
> ((ICloseParticipant)presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);
> Assert.False(allow);
> ```

- [ ] **Step 4: Build samples + run the demo's tests**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj`
Then: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~WindowClosingDemoPresenterTests`
Expected: sample builds; demo tests PASS.

- [ ] **Step 5: Commit**

```bash
git add samples/WinformsMVP.Samples/WindowClosingDemo/ tests/WinformsMVP.Samples.Tests/Presenters/WindowClosingDemoPresenterTests.cs
git commit -m "refactor(closing): migrate WindowClosingDemo to CanClose/RequestClose"
```

---

### Task 11: Apply Transform P to the remaining 6 `IRequestClose` presenters

**Files (apply Transform P to each):**
- `samples/WinformsMVP.Samples/NavigatorDemo/Dialogs/InputDialogPresenter.cs`
- `samples/WinformsMVP.Samples/NavigatorDemo/Dialogs/SimpleDialogPresenter.cs`
- `samples/WinformsMVP.Samples/NavigatorDemo/Dialogs/ConfirmDialogPresenter.cs`
- `samples/WinformsMVP.Samples/NavigatorDemo/Windows/CallbackWindowPresenter.cs`
- `samples/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs`
- `samples/MultiProjectDemo.UserModule/UserEditPresenter.cs`

- [ ] **Step 1: Migrate each presenter**

Read each file, then apply Transform P. For dialogs that have no dirty check (e.g. `InputDialogPresenter`), there is no `CanClose` to add — only steps P1–P4 apply. Concretely, `InputDialogPresenter` becomes (the `OnOk`/`OnCancel` bodies unchanged except the push call):

```csharp
    public class InputDialogPresenter : WindowPresenterBase<IInputDialogView>, IRequestClose<string>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(InputDialogActions.Ok, OnOk);
            Dispatcher.Register(InputDialogActions.Cancel, OnCancel);
        }

        protected override void OnInitialize() => View.SetPrompt("Please enter your name:");

        private void OnOk()
        {
            var input = View.GetInput();
            if (string.IsNullOrWhiteSpace(input))
            {
                Messages.ShowWarning("Please enter a value.", "Input Required");
                return;
            }
            this.RequestClose(input, InteractionStatus.Ok);
        }

        private void OnCancel() => this.RequestClose(null, InteractionStatus.Cancel);
    }
```

Drop `using WinformsMVP.Common.Events;` where it becomes unused. For `ComposeEmailPresenter` / `UserEditPresenter`, move any `OnViewClosing` dirty logic into `CanClose` (Transform P5–P6). For presenters that pushed via a local `RaiseClose`, delete that helper.

- [ ] **Step 2: Build samples**

Run: `dotnet build samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj` and `dotnet build samples/MultiProjectDemo.UserModule/MultiProjectDemo.UserModule.csproj`
Expected: PASS (Forms still need Transform F — if a Form in these demos still has the boilerplate it will fail at Task 12; build after Task 12 for a clean pass. If build fails here only on `IWindowView.Closing` in a Form, proceed to Task 12 and build together).

- [ ] **Step 3: Commit**

```bash
git add samples/WinformsMVP.Samples/NavigatorDemo samples/WinformsMVP.Samples/EmailDemo samples/MultiProjectDemo.UserModule/UserEditPresenter.cs
git commit -m "refactor(closing): migrate remaining IRequestClose presenters to RequestClose"
```

---

### Task 12: Apply Transform F to all remaining Forms

**Files (apply Transform F where the closing boilerplate is present):** every Form under `samples/` implementing `IWindowView`. Enumerate by searching for the boilerplate, then edit each:

- [ ] **Step 1: List the Forms still carrying the boilerplate**

Run (Grep tool, not bash): pattern `IWindowView\.OnClosing|EventHandler<WindowClosingEventArgs>` across `samples/`. Expected hits include (non-exhaustive — use the live search result): `WindowClosingDemoForm` (already done), `NavigatorDemo/Dialogs/{Input,Simple,Confirm}DialogForm.cs`, `NavigatorDemo/Windows/{NonModal,Singleton,Callback}WindowForm.cs`, `EmailDemo/{MainEmail,ComposeEmail}Form.cs`, `MultiProjectDemo.UserModule/{UserList,UserEdit}Form.cs`, `MultiProjectDemo.Shell/MainForm.cs`, `MultiProjectDemo.OrderModule/OrderListForm.cs`, and any other Form whose grep line matched in the survey.

- [ ] **Step 2: Apply Transform F to each listed Form**

For each: delete the `_closing` field, the explicit `event ... IWindowView.Closing { add/remove }`, and `void IWindowView.OnClosing(...)`. Keep `IsDisposed`/`Activate`. Drop `using WinformsMVP.Common.Events;` if unused. (Forms that referenced `WindowClosingBridge.ForwardClosing` in an `OnFormClosing` override — e.g. the shell `MainForm` — delete that override entirely; the controller now owns `FormClosing`.)

- [ ] **Step 3: Migrate the shell to `Connect` if it used the bridge**

For `samples/MultiProjectDemo.Shell/MainForm.cs` (and any shell using `Application.Run`): if it created its presenter and manually forwarded closing, replace with `presenter.Connect(this)` after `InitializeComponent()` (or in `OnLoad`), per the Adopted shell pattern. Read the file to see how the presenter is currently created; wire `Connect` accordingly. If the shell had no presenter, no change beyond Transform F.

- [ ] **Step 4: Build everything**

Run: `dotnet build winforms-mvp.sln`
Expected: PASS across all sample projects.

- [ ] **Step 5: Commit**

```bash
git add samples/
git commit -m "refactor(closing): remove closing boilerplate from all sample Forms"
```

---

## Phase 6 — Tests

### Task 13: Rewrite `WindowClosingTests.cs` and fix mocks

**Files:**
- Modify: `tests/WinformsMVP.Samples.Tests/Presenters/WindowClosingTests.cs`
- Modify: `tests/WinformsMVP.Samples.Tests/Mocks/MockComposeEmailView.cs`, `MockMainEmailView.cs`, `MockToDoView.cs` (drop `Closing`/`OnClosing`)
- Modify: `tests/WinformsMVP.Samples.Tests/Presenters/ComposeEmailPresenterTests.cs` (drive via `CanCloseGate` / sink)

- [ ] **Step 1: Rewrite `WindowClosingTests.cs`**

The old file tests the event model (View.Closing multi-subscriber, `WindowClosingEventArgs`, `CloseRequested` event). Most of that is gone. Replace the file with tests of the new contract against a fake `WindowPresenterBase` presenter, reusing the `CanCloseTests` patterns. Cover: Pull allow/block by reason, the async-callback `CanClose(reason, proceed)` override path, Push via injected sink (typed + untyped `RequestClose`). Delete `WindowClosingEventArgs`/`CloseRequested`/multi-subscriber tests (those concepts no longer exist). Keep the file as the Presenter-level contract suite (controller-level mechanics live in `WindowCloseControllerTests`).

> Concrete async-override test to include:
> ```csharp
> private sealed class AsyncPresenter : WindowPresenterBase<IFakeView>
> {
>     public Action<bool> Pending;
>     protected override void OnViewAttached() { }
>     protected override void CanClose(CloseReason reason, Action<bool> proceed) => Pending = proceed;
> }
> [Fact]
> public void AsyncCanClose_DefersUntilProceed()
> {
>     var p = new AsyncPresenter(); p.AttachView(new FakeView()); p.Initialize();
>     bool? allow = null;
>     ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);
>     Assert.Null(allow);          // not answered yet
>     p.Pending(true);
>     Assert.True(allow);
> }
> ```

- [ ] **Step 2: Fix the mock views**

In each mock under `tests/.../Mocks/` implementing `IWindowView`, delete the `Closing` event + `OnClosing` (and any `RaiseClosing` test helper). Keep the real contract members. Update any test that called `mock.RaiseClosing(...)` to drive `((ICloseParticipant)presenter).CanCloseGate(...)` instead.

- [ ] **Step 3: Run the full presenter test suite**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~Presenters`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/WinformsMVP.Samples.Tests/
git commit -m "test(closing): rewrite closing tests for CanClose/RequestClose; fix mocks"
```

---

### Task 14: Update `WindowNavigatorTests.cs`

**Files:**
- Modify: `tests/WinformsMVP.Samples.Tests/Services/WindowNavigatorTests.cs`

- [ ] **Step 1: Read and update**

Read the file. Any test presenter/fake implementing `IRequestClose<T>` via a `CloseRequested` event must switch to the marker + `this.RequestClose(...)`. Any fake view with `Closing`/`OnClosing` loses them. Tests asserting modal result on Push should still pass (the controller converges the pushed result). Tests asserting "user X => Cancel" still hold. If a test reached into `WindowCloseCoordinator`, delete it (covered now by `WindowCloseControllerTests`).

- [ ] **Step 2: Run**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter FullyQualifiedName~WindowNavigatorTests`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/WinformsMVP.Samples.Tests/Services/WindowNavigatorTests.cs
git commit -m "test(closing): update WindowNavigator tests for the controller model"
```

---

## Phase 7 — Verify the whole build + net40

### Task 15: Full solution build, full test run, net40 smoke

**Files:** none (verification only)

- [ ] **Step 1: Clean build the solution**

Run: `dotnet build winforms-mvp.sln -c Release`
Expected: PASS, zero errors, no warnings about the removed types.

- [ ] **Step 2: Confirm net40 still compiles the core (no `Task`/`async` crept in)**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj -c Release` (multi-targets `net40;net48`)
Expected: PASS for both TFMs. If net40 fails, a `Task`/`async`/`Task.FromResult` or a `>= 4.5` API leaked into the closing path — replace with the `Action<bool>` callback form.

- [ ] **Step 3: Run the net40 smoke test**

Run: `dotnet run --project tests/WinformsMVP.Net40SmokeTest/WinformsMVP.Net40SmokeTest.csproj`
(Read `tests/WinformsMVP.Net40SmokeTest/Program.cs` first; if it exercised the old closing API, update it to the new `CanClose`/`RequestClose` surface.)
Expected: runs clean.

- [ ] **Step 4: Run the entire test suite**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: ALL PASS.

- [ ] **Step 5: Commit any net40 smoke fixes**

```bash
git add tests/WinformsMVP.Net40SmokeTest/
git commit -m "test(closing): update net40 smoke test for new closing API"
```

---

## Phase 8 — Documentation

### Task 16: Update `CLAUDE.md` closing section

**Files:**
- Modify: `CLAUDE.md` (the "Window closing — two-direction model" section)

- [ ] **Step 1: Rewrite the closing section**

Replace the two-direction table + the "Every Form needs the explicit-interface close boilerplate" block with the new model: Pull = `protected override bool CanClose(CloseReason reason)` (and the async `CanClose(reason, Action<bool> proceed)` overload), Push = `this.RequestClose(result, status)` via the `IRequestClose<TResult>` marker (or base `RequestClose(status)` for no result). State that Forms write **zero** closing code, that Managed (navigator) and Adopted (`presenter.Connect(form)`) share one `WindowCloseController`, and that the single-owner rule applies. Update the **Testing rule** snippet: replace `view.RaiseClosing(CloseReason.Normal)` and `presenter.CloseRequested += ...` with `((ICloseParticipant)presenter).CanCloseGate(reason, ok => ...)` and a bound `RecordingSink`.

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs(closing): update CLAUDE.md for CanClose/RequestClose model"
```

---

### Task 17: Update the wiki + remaining doc references

**Files:**
- Modify: `wiki/Concept-Window-Closing-Model.md` (core rewrite)
- Modify: `wiki/HowTo-Handle-Window-Closing.md` (core rewrite)
- Modify: `wiki/Reference-Presenter-Base-Classes.md` (add `CanClose`/`RequestClose`/`Connect`, `WindowPresenterBaseCore`)
- Modify: `wiki/Reference-WindowNavigator.md` (closing behavior, Adopted vs Managed)
- Modify: `wiki/Design-Rules.md` (any rule referencing `View.Closing`)
- Modify: `README.md`, `CHANGELOG.md`, `wiki/Glossary.md`, `wiki/_Sidebar.md`, `wiki/Home.md`, `wiki/FAQ.md`, `wiki/Troubleshooting.md`, `wiki/Tutorial-Building-Your-First-App.md`, `wiki/Concept-MVP-Pattern.md`, `wiki/Concept-Architecture-Overview.md`, `wiki/HowTo-Test-A-Presenter.md`, `wiki/HowTo-Communicate-Between-Presenters.md`, `wiki/Reference-ChangeTracker.md` — replace stale `Closing`/`OnClosing`/`CloseRequested` mentions with the new API.

- [ ] **Step 1: Rewrite the two primary closing pages**

Rewrite `Concept-Window-Closing-Model.md` and `HowTo-Handle-Window-Closing.md` around: Pull (`CanClose`), Push (`RequestClose`), async callback, Managed vs Adopted (`Connect`), single-owner rule, composite-window aggregation (C1), shutdown bypass. Base the prose on the two source documents in `~/Downloads` (`窗口关闭模型设计.md` + the review-response), reflecting the final decisions in this plan (marker `IRequestClose`, non-generic sink, `WindowPresenterBaseCore`, close-before-show).

- [ ] **Step 2: Sweep the remaining files**

For each remaining file, read the closing-related passage and update it. Most are one-liners (a `View.Closing` mention, a boilerplate snippet). Add a `CHANGELOG.md` entry under a new section documenting the breaking change (removed `IWindowView.Closing/OnClosing`, `WindowClosingEventArgs`, `CloseRequestedEventArgs`, `WindowClosingBridge`, `WindowCloseCoordinator`, `IRequestClose<T>.CloseRequested` event; added `CanClose`, `RequestClose`, `Connect`).

- [ ] **Step 3: Verify no stale references remain**

Run (Grep tool): patterns `OnClosing`, `WindowClosingEventArgs`, `CloseRequested`, `WindowClosingBridge`, `WindowCloseCoordinator`, `\.Closing \+=` across the repo. Expected: only historical mentions in `CHANGELOG.md`. Everything else updated.

- [ ] **Step 4: Commit**

```bash
git add wiki/ README.md CHANGELOG.md
git commit -m "docs(closing): rewrite wiki + docs for the redesigned closing model"
```

---

## Self-review checklist (run before handing off the branch)

- [ ] `dotnet build winforms-mvp.sln -c Release` clean (net40 + net48).
- [ ] `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj` all green.
- [ ] net40 smoke runs.
- [ ] Grep shows no live references to deleted types (only `CHANGELOG.md` history).
- [ ] No `Task`/`async`/`await` in the closing path (`WindowPresenterBaseCore`, `WindowCloseController`).
- [ ] `IRequestClose<T>` presenters compile and push results; Pull `CanClose` blocks/allow per reason.
- [ ] `presenter.Connect(form)` attaches+inits idempotently; double-`Connect`/navigator+`Connect` not exercised in samples.
