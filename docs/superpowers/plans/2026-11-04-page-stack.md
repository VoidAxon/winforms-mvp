# Page Stack Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In-window drill-down navigation (breadcrumb) for Kibinago: `PageStack` + `IPageCloseGuard`, integrated with the
window close gate, with tests, sample, and docs.

**Architecture:** Two public types in `Kibinago.Core` (`Kibinago.Presenters`). `PageStack` reaches the owner's
`AttachChild` machinery through internal seams on `PresenterBase`; one internal check policy (`PageCloseCheck`)
walks stacks recursively for jumps, `CanCloseAllAsync` and window close, with reservations owned by the operation.
The window gate (`WindowPresenterBaseCore`) runs the page check before `CanCloseAsync(CloseReason)` and keeps the
reservation until the host settles the close attempt (`WindowLifecycleController`, `PresenterTestHost`).

**Tech Stack:** C# (LangVersion as in `Directory.Build.props`), netstandard2.0 (Core / Testing), net472 + net10.0-windows
(Kibinago), xUnit 2.9.3 tests on net48.

**Spec:** `docs/superpowers/specs/2026-11-04-page-stack-design.md` — read it first; this plan argues from it and does
not repeat its behaviour rules. Section names below ("Check policy", "Push", …) refer to the spec.

## Global Constraints

- Public API = exactly two new types: `IPageCloseGuard` (one member `Task<bool> CanCloseAsync()`) and `PageStack`
  (`PageStack(IPresenter owner)`, `Count`, `IsBusy`, `Push<TView>(…)`, `Push<TView,TParam>(…)`,
  `PopToCountAsync(int remainingCount)`, `PopAsync()`, `CanCloseAllAsync()`). Nothing else public, **no new member
  (public or protected) on `PresenterBase`, `ControlPresenterBase`, `WindowPresenterBase*`**.
- Namespace `Kibinago.Presenters`; no new namespace (`ApiSurfaceTests` pins the list).
- `Kibinago.Core` must not reference WinForms / System.Drawing; no new package dependency.
- Every public member has XML docs (`<param>`, valid `cref`) — missing docs are build errors in `src/`.
- No version in any `.csproj`.
- Single-threaded (UI thread) by contract; never capture `SynchronizationContext` in a constructor.
- Window presenters keep `CanCloseAsync(CloseReason)`; `IPageCloseGuard` XML docs say it is for pages.
- Wiki / CHANGELOG / sample UI text in Japanese (as existing pages); spec / plan / code comments in English.
- Never run anything on the Windows host. Linux: `dotnet build Kibinago.sln` and the local net10 Core harness
  (Task 0). The net48 suite runs on GitHub Actions only (push the branch / PR — ask the owner before pushing).
- The AntdUI trial (Task 11) stays local: never pushed, never sent to codex / artifacts.

## Review Focus

1. A page's guard shows a modal `Messages.ConfirmYesNo` and, while it is open, a posted breadcrumb click / ✕ arrives
   → the second jump returns `false`, the ✕ is blocked without a second prompt; nothing removed twice. (Task 3, 5)
2. A page's `Saved` callback (called from inside its own action) pushes or pops on the parent's stack while no check
   runs → allowed and works; while a check runs → rejected without corrupting `Count`. (Task 2, 3)
3. The user disposes the whole window while an async guard is pending → the pending jump answers `false`, no
   `removeView` after disposal, no exception escapes to `Application.ThreadException`. (Task 3)
4. A view's `removeView` throws for one page during a 3-page jump → the other two are still disposed and removed,
   `Count` is correct, the exception surfaces once. (Task 3)
5. An approved async window close is abandoned by another `FormClosing` handler → breadcrumb navigation works again
   afterwards (reservation released). (Task 6)

---

### Task 0: Local net10 harness for Core tests (not committed)

**Files:**
- Create: `<scratchpad>/coretests/CoreTests.csproj` (scratchpad of the executing session; not in the repo)

