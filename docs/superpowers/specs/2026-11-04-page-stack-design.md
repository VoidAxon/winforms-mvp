# In-window page navigation with a breadcrumb (`PageStack` + `IPageCloseGuard`) — design

Date: 2026-11-04 · Status: draft, awaiting the owner's approval · Reviewed with codex (three rounds) · Builder check
done (three questions, answered below)

## Why

Some screens drill down inside one window — customer list → customer detail → order edit — with a breadcrumb
(AntdUI `Breadcrumb`, a row of `LinkLabel`s, …) to jump back to any level. Prism WPF covers this with Region
navigation (samples 17–24: `RequestNavigate`, `INavigationAware`, `IConfirmNavigationRequest`, `KeepAlive`, journal).
Kibinago has the building blocks (`AttachChild` + its handle, the dynamic-panel recipe) but nothing that

- asks the pages being discarded "may you be closed?" (unsaved edits) before a jump, all-or-nothing;
- asks every page when the window closes (today `AttachChild` children take no part in close decisions);
- gets the async coordination right: a guard may show a dialog (which pumps messages) or await a save, so a second
  breadcrumb click, a window close or a guard that navigates can arrive while a jump is still deciding.

The last point is what applications would get wrong if each wrote it; that is why it goes into the framework.

The owner's constraints:

- Do it completely now — no "add later" pieces.
- **Whoever does not use it must not notice it**: no new member on `PresenterBase`, `ControlPresenterBase`,
  `WindowPresenterBase` (nothing new in IntelliSense for ordinary presenters).
- The scenario is rare, so the public API stays small: **one interface + one dedicated data structure**. Internal
  machinery may be as thorough as needed.

## Decisions (made with the owner)

- **Single page stack, no forward / journal.** Only browser-modelled APIs (WPF / UWP / WinUI `Frame`, Prism WPF
  journal, web routers) have "forward"; app drill-down stacks (ReactiveUI `RoutingState`, MAUI `NavigationPage` /
  Shell, MvvmCross, iOS / Android / Flutter) do not, and a breadcrumb means exactly that: jumping to level N discards
  the pages to its right.
- **No KeepAlive switch.** Pages below the top always stay alive (hidden by the view, filters / scroll / selection
  kept); popped pages are always disposed. Nothing is left to choose.
- **No "reactivated" notification in the framework.** What a page does when it is on top again is the application's:
  results flow back through callbacks in the page's parameter object (`OrderEditParams.Saved = order => …`), and a
  page that needs "I am visible again" gets a semantic event from its own view (`VisibleChanged` →
  `event EventHandler Reshown`) via `SubscribeViewEvent`.
- **No region names, no view resolution by name, no reuse-by-parameter (Prism sample 20).** Presenters are `new`-ed by
  the parent, exactly like `AttachChild`; the parent view interface creates and removes the page controls.
- **Page hook = one interface, one async method**, explicitly implemented (off the public surface, Rule 16/17).
  Async because a synchronous answer is one `Task.FromResult` away (owner: "sync can be emulated"); netstandard2.0 /
  net472 have no default interface methods, so one method keeps the interface free of no-op members.
