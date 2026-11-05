# Page host for `PageStack` — design

Date: 2026-11-05 · Status: draft — owner answered A / B (2026-11-05); awaiting approval of the document · Builder check + codex review done · Follows PR #81 (`PageStack` + `IPageCloseGuard`,
spec `2026-11-04-page-stack-design.md`, which stays the authority for the check policy, busy rules, window close and
external release — nothing there changes).

## Why

With #81 each drill-down screen writes the page plumbing in its view (the owner's view of the merged sample and the
AntdUI trial, ~50 lines per screen):

- the parent view interface has one `PushXxx(title)` per page type plus `RemovePage(page)`;
- the view implements them: create the UserControl, add it, hide the page below, add / remove a breadcrumb item;
- the presenter first asks the view for the control, then pushes it with a `removeView` callback:
  `_pages.Push(new OrderEditPresenter(_store), View.PushOrderEdit(title), param, View.RemovePage)`.

The owner asked why the presenter needs the view at all, what `removeView` means, and whether the method is too
complex — and pointed out that Kibinago can already map view interfaces to implementations (`IViewMappingRegister`;
its factory overload already accepts any type, `CreateInstance` returns `object`). Every comparable library provides
a host control that creates and shows the pages (WPF / UWP `Frame`, Prism region on a `ContentControl`, Caliburn.Micro
`ContentControl` + conductor, ReactiveUI `RoutedViewHost` / WinForms `RoutedControlHost`, MAUI `NavigationPage`,
DevExpress WinForms `NavigationFrame`), and none of them draws the breadcrumb: the host shows pages, the app binds its
breadcrumb to the page list (`Frame.BackStack`, Prism journal, ReactiveUI `Router.NavigationStack`).

Owner's decisions: add the host ("其它库里有提供这个宿主吗？" → yes → "OK，开始吧"); do it completely; whoever does
not use it must not notice it; no compatibility shims (no users yet).

## Goal

```csharp
// startup — the same registry the Navigator uses for windows
register.Register<IOrderEditView, OrderEditControl>();

// parent view interface: no PushXxx / RemovePage any more
public interface ICustomerWorkspaceView : IWindowView
{
    IPageHost Pages { get; }                        // a PageHost control on the Form (Kibinago package)
}

// presenter
_pages = new PageStack(this, () => View.Pages);
_pages.Push(new OrderEditPresenter(_store), new OrderEditParams { OrderId = id, Saved = OnSaved }, "受注 " + id);

// view: only the breadcrumb (≈10 lines) — the host keeps the titles, the view draws them
pageHost.PagesChanged += (s, e) => RebuildCrumbs(pageHost.PageTitles);   // crumb click → view event → presenter
```

## Public API

### Kibinago.Core — `Kibinago.Views` (the view capability) and `Kibinago.Presenters`

```csharp
namespace Kibinago.Views
{
    /// Where a PageStack's pages are shown: a view capability, like IActionBindable. Implemented by Kibinago's
    /// PageHost control (WinForms) and Kibinago.Testing's FakePageHost; an application may implement it.
    public interface IPageHost
    {
        /// Creates a new view for viewInterface (services: the pushing presenter's — its scope, its container),
        /// shows it on top with title as its breadcrumb text (the page below stays alive, hidden) and returns it.
        /// Throws, with nothing added, when no new usable view can be made (no mapping; null; a Form; disposed;
        /// already parented or used by a live presenter); a host that throws midway undoes its own changes.
        IControlView AddPage(Type viewInterface, string title, IServiceProvider services);

        /// Removes a page this host added (any level) and disposes it; shows the new top. Tolerates a control
        /// already disposed.
        void RemovePage(IControlView page);
    }
}

public sealed class PageStack
{
    public PageStack(IPresenter owner, Func<IPageHost> host);    // host read on first use (View exists from OnInitialize)

    /// true once the page is on top. false when it is not: the stack was busy (nothing created, the page presenter
    /// untouched), or the page ended during its own initialization (it disposed itself — its view removed — or the owner
    /// was disposed — nothing removed; the page presenter is disposed either way).
    public bool Push<TView>(ControlPresenterBase<TView> page, string title) where TView : class, IControlView;
    public bool Push<TView, TParam>(ControlPresenterBase<TView, TParam> page, TParam parameters, string title)
        where TView : class, IControlView;

    // unchanged: Count, IsBusy, PopToCountAsync, PopAsync, CanCloseAllAsync
}
```