- [ ] **Step 1:** Create an SDK-style xUnit test project targeting `net10.0`, `AssemblyName` = `Kibinago.Samples.Tests`
  (so `InternalsVisibleTo` applies), `ProjectReference` to `src/Kibinago.Core/Kibinago.Core.csproj` and
  `src/Kibinago.Testing/Kibinago.Testing.csproj`, xUnit 2.9.3, and `<Compile Include=".../tests/Kibinago.Samples.Tests/Composition/PageStack*.cs" />`
  plus the fixtures those files use (`Composition/AttachChildFixtures.cs` only if needed).
- [ ] **Step 2:** `dotnet test <scratchpad>/coretests/CoreTests.csproj` → builds (0 tests is fine before Task 1).

The harness can only run tests that need no WinForms; everything in Tasks 1–5 is written that way (fake views are
plain objects). Tasks 6 and 8 rely on CI.

---

### Task 1: Internal seams in `PresenterBase` / `ChildAttachment` + fix the post-transfer gap

**Files:**
- Modify: `src/Kibinago.Core/Presenters/PresenterBase.cs` (`AttachChildCore` ~628–690, `Dispose` ~503–545, child region ~556)
- Modify: `src/Kibinago.Core/Presenters/ChildAttachment.cs`
- Test: `tests/Kibinago.Samples.Tests/Composition/AttachChildTests.cs` (add cases)

**Interfaces:**
- Produces (all `internal`):
  - `enum ChildReleaseKind { Explicit, External, OwnerTeardown }` in `ChildAttachment.cs`.
  - `ChildAttachment.Released` — `Action<ChildAttachment, ChildReleaseKind>` invoked once from `Unlink()` **after**
    bookkeeping, **before** the child's own disposal continues. `Explicit` = `ChildAttachment.Dispose()` called (handle);
    `OwnerTeardown` = the owner's `Dispose` loop; `External` = the child was disposed directly or its control was
    disposed (the `ViewDisposal` observation).
  - On `IComposablePresenter`: `IReadOnlyList<ChildAttachment> Children { get; }` (snapshot) and
    `List<PageStack> OwnedStacks { get; }` (lazily created, registration order; used by Task 2/3).
  - `PresenterBase<TView>.AttachChildInternal<TChildView>(PresenterBase<TChildView> child, TChildView view, Action initialize, Action<ChildAttachment> beforeInitialize)`
    → `ChildAttachment`: the existing `AttachChildCore` body, exposed `internal` (the two protected `AttachChild`
    overloads call it). `beforeInitialize` runs right after ownership transfer (Task 2 uses it to record the entry
    and set `Released`), so every post-transfer failure is inside the `try`.
  - `PageStack` validates its owner by casting `IPresenter` to `IComposablePresenter` (every framework presenter
    implements it); no extra helper.

- [ ] **Step 1: Failing tests** in `AttachChildTests.cs`:
  - `PostTransferFailure_InObservation_DisposesChildAndRethrows` — use a test `ViewDisposal.Observer` substitute (or a
    view whose disposal observation throws, whichever the existing `ViewDisposalTests` pattern allows) so that
    `ObserveDisposal` throws; assert the child is disposed, the owner has no child, the exception propagates.
  - `Release_ReportsKind` — handle `Dispose()` → `Explicit`; `child.Dispose()` directly → `External`; owner disposed →
    `OwnerTeardown` (assert via the internal `Released` callback set from the test, which has internals access).
- [ ] **Step 2:** Run in the harness / build → fail (members missing).
- [ ] **Step 3:** Move `ObserveDisposal`, `OwnUntilDisposed`, `ViewOwnership.Claim` and the new `beforeInitialize`
  call inside the existing `try` of `AttachChildCore`; add `Released` + kinds (the owner's `Dispose` loop calls
  `Unlink(ChildReleaseKind.OwnerTeardown)`, `ChildAttachment.Dispose` → `Explicit`, `PresenterBase.Dispose`'s
  `_ownerLink?.Unlink()` and the view-disposal callback → `External`); add `Children` / `OwnedStacks`.