- **"Close" vocabulary** (owner accepted codex's naming): the guard controls permanent discarding — a jump, the window
  closing, an ancestor disposing the panel — not being hidden by a push, so `CanCloseAsync` (same word as the window's
  `CanClose` / `CanCloseAsync`, closest prior art Caliburn.Micro `IGuardClose.CanCloseAsync`). Only control presenters
  implement it (`Push` accepts `ControlPresenterBase` only; `ControlPresenterBase` has no `CanClose` of its own), so
  inside a page class it is the only `CanClose*` member. Window presenters keep `CanCloseAsync(CloseReason)`; the
  interface's XML docs say so (no analyzer).
- One guard answers every discard (jump, window close, ancestor disposal), so the unsaved-changes code exists once.
- **Ancestor disposal is covered** (owner's choice): `PageStack.CanCloseAllAsync()` lets the panel that owns a stack
  forward its own guard to the stack, so an ancestor can ask before disposing it.

## Public API (Kibinago.Core, namespace `Kibinago.Presenters`)

```csharp
/// Implemented (explicitly) by a page presenter (a control presenter pushed into a PageStack, or a panel that owns
/// one) that may refuse to be discarded. Window presenters override CanCloseAsync(CloseReason) instead.
public interface IPageCloseGuard
{
    /// true = this page may be discarded. Asked before a PageStack jump removes it, before its window closes and when
    /// application code asks (an ancestor about to dispose it). May be asked again after a refusal elsewhere.
    Task<bool> CanCloseAsync();
}

/// The pages of one in-window drill-down, owned by a presenter (window or panel).
public sealed class PageStack
{
    public PageStack(IPresenter owner);          // typically in the owner's constructor: new PageStack(this)

    public int Count { get; }
    public bool IsBusy { get; }                   // a check or a removal is running; blocks stack operations only

    public void Push<TView>(ControlPresenterBase<TView> page, TView view, Action<TView> removeView)
        where TView : IControlView;
    public void Push<TView, TParam>(ControlPresenterBase<TView, TParam> page, TView view, TParam parameters,
        Action<TView> removeView) where TView : IControlView;

    public Task<bool> PopToCountAsync(int remainingCount);   // keep the first remainingCount pages
    public Task<bool> PopAsync();                             // PopToCountAsync(Count - 1); empty stack → true
    public Task<bool> CanCloseAllAsync();                     // asks every page (nested stacks included), removes nothing
}
```

Usage (parent presenter + parent view interface defined by the application):

```csharp
public interface ICustomerWorkspaceView : IWindowView
{
    ICustomerListView PushCustomerList();                // adds the control, shows it, extends the breadcrumb
    ICustomerDetailView PushCustomerDetail(string title);
    IOrderEditView PushOrderEdit(string title);
    void RemovePage(IControlView page);                  // removes that control and its crumb, shows the one below;
                                                         // must tolerate a control that is already disposed
}

public sealed class CustomerWorkspacePresenter : WindowPresenterBase<ICustomerWorkspaceView>
{
    private readonly PageStack _pages;
    public CustomerWorkspacePresenter(ICustomerRepository repo) { _repo = repo; _pages = new PageStack(this); }

    protected override void RegisterViewActions()
        => Dispatcher.Register(WorkspaceActions.BreadcrumbClicked, (int level) => _pages.PopToCountAsync(level + 1));

    protected override void OnInitialize()
        => _pages.Push(new CustomerListPresenter(_repo), View.PushCustomerList(),
                       new CustomerListParams { Open = OpenCustomer }, View.RemovePage);

    private void OpenCustomer(int id)
    {
        if (_pages.IsBusy) return;                       // check before the view creates the control
        _pages.Push(new CustomerDetailPresenter(_repo), View.PushCustomerDetail(…), new CustomerDetailParams { … },
                    View.RemovePage);
    }
}

sealed class OrderEditPresenter : ControlPresenterBase<IOrderEditView, OrderEditParams>, IPageCloseGuard
{
    Task<bool> IPageCloseGuard.CanCloseAsync()
        => Task.FromResult(!_tracker.IsChanged || Messages.ConfirmYesNo("変更を破棄しますか?"));
}
```

## Behaviour

### Owner

- `new PageStack(owner)`: `ArgumentNullException` for null, `ArgumentException` for a presenter that is not a
  framework presenter (checked through an internal capability), `ObjectDisposedException` for a disposed owner.
  Construction touches no service, view, synchronization context or overridable code, so the owner's constructor is
  a fine place. A presenter may own several stacks; each registers with its owner (internal, registration order kept).
- Pushing requires the owner's view (same rule as `AttachChild`: from `OnInitialize` on).
- Owner disposal disposes the pages with it (they are `AttachChild` children; existing order). `removeView` is **not**
  called then (presenter disposal never tears views down). The stack is dead afterwards: `Push` throws
  `ObjectDisposedException`; `PopToCountAsync` / `PopAsync` / `CanCloseAllAsync` return completed `false`.

### One check policy for every discard

Jump, window close and `CanCloseAllAsync` use **one internal traversal**:

- **Order (recursive):** a stack is visited top first; for each page, the stacks the page's presenter tree owns
  (stacks owned by the page or by its `AttachChild` descendants, sibling stacks in reverse registration order) are
  visited first, then the page's own guard. Each guard is awaited before the next is asked.
- **Who is asked:** pages in stacks (with or without a guard — a page without a guard still has its nested stacks
  checked) and their guards. Plain `AttachChild` panels that are not pages are not asked (the master-detail
  contract stays). A page's guard is asked even when the page owns stacks (it may check page-level state too).
- **Check context:** an internal context is shared by everything one check reaches, including a forwarding guard
  that calls `CanCloseAllAsync()` on a stack the check already holds. Results are recorded per presenter and per
  stack: a guard or stack already answered in this check is not asked again (its result is reused); a forwarding call
  into a stack held by the same check reuses / runs within that check instead of seeing "busy". A cycle (a guard
  awaiting a check that is waiting for it) refuses without awaiting itself. An independent, overlapping check sees
  the stacks busy and gets `false` without anyone being asked.
- **Refusal:** the first `false`, a faulted / cancelled task, a `null` task (logged as a contract failure) or a
  synchronous throw → stop, log (faults / null / throws, like `CanCloseAsync`), release, answer `false`. Nothing is
  removed. Guards may be asked again later; a save done inside a guard is not undone (documented).
- **Reservation:** before the first guard is asked, every stack the check will traverse is reserved (busy). If any is
  already busy, the reservations taken are released and the check answers `false` without asking. Stacks that enter
  the traversed trees during the check are enrolled in its reservation: stacks constructed there are born reserved,
  and when a presenter that already owns stacks is attached into a reserved tree (plain `AttachChild` or `Push`
  during an awaited guard), its subtree's existing stacks are enrolled at ownership transfer.
- **Who releases:** the reservation belongs to the **operation** that started the check, not to the check: a
  standalone `CanCloseAllAsync()` releases when it answers; a jump keeps it through the removal and releases after;
  a window close keeps it until the host consumes the answer (see Window close); a refusal releases at once. A
  forwarding call that joined an enclosing check never releases anything. Release runs on the UI context, in the
  operation's own `finally`, before its result is reported.
- **After every await:** owner disposed → stop, `false`; entries released meanwhile (external disposal, below) are
  re-evaluated.
- **Threading:** runs on the UI thread; awaits resume on the caller's `SynchronizationContext`. With no context (unit
  tests) continuations run inline / on the thread pool and the same rules hold (the stack is single-threaded by
  contract, like `ChangeTracker`). The context is never captured at construction.
- `IsBusy` blocks **stack operations only**; it does not disable editing or other actions in the pages. A
  `ShowBusy()` inside one guard ends when that guard answers, so it does not protect the rest of the decision. A
  screen that must lock input for a whole jump wraps the operation in the parent:
  `using (ShowBusy()) await _pages.PopToCountAsync(n);` (documented). For a window close the pages' guards and the
  window's own `CanCloseAsync` run in sequence with no framework-wide input lock; a page that awaits non-modal work
  in its guard wraps that work in `ShowBusy()`, and the documentation states that edits made in another page between
  two guards' answers are not re-asked.

### Push

- Order of events: preflight (owner alive and has a view, stack not busy, arguments valid, the page not attached /
  owned, the view not claimed by another live presenter, the view not a Form / not disposed) → the page is accepted
  for this push → attach + initialize through the owner's `AttachChild` machinery (made reachable inside Core; no new
  protected member) → entry recorded.
