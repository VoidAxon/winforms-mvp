# In-window page navigation with a breadcrumb (`PageStack` + `IPageLeaveGuard`) — design

Date: 2026-11-04 · Status: draft, awaiting the owner's approval · Reviewed with codex (two rounds, on the earlier variants) · Builder check done (three questions, answered below)

## Why

Some screens drill down inside one window — customer list → customer detail → order edit — with a breadcrumb
(AntdUI `Breadcrumb`, a row of `LinkLabel`s, …) to jump back to any level. Prism WPF covers this with Region
navigation (samples 17–24: `RequestNavigate`, `INavigationAware`, `IConfirmNavigationRequest`, `KeepAlive`, journal).
Kibinago has the building blocks (`AttachChild` + its handle, the dynamic-panel recipe) but nothing that

- asks the pages being discarded "can you leave?" (unsaved edits) before a jump, all-or-nothing;
- asks every page when the window closes (today `AttachChild` children take no part in close decisions);
- gets the async coordination right: a confirmation dialog pumps messages, so a second breadcrumb click, a window
  close or a guard that navigates can arrive while a jump is still deciding.

The last point is what applications would get wrong if each wrote it; that is why it goes into the framework.

The owner's constraints:

- Do it completely now — no "add later" pieces.
- **Whoever does not use it must not notice it**: no new member on `PresenterBase`, `ControlPresenterBase`,
  `WindowPresenterBase` (nothing new in IntelliSense for ordinary presenters).
- The scenario is rare, so the API stays small: **one interface + one dedicated data structure**.

## Decisions (made with the owner)

- **Single page stack, no forward / journal.** Only browser-modelled APIs (WPF / UWP / WinUI `Frame`, Prism WPF
  journal, web routers) have "forward"; app drill-down stacks (ReactiveUI `RoutingState`, MAUI `NavigationPage` /
  Shell, MvvmCross, iOS / Android / Flutter) do not, and a breadcrumb means exactly that: jumping to level N discards
  the pages to its right.
- **No KeepAlive switch.** Pages below the top always stay alive (hidden by the view, filters / scroll / selection
  kept); popped pages are always disposed. Nothing is left to choose.
- **No "reactivated" notification in the framework.** What a page does when it is on top again (nothing, refresh a
  row, reload) is the application's: results flow back through callbacks in the page's parameter object
  (`OrderEditParams.Saved = order => …`), and a page that really needs "I am visible again" gets a semantic event
  from its own view (`VisibleChanged` → `event EventHandler Reshown`) via `SubscribeViewEvent`.
- **No region names, no view resolution by name, no reuse-by-parameter (Prism sample 20).** Presenters are
  `new`-ed by the parent, exactly like `AttachChild`; the parent view interface creates and removes the page controls.
- **Page hook = one interface, one async method**, explicitly implemented (stays off the public surface, Rule 16/17):
  `Task<bool> CanLeaveAsync()`. Async because a synchronous answer is one `Task.FromResult` away, while the reverse
  (save before leaving) is impossible with a sync signature. netstandard2.0 / net472 have no default interface
  methods, so one method is the only way to keep the interface free of no-op members.
- The same guard answers both "a breadcrumb jump discards you" and "the window closes": to the page both mean "you
  are about to be discarded", so the unsaved-changes code exists once.

## Public API (Kibinago.Core, namespace `Kibinago.Presenters`)

Names are tentative; the owner may rename before implementation.

```csharp
/// Implemented (explicitly) by a page presenter that may refuse to be discarded.
public interface IPageLeaveGuard
{
    /// true = the page may be discarded. Asked before a PageStack jump removes it and before its window closes.
    Task<bool> CanLeaveAsync();
}

/// The pages of one in-window drill-down, owned by a presenter (window or panel).
public sealed class PageStack
{
    public PageStack(IPresenter owner);                 // typically in the owner's constructor: new PageStack(this)

    public int Count { get; }
    public bool IsBusy { get; }                          // a jump or a close check is deciding

    public void Push<TView>(ControlPresenterBase<TView> page, TView view, Action<TView> removeView)
        where TView : IControlView;
    public void Push<TView, TParam>(ControlPresenterBase<TView, TParam> page, TView view, TParam parameters,
        Action<TView> removeView) where TView : IControlView;

    public Task<bool> PopToAsync(int count);             // keep the first `count` pages
    public Task<bool> PopAsync();                        // PopToAsync(Count - 1)

    public Task<bool> CanLeaveAllAsync();                // asks every page (and nested stacks), removes nothing
}
```