- [ ] **Step 4:** Tests pass; `dotnet build Kibinago.sln` clean.
- [ ] **Step 5: Commit** `fix(presenters): AttachChild covers every post-transfer failure; internal release kinds`

---

### Task 2: `IPageCloseGuard` + `PageStack` construction and `Push`

**Files:**
- Create: `src/Kibinago.Core/Presenters/IPageCloseGuard.cs`
- Create: `src/Kibinago.Core/Presenters/PageStack.cs`
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackFixtures.cs` (fake views, test pages)
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackPushTests.cs`
- Modify: `tests/Kibinago.Samples.Tests/ApiSurfaceTests.cs`

**Interfaces:**
- Consumes: Task 1 internals.
- Produces:
  - `public interface IPageCloseGuard { Task<bool> CanCloseAsync(); }`
  - `public sealed class PageStack` with the public members in Global Constraints.
  - Internal: `sealed class PageEntry { IComposablePresenter Page; object View; Action RemoveView; ChildAttachment Handle; }`,
    `PageStack.Entries` (`IReadOnlyList<PageEntry>`), `PageStack.Owner` (`IComposablePresenter`),
    `PageStack.Reservation` (`PageCloseCheck` or null; Task 3), `PageStack.IsDead`.
- Fixtures (`PageStackFixtures.cs`): `IPageView : IControlView` + `FakePageView` (plain object, a `Tag` string);
  external release in tests is triggered by disposing the page presenter directly (`ChildReleaseKind.External`);
  `GuardedPage : ControlPresenterBase<IPageView>, IPageCloseGuard` whose answer is a settable
  `Func<Task<bool>>`, recording `AskCount` and appending its tag to a shared `List<string> log` on ask / dispose;
  `PlainPage` (no guard); `IWorkspaceView : IWindowView` + `FakeWorkspaceView` recording `Removed` views in order;
  `WorkspacePresenter : WindowPresenterBase<IWorkspaceView>` exposing nothing public except via its `Stack` field
  being reachable from the test through a constructor callback (`Action<PageStack> exposeStack`) — tests drive it via
  `PresenterTestHost.Start(services, presenter, view)` and the exposed stack.