- **Preflight rejection** (`ArgumentNullException`, `ArgumentException`, `ObjectDisposedException`,
  `InvalidOperationException` incl. "busy") does nothing else: the presenter is not disposed and `removeView` is
  **not** called (the view may belong to someone else). Callers check `IsBusy` before creating the view (sample does).
- **Failure after acceptance** (the page's `OnViewAttached` / `RegisterViewActions` / `OnInitialize` throws, its view
  is disposed during attach, …) unwinds the attachment and calls `removeView(view)` exactly once, then propagates
  (`AggregateException` with the removal failure if that throws too). The existing `AttachChildCore` gap — disposal
  observation and view claiming happen after ownership transfer but outside the `try` — is fixed so every
  post-transfer failure is covered (also benefits `AttachChild`).
- **Reentrancy:** the stack is reserved during `Push`, so a page's `OnInitialize` that pushes or pops on the same
  stack is rejected (`Push` throws, pops return `false`). The page's own nested stacks are unaffected.
- Any `ControlPresenterBase` may be pushed; one without `IPageCloseGuard` may always be discarded (its nested stacks
  are still checked).

### PopToCountAsync(remainingCount) / PopAsync()

1. Owner disposed → `false`. Busy → `false` (ignored, not queued — a queue would deadlock a guard that awaits its own
   navigation). These come first, before any no-op answer.