Usage (parent presenter + parent view interface defined by the application):

```csharp
public interface ICustomerWorkspaceView : IWindowView
{
    ICustomerListView PushCustomerList();                // adds the control, shows it, extends the breadcrumb
    ICustomerDetailView PushCustomerDetail(string title);
    IOrderEditView PushOrderEdit(string title);
    void RemovePage(IControlView page);                  // removes the top control, shows the one below, trims the breadcrumb
}

public sealed class CustomerWorkspacePresenter : WindowPresenterBase<ICustomerWorkspaceView>
{
    private readonly PageStack _pages;
    public CustomerWorkspacePresenter(ICustomerRepository repo) { _repo = repo; _pages = new PageStack(this); }

    protected override void RegisterViewActions()
        => Dispatcher.Register(WorkspaceActions.BreadcrumbClicked, (int level) => _pages.PopToAsync(level + 1));

    protected override void OnInitialize()
        => _pages.Push(new CustomerListPresenter(_repo), View.PushCustomerList(), new CustomerListParams { Open = OpenCustomer },
                       View.RemovePage);

    private void OpenCustomer(int id) => _pages.Push(new CustomerDetailPresenter(_repo), View.PushCustomerDetail(…), …, View.RemovePage);
}

sealed class OrderEditPresenter : ControlPresenterBase<IOrderEditView, OrderEditParams>, IPageLeaveGuard
{
    Task<bool> IPageLeaveGuard.CanLeaveAsync()
        => Task.FromResult(!_tracker.IsChanged || Messages.ConfirmYesNo("変更を破棄しますか?"));
}
```

## Behaviour

### Owner

- `new PageStack(owner)` accepts any framework presenter (`PresenterBase<T>`; anything else → `ArgumentException`).
  Creating it touches no service and no view, so the owner's constructor is a fine place. A presenter may own several
  stacks. The stack registers itself with the owner (internal list) so the window close gate can find it.
- Pushing requires the owner's view (same rule as `AttachChild`: from `OnInitialize` on).
- When the owner is disposed, its pages are disposed with it (they are `AttachChild` children; the existing order
  applies). `removeView` is **not** called then (views are never torn down by presenter disposal). The stack is dead
  afterwards: `Push` throws `ObjectDisposedException`, `PopToAsync` returns `false`.

### Push

- Attaches and initializes the page through the owner's existing `AttachChild` machinery (made reachable inside Core;
  no new protected member), so service inheritance, view-ownership checks and control-disposal observation are the
  same as `AttachChild`. Any `ControlPresenterBase` may be pushed; one that does not implement `IPageLeaveGuard`
  always may leave.
- If attaching / initializing throws, nothing is recorded, `removeView(view)` is called (the view already created the
  control), then the exception propagates (`AggregateException` if `removeView` throws too). Preflight rejections
  that `AttachChild` raises before taking ownership (null args, a Form, owner without view, already attached) behave
  the same way.
- While `IsBusy`, `Push` throws `InvalidOperationException` (programming error: a guard or a close check is deciding;
  the modal confirmation makes it unreachable from ordinary UI input).
- A page whose control is disposed by someone else is released by the existing `AttachChild` observation; the stack
  drops that entry and every entry above it, without asking: top first, each `handle.Dispose()` then `removeView`,
  **the disposed page included** (so the view trims its breadcrumb and stays in step with `Count`). The view's
  `removeView` must therefore tolerate a control that is already disposed (removing its crumb is what matters).
  *(Builder-check answer — my call.)*

### PopToAsync(count)

1. `count` outside `0..Count` → `ArgumentOutOfRangeException`. `count == Count` → completed `true`, nothing happens.
2. Busy (another jump or a close check deciding) or owner disposed → completed `false`, nothing asked (repeat clicks
   are ignored, not queued — a queue would deadlock a guard that awaits its own navigation).
