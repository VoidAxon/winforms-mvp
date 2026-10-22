# AttachChild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A parent presenter composes embedded control presenters (fixed and dynamic panels) with `protected IDisposable AttachChild(...)`, sharing one hardened lifetime model with `Connect`.

**Architecture:** Core (`PresenterBase`) gets a child registry, preflight, service inheritance and ordered teardown; the dispatcher can be deactivated and binders unbound; an internal `IViewDisposalObserver` (WinForms implementation discovered by reflection like `WinFormsPlatformServices`) releases a child or a connected presenter when its control is disposed.

**Tech Stack:** C# latest, netstandard2.0 (Core), net472/net10.0-windows (WinformsMVP), xUnit 2.9.3 (net48 CI; Core-only tests on the local net10 harness).

**Spec:** `docs/superpowers/specs/2026-10-22-attach-child-design.md`

## Global Constraints

- Core never references WinForms / System.Drawing. New public surface: only the two `protected IDisposable AttachChild` overloads on `PresenterBase<TView>` and `void Unbind()` on `IViewActionBinder`.
- No backward compatibility (owner): `IViewActionBinder.Unbind()` is a breaking addition.
- Teardown order (child release): released+unlinked → dispatcher inactive → disposal observer unsubscribed → `Lifetime` cancelled → binder unbound → `Cleanup()` → own children reverse → `Disposables`. Parent: dispatcher inactive (self and descendants) → `Lifetime` cancelled → binder unbound → `Cleanup()` → remaining children reverse → `Disposables`. Every stage runs even if one throws; failures rethrown together (single → as is; several → `AggregateException`).
- Views are never disposed by the framework.
- UI-thread only; no locks around user callbacks.
- Commit trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; `codex review --base master` after the implementation (NODENV_VERSION=24.21.0).
- Local test command: `dotnet test <scratchpad>/testingpkg` (extend its csproj `Compile Include` with the new Core-only test files); full suite on CI.

## Review Focus

1. A click on a removed panel (retained control) must not run the child's handler, sync or async.
2. A child whose `OnInitialize` throws must leave the parent usable, the child disposed, and no registry entry.
3. A parent hosted with `PresenterTestHost.Start(services, …)` must give unbound children the same services (no `ServiceLocator.Current` fallback); a factory-bound child keeps its own.
4. Disposing a handle inside the parent's `Cleanup` (or during parent teardown) must not dispose the child twice or throw.
5. `Connect` on a control disposed during the presenter's initialization must dispose the presenter.

---

### Task 1: Dispatcher deactivation

**Files:** `src/WinformsMVP.Core/MVP/ViewActions/ViewActionDispatcher.cs`; test `tests/WinformsMVP.Samples.Tests/ViewActions/DispatcherDeactivationTests.cs`.

**Produces:** `internal void Deactivate()`; `internal bool IsActive`. Inactive: `CanDispatch` → false; `Dispatch` no-op; `DispatchAsync` → `Task.CompletedTask`; checked first in `DispatchCore` (before key lookup / strict-mode errors / middleware / payload checks); every `CanExecuteChanged?.Invoke` goes through a private `NotifyCanExecuteChanged()` that is silent when inactive (also for completions of in-flight async handlers). `Register` after deactivation is allowed but inert.

- [ ] Tests: `Deactivated_CanDispatchFalse_DispatchDoesNothing`; `Deactivated_StrictMode_UnknownKeyDoesNotThrow`; `Deactivated_DispatchAsyncCompletes_WithoutRunningHandler`; `Deactivated_InFlightAsyncCompletion_RaisesNoCanExecuteChanged`; `Deactivated_MiddlewareNotInvoked`. (Use `InternalsVisibleTo` — test assembly already has it.)
- [ ] Harness FAIL → implement → PASS. Commit `fix(core): a deactivated dispatcher runs nothing`.

### Task 2: Binders unbind; presenters deactivate on dispose