- [ ] **Step 1: Failing tests** (`PageStackPushTests`):
  - `Ctor_Rejects`: null → `ArgumentNullException`; a non-framework `IPresenter` stub → `ArgumentException`;
    disposed owner → `ObjectDisposedException`.
  - `Push_AttachesInitializesAndCounts`: two pushes → `Count == 2`, pages initialized, use owner's services.
  - `Push_BeforeOwnerView_Throws_InvalidOperation_AndCallsNoRemove`.
  - `Push_Preflight_ViewClaimedByAnotherPresenter_DoesNotRemoveOrDispose`: the other presenter stays alive,
    `removeView` not called.
  - `Push_FailureAfterAcceptance_RemovesViewOnce`: page `OnInitialize` throws → exception propagates, `removeView`
    called exactly once, `Count` unchanged, page disposed.
  - `Push_RemoveViewThrowsAfterFailure_Aggregates`.
  - `Push_ReentrantFromPageOnInitialize_Throws`: a page whose `OnInitialize` pushes on the same stack → inner `Push`
    throws `InvalidOperationException`; outer push completes (Review Focus 2).
  - `Push_FromSavedCallbackWhileIdle_Works` (Review Focus 2).
  - `OwnerDisposed_DisposesPagesWithoutRemoveView_StackDead`: then `Push` → `ObjectDisposedException`,
    `PopAsync()` → `false`.
  - `ApiSurfaceTests.PageStackSurface`: `PageStack` sealed, public members exactly the list; `IPageCloseGuard` has
    one method; no new public/protected members on `PresenterBase<IView>`, `ControlPresenterBase<IControlView>`,
    `WindowPresenterBase<IWindowView>` (compare against a hard-coded member-name list taken before the change).
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Implement. `Push` order: dead / busy / owner-view checks → argument & ownership preflight (reuse
  the checks `AttachChildInternal` does before transfer; a preflight exception means no `removeView`) → reserve the
  stack for the duration of `Push` (reentrancy) → `AttachChildInternal(..., beforeInitialize: a => record entry,
  a.Released = OnReleased)` → on post-acceptance failure remove the entry, call `removeView` once, aggregate.
  `Push<TView,TParam>` initializes through `IInitializable<TParam>`. Register with `Owner.OwnedStacks` in the ctor.
  XML docs on everything (wording from the spec's API block).
- [ ] **Step 4:** Tests pass (harness), solution builds.
- [ ] **Step 5: Commit** `feat(presenters): PageStack.Push and IPageCloseGuard`

---

### Task 3: Check policy, `PopToCountAsync` / `PopAsync` / `CanCloseAllAsync`

**Files:**
- Create: `src/Kibinago.Core/Presenters/PageCloseCheck.cs` (internal)
- Modify: `src/Kibinago.Core/Presenters/PageStack.cs`
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackCheckTests.cs`
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackPopTests.cs`

**Interfaces:**
- Consumes: Task 2 internals.
- Produces (internal):
  - `sealed class PageCloseCheck` — one per operation. `static PageCloseCheck Current` is **not** used (no ambient
    static); instead the check is found through the stacks it reserved: `PageStack.Reservation`.
  - `static bool TryReserve(IEnumerable<PageStack> stacks, PageCloseCheck check)` — all-or-nothing; enrols.
  - `Task<bool> AskAsync(IReadOnlyList<PageEntry> entriesTopFirst)` — the recursive traversal (spec "Check policy"):
    for each entry: the stacks of the page's presenter tree (via `IComposablePresenter.Children` recursion +
    `OwnedStacks`, sibling stacks reverse registration order, each stack top first), then the page's guard. Results
    memoized per presenter and per stack in the check. A guard whose call re-enters the same check through
    `CanCloseAllAsync()` on a stack reserved by this check reuses / runs within it; awaiting a presenter that is still
    being asked → `false` (cycle).
  - `void Enrol(PageStack stack)` — used by `PageStack` ctor and `Push` when the owner tree is reserved (born / attached
    into a reserved tree).
  - `void Release()` — idempotent; clears `Reservation` on every enrolled stack.
  - `static IEnumerable<PageStack> StacksOf(IComposablePresenter root)` — every stack in the tree, used by Task 5.
- Public: `PopToCountAsync`, `PopAsync`, `CanCloseAllAsync` per the spec ("PopToCountAsync…", "CanCloseAllAsync").

- [ ] **Step 1: Failing tests** (`PageStackCheckTests`):
  - `TopFirst_NestedFirst_Order` — stack [A, B(owns inner stack [B1, B2]), C]: `PopToCountAsync(0)` asks
    `C, B2, B1, B` then `A` (log order), each once.
  - `NestedStacksChecked_ForPageWithoutGuard` — `PlainPage` owning a stack with a refusing page → pop refused.
  - `ForwardingGuard_JoinsCheck_NoFalseBusy` — a panel page forwarding to its own stack's `CanCloseAllAsync()` →
    whole tree approves; each guard asked once.
  - `Cycle_RefusesWithoutHanging` — a guard that awaits `CanCloseAllAsync()` of a stack whose check is waiting for
    that guard → `false` within the test timeout.
  - `FaultedCancelledNullOrThrowingGuard_Refuses_AndLogs` (four cases, `RecordingLoggerFactory`).
  - `Refusal_RemovesNothing_AskedAgainNextTime`.
  - `OverlappingOperation_WhileAsyncGuardPending` (`TaskCompletionSource`): second `PopToCountAsync` → `false`, no
    extra ask; `Push` → `InvalidOperationException`; `CanCloseAllAsync()` from outside → `false` (Review Focus 1).
  - `OwnerDisposedWhilePending_ReturnsFalse_NoRemoveView` (Review Focus 3).
  - `StackCreatedOrAttachedDuringCheck_IsEnrolled` — a guard that pushes a page owning a new stack into a sibling
    tree → that stack is busy until the operation ends.