3. Set busy **before** asking anyone. Ask every page above `count`, **top first**, one at a time (await each). The
   first `false`, a faulted / cancelled task, or a synchronous throw from `CanLeaveAsync` → stop, log it (faults
   only, like `CanCloseAsync`), clear busy, return `false`. **Nothing is removed.** Pages that already answered
   `true` are asked again next time (documented: guards must tolerate being asked again; a save done inside a guard
   is not undone).
4. After every await: if the owner was disposed meanwhile, stop and return `false`; if an entry was released
   meanwhile (its control disposed), re-evaluate the remaining set.
5. All agreed → remove **top first**: for each page, `handle.Dispose()` in `try`, `removeView(view)` in `finally`
   (the dynamic-panel order). Removal continues past failures; afterwards busy is cleared, and collected exceptions
   are thrown (one → rethrown as is, several → `AggregateException`). Otherwise return `true`.
6. Runs on the UI thread (the caller's `SynchronizationContext`; awaits resume there).

### CanLeaveAllAsync — a panel that owns a stack is discarded by an ancestor

A third way pages are discarded: an ancestor disposes the panel that owns the stack through its `AttachChild` handle
(a tab closed, a dynamic panel removed, the sample shell switching demos). The framework cannot intercept a
synchronous `Dispose`, so the ancestor asks first — through the same interface, no new type *(owner's choice)*:

```csharp
// the panel that owns the stack is itself "a page that may refuse":
sealed class CustomerWorkspacePanelPresenter : ControlPresenterBase<IWorkspacePanelView>, IPageLeaveGuard
{
    Task<bool> IPageLeaveGuard.CanLeaveAsync() => _pages.CanLeaveAllAsync();
}
// the ancestor, before disposing the child's handle:
if (child is IPageLeaveGuard guard && !await guard.CanLeaveAsync()) return;
handle.Dispose();
```

- `CanLeaveAllAsync()` runs the same walk as the window close (below) over this stack: every page, top first, nested
  stacks of those pages first, each presenter asked at most once; removes nothing. Same busy rules as a jump (busy →
  completed `false` without asking; while it runs the stack is busy: jumps ignored, `Push` throws). Owner disposed →
  `false`. Faulted / throwing guard → refusal, logged.
- **Each presenter is asked at most once per check.** The window close walk (below) does not ask a presenter twice
  when it is reachable both as a page and through a forwarding guard; a guard of a presenter that is not a page is
  asked only when application code asks it (as above).
- The sample shell uses this before switching demos, so unsaved edits get the same prompt as ✕.

### Window close

- The window presenter's close gate (`ICloseParticipant.CanCloseGate`, internal) asks **every page of every stack in
  its presenter tree** before its own `CanCloseAsync`: stacks owned by the window and by its `AttachChild`
  descendants (an embedded panel that owns a stack, nested stacks), deepest stack first, each stack top first. Only
  pages pushed into a stack are asked; plain `AttachChild` panels are not (the master-detail contract — the parent
  checks shared input — stays unchanged).
- Any refusal blocks the close; the window's own `CanCloseAsync` is then not asked.
- `SystemShutdown` / `TaskManager` keep bypassing everything (unchanged).
- A stack that is busy (a jump deciding) answers "block" without prompting. The stacks stay busy **for the whole close
  check, including the window's own `CanCloseAsync`** (so no page can be pushed or edited between "pages agreed" and
  "window agreed"); busy is cleared on the UI thread (the gate's continuation today runs on `TaskScheduler.Default`,
  so clearing is marshalled to the owner's captured `SynchronizationContext`). *(Builder-check answer — my call.)*
- After the close is approved and before the host actually closes (async decisions are posted back), the stacks
  are no longer busy. A jump or push in that gap is allowed; the window then closes without asking the newly visible
  state again. Accepted: a page pushed in that gap has no edits yet, and a jump asked its own guards.
- `PresenterTestHost` needs no change: `host.CanClose()` / `CanCloseAsync()` / `RequestClose` go through the same
  gate.
- Pages pushed into a stack whose owner is a **control** presenter connected with `Connect` (no window presenter
  above it) take no part in any close: there is no gate. Documented.

### Child → parent results

Results travel through callbacks in the page's parameter object (`Action<Order> Saved`). This needs a narrow
exception in Design-Rules Rule 10 ("child → parent: events, never direct calls"):

> A child may invoke an app-defined callback supplied in its initialization parameters to report a bounded result to
> its owner. The callback carries application data; the child must not reference the parent presenter or the page
> stack. Shared observable state still belongs in a Model or Service.

## Testing (framework)

Tests in `tests/Kibinago.Samples.Tests` (net48, run by CI), driving the real entry points through `PresenterTestHost`
with fake views (no Forms):

- push / pop order, `removeView` calls, dispose-before-remove, `count` bounds, no-op pop;
- refusal leaves everything untouched; top-first asking; asked again after a refusal;
- async guard pending (`TaskCompletionSource`): repeat jump ignored, `Push` throws, window close blocked without
  prompting, owner disposed while pending → `false` and nothing removed;
- faulted / throwing guard → refusal + logged (`RecordingLoggerFactory`);
- removal failures continue and aggregate;
- push failure calls `removeView` and records nothing;
- window close: pages of window-owned and descendant-owned stacks asked deepest-first / top-first, refusal blocks,
  window `CanCloseAsync` not asked after a refusal, system shutdown bypasses, busy stack blocks without asking;
- page control disposed externally drops the entry and those above, `removeView` called for all of them top first,
  the disposed one included;
- `CanLeaveAllAsync`: asks all (nested first, top first), removes nothing, busy rules, each presenter asked once
  (also when the window close reaches a page through a forwarding guard);
- stacks stay busy while the window's own async `CanCloseAsync` is pending;
- the owner's disposal disposes the pages without `removeView`;
- `IPageLeaveGuard` explicitly implemented in a test page compiles without public members (Rule 16).

`ApiSurfaceTests` (if it pins public types) is updated for the two new public types.

## Documentation and samples

- **wiki** `wiki/HowTo-Implement-Page-Navigation.md` (Japanese, like the other wiki pages): when to use it; the
  parent view interface; push / jump; `IPageLeaveGuard`; results through parameter callbacks; "shown again" through
  a view event; window close; testing with `PresenterTestHost` (breadcrumb as a typed `ViewAction<int>` so a test can
  `await view.Binder.DispatchAsync(...)`); why there is no forward / KeepAlive / region (short Prism comparison);
  breadcrumb controls (AntdUI etc.) are a View-layer swap.
- `wiki/Design-Rules.md` Rule 10: the exception above. Link from `HowTo-Implement-Master-Detail.md`.
- `FEATURES.md`: a row (entry point `PageStack` / `IPageLeaveGuard`, package `Kibinago.Core`, wiki page).
- `CLAUDE.md`: one paragraph under "Child presenters owned by a parent presenter".
- `CHANGELOG.md` `[Unreleased]` → Added.
- XML docs on every public member (build error otherwise).
- **Sample** in `samples/Kibinago.Samples` (embedded demo in the shell, Japanese UI, standard controls — a row of
  `LinkLabel`s as breadcrumb): 顧客一覧 → 顧客詳細 → 受注編集 with in-memory data, a leave guard with
  `ChangeTracker`, a saved callback refreshing the list row, and presenter tests in `Kibinago.Samples.Tests`.
  The demo presenter forwards `IPageLeaveGuard` to `CanLeaveAllAsync()`, and `ShellPresenter` asks it before
  switching demos.
- **AntdUI trial** (local only, `~/workspace/kibinago-antdui-trial/`, never pushed, never sent to codex / artifacts):
  the same three levels with `AntdUI.Breadcrumb`, following that directory's `ai-guide/` stages (stage 0 → HTML mock
  → WinForms → mandatory `2-compare.md`). The owner runs it on Windows.

## Non-goals

Forward / journal, KeepAlive, region names, view resolution by name, reuse-by-parameter, a reactivation hook,
transition animations, page title management in the framework (the view owns the breadcrumb text), queuing of
navigation requests, a navigation state machine, `Connect`-hosted control owners participating in a close.

## Left to the builder

Internal shape (how `PageStack` reaches `AttachChildCore`, how the window gate enumerates stacks in the tree), log
messages, exception texts, test file layout, sample layout and wording, wiki wording.

## Notes

- This breaks the 2026-10-27 "feature freeze → stabilize → rc" decision; the owner chose to do it.
- Delivered as one PR (framework + tests + docs + sample), reviewed with codex before it is opened; the AntdUI trial
  stays local.