**Files:** `IViewActionBinder.cs` (+`void Unbind()` with XML doc: stop dispatching and refreshing; controls/view untouched; idempotent); `src/WinformsMVP/MVP/ViewActions/ViewActionBinder.cs` (split private `UnbindControls()` used by internal rebinding; public `Unbind()` = controls + dispatcher event subscriptions + `_dispatcher = null`); `src/WinformsMVP.Testing/CapturingActionBinder.cs` (`Unbind()` clears the captured dispatcher; after unbind `Dispatch`/`DispatchAsync` are no-ops and `CanDispatch` false — not the "before bind" exception; track `IsBound`); `PresenterBase.Dispose` (deactivate dispatcher before cancelling `Lifetime`; unbind `View.ActionBinder` when the framework bound it — remember the bound binder instance in `InitializeCore`).

- [ ] Tests (Core/harness): `Dispose_DeactivatesDispatcher_LateDispatchThroughBinderDoesNothing`; `Dispose_UnbindsTheBinderItBound`; `CapturingActionBinder_AfterUnbind_IsInert`; `ExplicitModeView_NullBinder_DisposeIsFine`. (CI) `ViewActionBinder_Unbind_StopsDispatchingAndClearsDispatcher`, `ViewActionBinder_Rebind_StillWorks`.
- [ ] Fix any existing test that dispatched after dispose expecting a run (grep `Dispose();` followed by `Dispatch`).
- [ ] FAIL → implement → PASS; `dotnet build winforms-mvp.sln` 0 errors. Commit `fix!: disposed presenters stop dispatching; IViewActionBinder.Unbind`.

### Task 3: View disposal observer; `Connect` observes before initialization