- [ ] **Step 2: Failing tests** (`PageStackPopTests`):
  - `PopTo_Bounds`: `-1` / `Count+1` → `ArgumentOutOfRangeException`; `Count` → `true`, nothing asked.
  - `PopAsync_Empty_ReturnsTrue`; `Busy_Or_Dead_BeatsNoOp` (`PopToCountAsync(Count)` while busy → `false`).
  - `Pop_DisposesThenRemoves_TopFirst` (log: `dispose C, remove C, dispose B, remove B`).
  - `Pop_RemovalFailures_ContinueAndAggregate` — `removeView` of the middle page throws and the top page's
    `Cleanup` throws → all three disposed / removed, `Count` correct, `AggregateException` with both
    (Review Focus 4); single failure → rethrown as is.
  - `CanCloseAllAsync_AsksAll_RemovesNothing`.
  - `Breadcrumb_ViaTypedViewAction` — the workspace registers `ViewAction<int>` → `PopToCountAsync(level+1)`;
    `await view.Binder.DispatchAsync(key, 0)` with a refusing top page leaves `Count` unchanged.
- [ ] **Step 3:** Run → fail.
- [ ] **Step 4:** Implement `PageCloseCheck` and the three public methods. Removal: mark entries as being removed
  and drop them from `Entries` **before** calling `handle.Dispose()` (so `Released(Explicit)` is ignored), then
  `removeView`; collect disposal and removal exceptions separately. Release the reservation in the operation's
  `finally` before returning.
- [ ] **Step 5:** Tests pass; build clean.
- [ ] **Step 6: Commit** `feat(presenters): PageStack jumps and CanCloseAllAsync through one check policy`

---

### Task 4: External disposal reconciliation