The `Push(page, view, removeView)` / `Push(page, view, parameters, removeView)` overloads and the `PageStack(IPresenter)`
constructor are **removed** (one way to push; no shims). `IPageCloseGuard` is unchanged.

### Kibinago (WinForms) — `Kibinago.Views`

```csharp
/// A panel that shows the top page of a PageStack. Put it on the Form (designer) and expose it as IPageHost.
public class PageHost : Panel, IPageHost
{
    public IReadOnlyList<string> PageTitles { get; }   // bottom first — for the view's breadcrumb
    public event EventHandler PagesChanged;            // after a page was added or removed
    // IPageHost: AddPage creates through services' IViewMappingRegister.WithServiceProvider(services) — the same
    // lookup (with the container fallback) WindowNavigator uses — then Dock = Fill, BringToFront, the previous top
    // hidden, focus into the new page; RemovePage removes any page it added, disposes it, shows the new top.
}
```

`IViewMappingRegister.Register<TViewInterface, TViewImplementation>()`: the constraint becomes
`where TViewImplementation : Control, TViewInterface` (was `Form`) so pages register like windows — in the interface,
`ViewMappingRegister` and `ServiceProviderViewMappingRegister`. `WindowNavigator`
already rejects a non-Form implementation with a clear message. `RegisterFromAssembly` / `RegisterFromNamespace` keep
scanning Forms only (see Non-goals).

### Kibinago.Testing

```csharp
/// IPageHost for presenter tests: creates mock views from registered factories and records what happened.
public sealed class FakePageHost : IPageHost
{
    public FakePageHost Register<TView>(Func<TView> create) where TView : class, IControlView;
    public IReadOnlyList<IControlView> Pages { get; }      // bottom first
    public IReadOnlyList<string> PageTitles { get; }          // as on PageHost
    public IReadOnlyList<IControlView> Removed { get; }    // in removal order
    public T Page<T>(int level) where T : class;           // typed access for assertions / driving a mock view
}
```

## Behaviour