**Files:** Core `src/WinformsMVP.Core/MVP/Presenters/ViewDisposal.cs` (internal `IViewDisposalObserver { IDisposable ObserveDisposal(IViewBase view, Action onDisposed); bool IsDisposed(IViewBase view); }` + internal static `ViewDisposal.Observer` resolved once by reflection of `"WinformsMVP.Services.Implementations.ControlDisposalObserver, WinformsMVP"`'s static `Instance`, falling back to a no-op observer); WinForms `src/WinformsMVP/Services/Implementations/ControlDisposalObserver.cs` (`Control.Disposed`; `IsDisposed` = `IsDisposed || Disposing`); `ControlLifecycleController` / `ControlPresenterConnectExtensions` (subscribe before attach/initialize; already-disposed control → `ObjectDisposedException` before any side effect; disposal during init → presenter disposed, exception from `Connect`; presenter disposed first → subscription removed).

- [ ] Tests (CI, WinForms): `Connect_DisposedControl_Throws_NoSideEffects`; `Connect_ControlDisposedDuringInitialize_DisposesPresenter`; `Connect_PresenterDisposedFirst_RemovesDisposedHandler`. (Harness) `NoWinForms_ObserverIsNoOp`.
- [ ] FAIL → implement → PASS (harness) + build. Commit `fix: Connect observes control disposal from the start`.

### Task 4: AttachChild — fixed panels

**Files:** `PresenterBase.cs` (+ new partial or helper `ChildAttachment` internal sealed class); `IPresenterServicesBinding.cs` (+ `bool HasExplicitServices`, `IServiceProvider ResolvedServices` or equivalent internal members); test `tests/WinformsMVP.Samples.Tests/Presenters/AttachChildTests.cs`.

**Produces:**
```csharp
protected IDisposable AttachChild<TChildView>(ControlPresenterBase<TChildView> child, TChildView view) where TChildView : IViewBase;
protected IDisposable AttachChild<TChildView, TParam>(ControlPresenterBase<TChildView, TParam> child, TChildView view, TParam parameters) where TChildView : IViewBase;
```
Preflight (no side effects on failure): null → `ArgumentNullException`; parent disposing/disposed → `ObjectDisposedException`; parent has no view → `InvalidOperationException`; child disposed → `ObjectDisposedException`; child attached/initialized/owned → `InvalidOperationException`; view already used by a live child of this parent → `InvalidOperationException`; `ViewDisposal.Observer.IsDisposed(view)` → `ObjectDisposedException`. Services: explicitly bound child keeps them; otherwise if the child lazily resolved a provider that is not the parent's `Services` → `InvalidOperationException` (same message style as `BindServices`); otherwise bind the parent's `Services`. Then record ownership (registry entry, child's internal `_owner` link) → observe disposal → attach view → initialize (parameters) → return handle. Synchronous failure: remove entry, dispose child (collect cleanup failures), rethrow original (`AggregateException` when cleanup failed too). Parent `Dispose`: deactivate dispatchers of self and all live descendants first, then the order in Global Constraints.

- [ ] Tests (harness): `Fixed_ChildInitializedWithParameters_ActionsRunThroughItsView`; `Child_InheritsParentServices_FromPresenterTestHost`; `Child_WithExplicitServices_KeepsThem`; `Child_ResolvedOtherServicesInCtor_Rejected`; preflight cases (one test each: null child, null view, parent without view, parent disposed, child already attached, same view twice, window presenter does not compile — skip, compile-time); `ChildInitializeThrows_ChildDisposed_NoEntry_ParentUsable`; `SecondChildFails_DuringParentInitialize_HostDisposesParentAndFirstChild`; `ParentDispose_Order_CleanupThenChildrenReverseThenDisposables`; `ParentDispose_ChildCleanupThrows_OtherChildrenStillDisposed_FailureRethrown`; `ParentDisposing_ChildDispatchIsInertBeforeChildCleanup`.
- [ ] FAIL → implement → PASS. Commit `feat(core): AttachChild for presenters that own embedded control presenters`.

### Task 5: Handles, dynamic panels, safety net

**Files:** same as Task 4 (+ handle class), tests `AttachChildDynamicTests.cs` (harness) and `AttachChildControlTests.cs` (CI, WinForms).

**Produces:** handle `Dispose()`: first call releases (order in Global Constraints), unlinks, clears references; later calls / already released by parent → no-op; during parent teardown → releases if alive. `child.Dispose()` called directly → unlinks its entry. Released child cannot be attached again (`ObjectDisposedException`). Observer callback (control disposed while attached) → same release path; debug log when parent still active; never touches model/view.

- [ ] Tests (harness): `Handle_ReleasesChild_LateDispatchInert_SyncAndAsync`; `Handle_DisposeTwice_NoOp`; `Handle_DisposedInParentCleanup_ChildDisposedOnce`; `ChildDisposedDirectly_EntryRemoved_ParentDisposeDoesNotTouchIt`; `ReleasedChild_CannotBeReattached`; `ManyAddRemove_RegistryHoldsOnlyLiveChildren`; `Dynamic_EndToEnd_AddDispatchRemove_WithFakeViews` (fake parent view with `AddLine()` / `RemoveLine()`); `ReleaseCancelsChildLifetime`. (CI) `ControlDisposedWhileAttached_ReleasesChild`; `ControlDisposedDuringChildInitialize_AttachFails_ChildDisposed`.
- [ ] FAIL → implement → PASS. Commit `feat(core): AttachChild handles for dynamic panels; release on control disposal`.

### Task 6: Docs

**Files:** `wiki/HowTo-Implement-Master-Detail.md` (ownership rule; fixed + dynamic recipes; keys; removal order and compensation; factory registration as migration note), `wiki/HowTo-Communicate-Between-Presenters.md` (link to AttachChild in the per-window shared model section — after PR #33 merges; rebase), `wiki/Reference-Presenter-Base-Classes.md` (AttachChild, lifecycle, teardown order), `wiki/HowTo-Test-A-Presenter.md` (parent tests drive children through fake child views), `wiki/Reference-ViewAction-System.md` (inactive dispatcher, `Unbind`), `CLAUDE.md`, `FEATURES.md`, `CHANGELOG.md` (Added AttachChild; BREAKING `IViewActionBinder.Unbind`; Fixed late dispatch after dispose; Fixed `Connect` disposal during initialization).

- [ ] Edit; `tests/templates/verify-templates.sh` → `== All templates OK`; Release build 0 errors. Commit `docs: AttachChild`.

### Task 7 (local sample, never pushed)

Edit screen: remove the overriding view-mapping registration and the `Func<…Presenter>` constructor parameters; parent view interface exposes the two panel view interfaces; the parent presenter takes the panels' dependencies and calls `AttachChild` in `OnInitialize`; add a parent test that dispatches a panel action through the parent host. `dotnet test tests/ConsultationSample.Tests` → all pass; commit on `impl`, merge to `main` / `mvvm`.