2. `remainingCount` outside `0..Count` → `ArgumentOutOfRangeException`. `remainingCount == Count` → `true`.
   `PopAsync()` on an empty stack → `true`.
3. Check the pages above `remainingCount` with the policy above (top first, nested first). Refusal → `false`,
   nothing removed.
4. All agreed → remove top first: stack bookkeeping is updated **before** any teardown callback runs (so reentrancy
   cannot remove an entry twice); for each page `handle.Dispose()`, then `removeView(view)` — each failure collected
   separately (neither hides the other); continue to the end; release; throw the collected failures (one → as is,
   several → `AggregateException`), else `true`.
5. The page that is on top afterwards is not notified (see Decisions).

### External disposal of a page's control

- `ChildAttachment` gets an internal release notification so the stack learns of a release immediately (no polling).
  Three cases are distinguished: **external release** (the page's control was disposed by someone else) → the stack
  removes that entry and every entry above it, without asking: bookkeeping first, then top first each remaining
  `handle.Dispose()` and `removeView`, **the disposed page included** (the view trims its crumb; `removeView` must
  tolerate a disposed control); **normal pop** → already removing, no second removal; **owner teardown** →
  `removeView` suppressed.
- Order caveat (documented): the externally disposed page's presenter may already be tearing down when the stack
  hears of it; top-first order is guaranteed for the `removeView` calls, not for that presenter's own teardown.
- During a pending check: the released entry counts as already discarded; the check continues with the rest.

### Window close

- The window presenter's close gate (`ICloseParticipant.CanCloseGate`, internal) first runs the check policy over
  every stack in its presenter tree (stacks owned by the window and by its `AttachChild` descendants; sibling stacks
  in reverse registration order), then — only if all pages agreed — its own `CanCloseAsync(CloseReason)`.
- `SystemShutdown` / `TaskManager` bypass everything before any reservation (unchanged).
- **Reservation until the host closes.** The reservation taken by a close check is kept through the window's own
  `CanCloseAsync` and **until the host consumes the answer**: approved → kept until the window actually closes /
  the presenter is disposed; refused → released when the answer is delivered; an approved close that the host then
  abandons (another `FormClosing` handler cancels, …) → released. So no push / jump can slip in between "approved" and
  "closed". This is internal lifecycle integration in `WindowLifecycleController` (the async answer is posted to the
  UI thread and then closes with the gate bypassed) and in `PresenterTestHost` (which mirrors the gate and must hold /
  release the reservation the same way, so tests observe identical behaviour). No public type or member is added.
  In `PresenterTestHost`: an accepted `RequestClose` counts as a close that happened — the reservation is kept until
  `host.Dispose()`; `host.CanClose()` / `host.CanCloseAsync()` are permission queries that close nothing — they
  release the reservation when the answer is delivered (like an abandoned close), so a later jump still works.
- A busy stack (a jump deciding) makes the close answer "block" without prompting.
- `RequestClose` goes through the same gate (unchanged), so pages are asked for presenter-initiated closes too.
- A stack whose owner is a **control** presenter hosted with `Connect` (no window presenter above it) takes part in no
  window close: there is no gate. Its host must ask (`CanCloseAllAsync` / a forwarding guard) before removing it.
  Documented.

### CanCloseAllAsync — a panel that owns a stack is discarded by an ancestor

An ancestor disposing the panel that owns a stack through its `AttachChild` handle (a tab closed, a dynamic panel
removed, the sample shell switching demos) cannot be intercepted — `Dispose` is synchronous and unvetoable — so the
ancestor asks first, through the same interface:

```csharp
sealed class CustomerWorkspacePanelPresenter : ControlPresenterBase<IWorkspacePanelView>, IPageCloseGuard
{
    Task<bool> IPageCloseGuard.CanCloseAsync() => _pages.CanCloseAllAsync();
}
// the ancestor keeps the child presenter (or its guard) next to the handle, and before disposing:
if (child is IPageCloseGuard guard && !await guard.CanCloseAsync()) return;
handle.Dispose();
```

`CanCloseAllAsync()` = the check policy over this stack, removing nothing. Owner disposed / busy (outside the same
check) → `false`. When reached from a window close or a jump through a forwarding guard, it joins that check (see
Check context).

### Child → parent results

Results travel through callbacks in the page's parameter object (`Action<Order> Saved`). Design-Rules Rule 10 ("child
→ parent: events, never direct calls") gets a narrow exception, and Rule 17's communication guidance is aligned:

> A child may invoke an app-defined callback supplied in its initialization parameters to report a bounded result to
> its owner. The callback carries application data; the child must not reference the parent presenter or the page
> stack. Shared observable state still belongs in a Model or Service.

## Testing (framework)

Tests in `tests/Kibinago.Samples.Tests` (net48, run by CI), driving real entry points through `PresenterTestHost`
with fake views (no Forms), plus `WindowLifecycleController` tests where the reservation-until-close is concerned:

- push / pop order, `removeView` calls, dispose-before-remove, `remainingCount` bounds, no-op pops, empty `PopAsync`,
  busy / disposed precedence over no-op;
- refusal leaves everything untouched; top-first and nested-first order; asked again after a refusal;
- **nested pop without forwarding**: a removed page that owns a stack has that stack's pages asked;
- **forwarding under an existing check**: a panel guard forwarding to `CanCloseAllAsync()` inside a window close / a
  jump reuses the check (no false "busy"), each guard asked once; cycle refuses without hanging;
- async guard pending (`TaskCompletionSource`): repeat jump ignored, `Push` throws, overlapping close blocked without
  prompting, owner disposed while pending → `false` and nothing removed, external disposal while pending;
- faulted / throwing / null-task guard → refusal + logged (`RecordingLoggerFactory`);
- removal failures continue and aggregate (disposal and removal failures both kept);
- push: preflight rejections call no `removeView` and dispose nothing (incl. a view claimed by another presenter);
  post-acceptance failure calls `removeView` once; reentrant push / pop from a page's `OnInitialize` rejected;
  `AttachChildCore` post-transfer failure coverage (also for plain `AttachChild`);
- external disposal: entry and those above dropped, `removeView` top first including the disposed page; normal pop
  and owner teardown do not double-remove / do call none;
- window close: pages of window-owned and descendant-owned stacks asked in the recursive order, refusal blocks and
  skips the window's own `CanCloseAsync`, system shutdown bypasses, busy stack blocks without asking, `RequestClose`
  asks pages; **reservation kept from approval until the window closes** (a push / jump in that gap is rejected),
  released on refusal and on an abandoned close — in `PresenterTestHost` and in `WindowLifecycleController`;
- owner disposal disposes the pages without `removeView`; stack dead afterwards;
- `IPageCloseGuard` explicitly implemented in a test page leaves no public members (Rule 16).

`ApiSurfaceTests` (if it pins public types) is updated for the two new public types.

## Documentation and samples

- **wiki** `wiki/HowTo-Implement-Page-Navigation.md` (Japanese, like the other wiki pages): when to use it; the parent
  view interface; push / jump; `IPageCloseGuard`; `IsBusy` before creating a view; results through parameter
  callbacks; "shown again" through a view event; window close; ancestor disposal with `CanCloseAllAsync`; `ShowBusy`
  in async guards; testing with `PresenterTestHost` (breadcrumb as a typed `ViewAction<int>` so a test can
  `await view.Binder.DispatchAsync(...)`); why there is no forward / KeepAlive / region (short Prism comparison);
  breadcrumb controls (AntdUI etc.) are a View-layer swap.
- `wiki/Design-Rules.md` Rule 10 exception + Rule 17 alignment. Link from `HowTo-Implement-Master-Detail.md`.
- `FEATURES.md`: a row (entry points `PageStack` / `IPageCloseGuard`, package `Kibinago.Core`, wiki page).
- `CLAUDE.md`: one paragraph under "Child presenters owned by a parent presenter".
- `CHANGELOG.md` `[Unreleased]` → Added (and Fixed for the `AttachChildCore` post-transfer gap).
- XML docs on every public member (build error otherwise).
- **Sample** in `samples/Kibinago.Samples` (embedded demo in the shell, Japanese UI, standard controls — a row of
  `LinkLabel`s as breadcrumb): 顧客一覧 → 顧客詳細 → 受注編集 with in-memory data, a guard with `ChangeTracker`, a
  saved callback refreshing the list row, presenter tests in `Kibinago.Samples.Tests`. The demo presenter forwards
  `IPageCloseGuard` to `CanCloseAllAsync()`. `ShellPresenter` keeps each embedded demo's guard next to its handle;
  category switching becomes a typed async ViewAction that asks every affected demo before disposing any, ignores
  overlapping switches, and is safe against a window close while a switch is pending. `Cleanup` stays synchronous and
  unconditional (disposal cannot be vetoed).
- **AntdUI trial** (local only, `~/workspace/kibinago-antdui-trial/`, never pushed, never sent to codex / artifacts):
  the same three levels with `AntdUI.Breadcrumb`, following that directory's `ai-guide/` stages (stage 0 → HTML mock
  → WinForms → mandatory `2-compare.md`). The owner runs it on Windows.