**Files:**
- Modify: `src/Kibinago.Core/Presenters/PageStack.cs` (`OnReleased`)
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackExternalReleaseTests.cs`

**Interfaces:**
- Consumes: `ChildAttachment.Released` + `ChildReleaseKind` (Task 1), `PageCloseCheck` (Task 3).

- [ ] **Step 1: Failing tests:**
  - `ExternalRelease_DropsEntryAndAbove_RemoveViewTopFirst_IncludingDisposed` — stack [A, B, C], dispose B's
    presenter directly → `Count == 1`; removed views in order `C, B`; C disposed; A untouched; no guard asked.
  - `ExternalRelease_DuringPendingCheck_CountsAsDiscarded` — pending guard on C, B released externally → the check
    continues with the rest, final state consistent.
  - `NormalPop_NoDoubleRemove`; `OwnerTeardown_NoRemoveView`.
  - `RemoveView_ReceivesDisposedView_IsCalledAnyway`.
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** `OnReleased(attachment, kind)`: `Explicit` while the stack is removing → ignore; `OwnerTeardown` →
  mark dead, no callbacks; `External` → bookkeeping first (drop the entry and those above), then for the entries above
  (top first) `handle.Dispose()` + `removeView`, then `removeView` for the released one; collect and rethrow failures
  to the releaser (they propagate out of the disposing call, matching `PresenterBase.Dispose`'s aggregation).
- [ ] **Step 4:** Pass; build.
- [ ] **Step 5: Commit** `feat(presenters): PageStack follows pages disposed elsewhere`

---

### Task 5: Window close gate integration (Core) + `PresenterTestHost`

**Files:**
- Modify: `src/Kibinago.Core/Presenters/WindowPresenterBaseCore.cs` (`CanCloseGate`)
- Modify: `src/Kibinago.Core/Presenters/ICloseParticipant.cs`
- Modify: `src/Kibinago.Testing/PresenterTestHost.cs` (`RunGate`, `RecordingSink`, `Dispose`)
- Create: `tests/Kibinago.Samples.Tests/Composition/PageStackWindowCloseTests.cs`

**Interfaces:**
- Consumes: `PageCloseCheck.StacksOf`, `TryReserve`, `AskAsync`, `Release` (Task 3).
- Produces (internal): `ICloseParticipant.SettleCloseAttempt(bool closed)` — the host calls it when the attempt that
  the gate approved has ended: `closed: true` when the window closed (reservation stays until disposal; the stacks
  die with the owner), `false` when the approved close was abandoned (release). A refused gate answer releases inside
  the gate itself before `proceed(false)`.

- [ ] **Step 1: Failing tests** (via `PresenterTestHost`):
  - `Close_AsksPagesRecursively_ThenWindow` — window-owned stack and a stack owned by an `AttachChild` panel
    (descendant); log order per spec; window's `CanCloseAsync(CloseReason)` last.
  - `PageRefusal_BlocksAndSkipsWindowGate`.
  - `SystemShutdown_BypassesPages` and `TaskManager_BypassesPages`.
  - `BusyStack_BlocksCloseWithoutAsking`.
  - `RequestClose_AsksPages` (recorded close only after pages agree).
  - `ReservationHeld_FromApprovalUntilHostDispose_ForRequestClose` — after an accepted `RequestClose`, a jump →
    `false`, `Push` throws, until `host.Dispose()`.
  - `CanCloseQuery_ReleasesWhenAnswered` — `await host.CanCloseAsync()` (true) then a jump works.
  - `AsyncWindowGatePending_StacksStayBusy` — window `CanCloseAsync` awaiting a `TaskCompletionSource` → jump `false`.
  - `ConnectHostedControlOwner_NotAsked` — documented boundary (a control presenter owning a stack, no window) —
    assert `CanCloseAllAsync` still works when called explicitly.
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Gate: system reasons → `proceed(true)` first (unchanged). Else build a `PageCloseCheck` over
  `StacksOf(this)`; `TryReserve` fails → `proceed(false)`. Await `AskAsync` over all stacks (sibling order per spec),
  then the existing `CanCloseAsync(reason)` path; refusal / fault → `Release()` then `proceed(false)`; approval →
  keep the check in a field, `proceed(true)`. `SettleCloseAttempt(false)` releases it; presenter disposal releases it.
  Release happens on the UI context before `proceed` is called (the existing `ContinueWith(TaskScheduler.Default)` path
  becomes an `async` local function awaited on the captured context of the gate call; with no context, inline).
  Host: `RunGate` calls `SettleCloseAttempt(false)` after delivering a `CanClose()` / `CanCloseAsync()` query answer;
  an accepted `RequestClose` keeps it (released by `Dispose`).
- [ ] **Step 4:** Pass (harness) — also run the existing `CanCloseTests`, `RequestCloseGateHostTests`,
  `PresenterTestHostTests` in the harness if they need no WinForms, otherwise rely on CI; build clean.
- [ ] **Step 5: Commit** `feat(presenters): window close asks every page and holds the stacks until it settles`

---

### Task 6: `WindowLifecycleController` settles close attempts

**Files:**
- Modify: `src/Kibinago/Services/WindowLifecycleController.cs` (`OnFormClosing` ~330–425, `OnAsyncDecision` ~431–470, `OnFormClosed`)
- Test: `tests/Kibinago.Samples.Tests/Presenters/PageStackFormCloseTests.cs` (real `Form` + `UserControl`, net48, CI)

**Interfaces:**
- Consumes: `ICloseParticipant.SettleCloseAttempt(bool closed)` (Task 5).

- [ ] **Step 1: Failing tests** (pattern of `ConnectTests` / `WindowDisposalTests`, a Form hosting page
  UserControls, presenter connected with `Connect`):
  - `ApprovedClose_Closes_PagesAskedOnce`.
  - `ApprovedThenAbandoned_ByOtherFormClosingHandler_ReleasesStacks` — a second `FormClosing` handler sets
    `e.Cancel = true` → after `Application.DoEvents()` a jump succeeds (Review Focus 5).
  - `AsyncApproval_NoNavigationBetweenApprovalAndClose` — async page guard; after it answers and before the posted
    close runs, a jump → `false`.
  - `RefusedClose_ReleasesStacks`.
- [ ] **Step 2:** Push the branch for CI only after asking the owner; otherwise mark these as CI-verified at the PR.
- [ ] **Step 3:** Implement: after a synchronous approval (`e.Cancel = false` set by the controller) and after the
  async path's `_form.Close()`, post (`BeginInvoke`) a settle check: if `_closed` (FormClosed ran) → nothing (disposal
  releases); else if modal and `DialogResult != None` → closing in progress, re-post once; else
  `SettleCloseAttempt(false)`. `OnFormClosed` → `SettleCloseAttempt(true)`.
- [ ] **Step 4:** `dotnet build Kibinago.sln` clean; CI green on the PR.
- [ ] **Step 5: Commit** `feat(winforms): settle page-stack reservations when a close is abandoned`

---

### Task 7: Sample — 顧客一覧 → 顧客詳細 → 受注編集 demo

**Files:**
- Create: `samples/Kibinago.Samples/PageNavigationDemo/` — `IPageNavigationDemoView.cs` (parent, `IControlView`,
  `PushCustomerList()` / `PushCustomerDetail(string)` / `PushOrderEdit(string)` / `RemovePage(IControlView)` /
  breadcrumb `ViewAction<int>` binder), `PageNavigationDemoPresenter.cs` (owns the `PageStack`, implements
  `IPageCloseGuard` → `CanCloseAllAsync()`), `CustomerList*`, `CustomerDetail*`, `OrderEdit*` (view interface,
  presenter, UserControl + `.Designer.cs` each), `DemoCustomerStore.cs` (in-memory data), `PageNavigationDemoActions.cs`.
- Modify: `samples/Kibinago.Samples/Shell/SampleCatalog.cs` (new embedded demo in the composition category, with
  `WikiLink("ページ移動 (パンくず)", "HowTo-Implement-Page-Navigation")`).
- Test: `tests/Kibinago.Samples.Tests/Samples/PageNavigationDemoPresenterTests.cs`

**Interfaces:**
- Consumes: public `PageStack` / `IPageCloseGuard`.
- Produces: `PageNavigationDemoPresenter : ControlPresenterBase<IPageNavigationDemoView>, IPageCloseGuard`.

- [ ] **Step 1: Failing tests:** open customer → detail → order; edit + breadcrumb to list with "No" → stays;
  "Yes" → back at list, `Count == 1`; save order → list row refreshed through `Saved` callback; `OpenCustomer`
  while busy does not create a view (sample checks `IsBusy` before `View.PushXxx`).
- [ ] **Step 2:** Implement with standard controls (`LinkLabel` row as breadcrumb; hidden pages via `Visible = false`,
  never removed from `Controls` until `RemovePage`), Japanese UI text, `[DesignerSerializationVisibility(Hidden)]` on
  view-interface properties (WFO1000), layout in `.Designer.cs`. `RemovePage` tolerates a disposed control.
- [ ] **Step 3:** Build; tests pass on CI.
- [ ] **Step 4: Commit** `feat(samples): page navigation demo with breadcrumb`

---

### Task 8: Sample shell asks demos before switching

**Files:**
- Modify: `samples/Kibinago.Samples/Shell/ShellPresenter.cs`, `IShellView.cs`, `ShellForm.cs` (category selection
  becomes a typed async ViewAction `ShellActions.SelectCategory : ViewAction<string>`; on refusal the view re-selects
  the current category: new `IShellView.SelectCategory(string)` already exists — reuse it)
- Modify: `tests/Kibinago.Samples.Tests/Shell/` (presenter tests; `ShellFormTests` if affected)

**Interfaces:**
- Consumes: `IPageCloseGuard`.
- Produces: `_attached` becomes `List<(string DemoId, IDisposable Handle, IPageCloseGuard Guard)>` (guard null when
  the demo presenter implements none); `Attacher.Attach` returns both.

- [ ] **Step 1: Failing tests:** switching category with a dirty order edit → prompt; "No" → demos kept, sidebar
  re-selected; "Yes" → demos disposed; overlapping switch while a guard is pending → ignored; window close while a
  switch is pending → blocked without prompting (stack busy); `Cleanup` still disposes unconditionally.
- [ ] **Step 2:** Implement: ask every attached guard (in reverse attach order) before disposing any; a
  `_switching` flag ignores overlapping requests.
- [ ] **Step 3:** Build; CI.
- [ ] **Step 4: Commit** `feat(samples): shell asks embedded demos before switching category`

---

### Task 9: Documentation

**Files:**
- Create: `wiki/HowTo-Implement-Page-Navigation.md` (Japanese; outline = spec "Documentation and samples" bullet 1)
- Modify: `wiki/Design-Rules.md` (Rule 10: add the exception quoted in the spec, translated; Rule 17 communication
  table: add "子 → 親の結果通知: 初期化パラメーターのコールバック")
- Modify: `wiki/HowTo-Implement-Master-Detail.md` (link "画面内で階層をたどる → ページ移動"), wiki sidebar / index page if
  the wiki has one (`wiki/_Sidebar.md` or `wiki/Home.md` — check)
- Modify: `FEATURES.md` (row: "In-window drill-down with a breadcrumb | `PageStack` + `IPageCloseGuard` … | Core | wiki")
- Modify: `CLAUDE.md` (one paragraph after "Child presenters owned by a parent presenter")
- Modify: `CHANGELOG.md` `[Unreleased]`: `### Added` (PageStack / IPageCloseGuard, window close asks pages, sample),
  `### Fixed` (AttachChild post-transfer failures)