- **Push is one transaction**:
  1. Busy → return `false` at once: nothing is created, the page presenter is untouched (owner's answer B, like
     the pops; callers drop their `if (_pages.IsBusy) return;`). Then the page-level checks before anything is
     created — they throw as today: owner alive (`ObjectDisposedException`) and has its view; `page` / `title`
     non-null (empty title allowed); the page not attached / initialized / owned / disposed. A rejection here
     changes nothing.
  2. The stack is reserved (busy) from here to the end, so a page constructor, focus handling or a `PagesChanged`
     handler cannot push or pop meanwhile.
  3. `host.AddPage(typeof(TView), title, services)` with the **page's effective services**: its own when it was given
     some (`IPresenterFactory`, its M.E.DI scope), otherwise the owner's (what `AttachChild` will give it) — so the
     view and its presenter resolve the same scoped services. It throws → propagates; the host has undone itself.
  4. The returned view is checked (a `TView`; not claimed by another presenter). Any failure from here until the
     page is accepted (wrong type, the view claimed, service inheritance rejected) → `host.RemovePage(view)` once,
     the presenter is **not** disposed, the failure propagates (with the removal's failure: `AggregateException`).
  5. Ownership transfer and initialization exactly as #81: a failure after acceptance disposes the page,
     `host.RemovePage(view)` once, propagates. A page that ended during its initialization without a failure → `Push` returns `false`: it
     disposed itself → its view removed once; the owner was disposed → nothing removed (owner teardown removes
     no views).
- **Every place #81 called `removeView`** now calls `host.RemovePage(view)` (pop, external release after the released
  page's teardown, failed push, page that disposed itself during its push). Owner teardown still removes nothing.
- **Host lookup**: `Func<IPageHost>` is called on the first `Push` and the result kept (one host per stack); it must
  return non-null (`InvalidOperationException` otherwise — "expose the PageHost control through the view
  interface"). A host disposed later makes `AddPage` throw `ObjectDisposedException` (propagates as step 3).
- **Breadcrumb** stays the view's: `PageHost` publishes `PageTitles` + `PagesChanged`; a crumb click is a view event /
  typed `ViewAction<int>` → the presenter's `PopToCountAsync(level + 1)` (as in #81, so jumps stay testable through
  the binder and logged by `IActionObserver`).
- **PageHost creating views**: from the services `PageStack` passes (the pushing presenter's provider — under M.E.DI its
  scope), `IViewMappingRegister.WithServiceProvider(services)`: the Navigator's lookup, so a page view registered only
  in the container (constructor dependencies, scoped services) is created like a window. No `ServiceLocator.Current`.
- Single-threaded (UI thread), as before.

## Testing

- Core (net10 harness + CI): the existing PageStack tests move to `FakePageHost` (the `removeView` lambdas become
  `host.Removed` assertions); new tests for the Push failure branches above, host lookup (called once, null), title
  null.
- `PageHost` (CI, WinForms): add / hide / remove any level / tolerate disposed / `PagesChanged` and `PageTitles` /
  mapping missing / focus.
- `IViewMappingRegister`: a UserControl registers type-based; `WindowNavigator` still rejects it as a window.
- Samples: the presenter tests use `FakePageHost`.

## Documentation and samples

- Sample `PageNavigationDemo`: `IPageNavigationDemoView` shrinks to `IPageHost Pages { get; }` (+ status line); the
  UserControl hosts a `PageHost` and draws the LinkLabel breadcrumb from `PageTitles`; page controls registered at
  startup (`Program.cs` / the sample's view registration).
- wiki `HowTo-Implement-Page-Navigation.md`, `CLAUDE.md`, `FEATURES.md`, `CHANGELOG.md` (`[Unreleased]` → Changed
  (breaking): Push signature / constructor; Added: `IPageHost`, `PageHost`, `FakePageHost`, Control mappings).
- AntdUI trial (local only): same change; the AntdUI `Breadcrumb` fed from `PageTitles`.

## Non-goals

Drawing the breadcrumb in the host; renaming a crumb after the push (owner, A); navigation by view name; transition animations; scanning UserControls in
`RegisterFromAssembly` (an application's assembly has many UserControls that are not pages — e.g. the sample's
demos — and the scan's "several implementations throw" rule would start to fire); keeping the explicit-view `Push`
overloads.

## Left to the builder

Internal shapes, exception texts, PageHost focus details, sample layout, FakePageHost internals.

## Calls made without the owner (check these)

1. One way to push: the explicit-view overloads and `removeView` are removed, not kept beside the host.
2. `IPageHost` lives in `Kibinago.Views` (a view capability, codex); `ApiSurfaceTests`' view-interface list is updated
   on purpose.
3. The host creates the view (`AddPage(Type, title)`), not the stack.
4. Breadcrumb clicks stay in the presenter (view event / typed action), not handled by the host.
5. `Register<TI, TImpl>()` constraint relaxed from `Form` to `Control`; scanning stays Forms-only.
6. `PageStack(owner, Func<IPageHost>)` instead of finding the host on the owner's view by convention.
7. `title` is required (a page without a breadcrumb text is a mistake).
8. The host creates the view from the page's effective services (its own binding, else the owner's — scope +
   container fallback, as the Navigator), passed in `AddPage` — not from `ServiceLocator.Current` (builder check /
   codex, two rounds).
9. A view rejected after the host made it (wrong type, claimed, services rejected) is removed through the host, the
   presenter stays undisposed; the host validates what it creates before it changes its visual tree (codex).

## Owner's answers (2026-11-05)

- **A. Renaming a crumb after the push: no.** The title is fixed at `Push` (the parent normally knows the name when
  it opens the page). Non-goal.
- **B. `Push` while busy: returns `false` and does nothing** (`Push` returns `bool`; `true` = the page is on top).