## Non-goals

Forward / journal, KeepAlive, region names, view resolution by name, reuse-by-parameter, a reactivation hook,
transition animations, page titles in the framework (the view owns the breadcrumb text), queuing of navigation
requests, a public navigation state machine, `Connect`-hosted control owners joining a window close, an analyzer for
window presenters implementing `IPageCloseGuard`.

## Left to the builder

Internal shapes (how `PageStack` reaches `AttachChildCore`, the check-context type, the release notification, how the
gate enumerates stacks, how `WindowLifecycleController` / `PresenterTestHost` hold the reservation), log messages,
exception texts, test file layout, sample layout and wording, wiki wording.

## Calls made without the owner (check these)

1. Window close asks all pages first, then the window's own `CanCloseAsync`; a page refusal skips the window's.
2. Recursive order: each stack top first, a page's nested stacks before the page; sibling stacks in reverse
   registration order.
3. While busy: `Push` throws `InvalidOperationException`; pops / `CanCloseAllAsync` return `false`.
4. External disposal calls `removeView` for the disposed page too; `removeView` must tolerate a disposed control.
5. Reservation kept from close approval until the window actually closes (codex round 3) — needs
   `WindowLifecycleController` and `PresenterTestHost` changes, no public API.
6. Push preflight rejections never call `removeView`; the `AttachChildCore` post-transfer gap is fixed in the same PR.
7. `PopAsync()` on an empty stack → `true`; a guard returning a `null` task → refusal, logged.
8. `IsBusy` does not lock editing; a whole-jump input lock is the parent's `using (ShowBusy())` around the pop.
9. Check reservations belong to the operation (jump: through removal; close: until the host consumes the answer);
   stacks entering a reserved tree during a check are enrolled.
10. `PresenterTestHost`: accepted `RequestClose` keeps the reservation until `host.Dispose()`; `host.CanClose*()`
    queries release it when answered.

## Notes

- This breaks the 2026-10-27 "feature freeze → stabilize → rc" decision; the owner chose to do it.
- Delivered as one PR (framework + tests + docs + sample), reviewed with codex before it is opened; the AntdUI trial
  stays local.