- [ ] **Step 1:** Write; every code snippet compiles against the final API (copy from the sample).
- [ ] **Step 2:** Check links (`grep -o '](wiki/[^)]*)'` targets exist; wiki-internal links use the page name form
  the other pages use).
- [ ] **Step 3: Commit** `docs: page navigation how-to, Rule 10 callback exception, feature map`

---

### Task 10: Review and PR

- [ ] **Step 1:** `dotnet build Kibinago.sln -c Release` clean; `dotnet test tests/Kibinago.Analyzers.Tests/...` green;
  harness green.
- [ ] **Step 2:** `codex review --base master` (in the herdr pane `pagestack-review` if still open); fix findings or
  bring disagreements to the owner.
- [ ] **Step 3:** Ask the owner, then push `feat/page-stack` and open the PR (body ends with the Claude Code line);
  CI must be green (net48 tests, template verification).
- [ ] **Step 4:** Update memory (`MEMORY.md` index in this session's memory dir) with the outcome.

---

### Task 11: AntdUI trial (local only)

- [ ] **Step 1:** In `~/workspace/kibinago-antdui-trial/`, follow `ai-guide/` (stage 0 → HTML mock → WinForms →
  `2-compare.md`) for the same three levels with `AntdUI.Breadcrumb`, referencing the local Kibinago build.
- [ ] **Step 2:** Linux `dotnet build` only; the owner runs it on Windows. No push, no codex, no artifacts.
