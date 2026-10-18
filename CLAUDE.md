# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A WinForms MVP (Model-View-Presenter) framework for .NET Framework 4.7.2+ and .NET 10 (Windows), providing infrastructure for building desktop applications with clean separation of concerns. All projects use SDK-style `.csproj`.

It ships as three packages:
- **`WinformsMVP.Core`** (`netstandard2.0`) — presenters, ViewAction dispatcher, EventAggregator, ChangeTracker, validation, logging and the service interfaces. It has **no WinForms reference**, so presenter code compiled against it cannot touch WinForms types. Bundles the MVP Roslyn analyzers.
- **`WinformsMVP`** (`net472;net10.0-windows`, depends on Core) — the WinForms implementation: `WindowNavigator`, `ViewActionBinder`, dialogs, toasts, `MessageService`, view mapping, `Connect` hosting. The analyzers flow to its consumers transitively.
- **`WinformsMVP.DependencyInjection`** (`net472;net10.0-windows`) — optional M.E.DI integration.

Namespaces are the same in both assemblies (`WinformsMVP.MVP.*`, `WinformsMVP.Services`, `WinformsMVP.Common`, ...).

It implements a **Supervising Controller** variant of MVP with a clear split between Form and UserControl scenarios.

## Build and Test Commands

```bash
# Build
dotnet build winforms-mvp.sln
dotnet build winforms-mvp.sln -c Release
dotnet build src/WinformsMVP/WinformsMVP.csproj

# Test (xUnit; the test project targets net48, so running it needs Windows or mono.
# `dotnet build` works on Linux too thanks to EnableWindowsTargeting)
dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj
dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj -v detailed

# Analyzer tests (net10.0, runs anywhere)
dotnet test tests/WinformsMVP.Analyzers.Tests/WinformsMVP.Analyzers.Tests.csproj

# Run the sample app
dotnet run --project samples/WinformsMVP.Samples/WinformsMVP.Samples.csproj

# Restore
dotnet restore winforms-mvp.sln
```

## MVP Principles — CRITICAL

These rules override default behavior and must be followed when writing or reviewing code.
The full 17-rule guide lives in [wiki/Design-Rules.md](wiki/Design-Rules.md). The non-negotiables:

**1. Presenters must NEVER reference WinForms / UI types.** No `MessageBox.Show()`, `new OpenFileDialog()`, `DialogResult`, `Button`, `TextBox`, etc. in a Presenter. Go through:
- **View interfaces** for displaying data and receiving input.
- **Service interfaces** (`IMessageService`, `IDialogProvider`, `IFileService`, ...) for dialogs/notifications/IO.

```csharp
// ❌ WRONG                              // ✅ CORRECT
MessageBox.Show("Saved!");              Messages.ShowInfo("Saved!");
```

**2. View interfaces expose only data and behavior — never UI controls.**

```csharp
// ❌ WRONG                               // ✅ CORRECT
Button SaveButton { get; }               string Name { get; set; }
TextBox NameTextBox { get; }             bool HasSelection { get; }
                                         void AddItem(string item);
                                         event EventHandler SelectionChanged;
                                         IViewActionBinder ActionBinder { get; }  // framework abstraction (inherited from IWindowView)
```

**3. Presenter does use-case logic only, not view logic.** Presenter owns the business workflow (validate → save → notify); the View decides *how* to display things. All data lives in the Model, not just in UI controls.

**4. Keep the Presenter's public surface minimal (Rules 16 & 17).** A Presenter is not a service.
- `public`: constructor (DI) only. `AttachView` / `Initialize` are explicit `IViewAttacher<TView>` / `IInitializable` implementations, and `_dispatcher` / `_initialized` are private — derived presenters use the `Dispatcher` property.
- `protected override`: lifecycle hooks (`OnInitialize`, `OnViewAttached`, `RegisterViewActions`, `Cleanup`) and close overrides (`CanClose`).
- `private`: action handlers (`OnSave`), helpers.
- A Presenter should expose **zero public events**. For cross-presenter notifications use a Service event or `IEventAggregator`. For push-direction close results, call the base `RequestClose(result, status)` / `RequestClose(status)` — no interface to implement, no extension method.

Subscriptions registered via `.DisposeWith(Disposables)` (event tokens, `Cascade.Bind` unsubscribers, `Disposable.Create(() => view.Event -= handler)` wrappers) are released automatically after `Cleanup()`; presenters normally need no `Cleanup` override for unsubscription.

**Testing rule:** never widen visibility for tests. Drive the Presenter through its real entry points so the same `CanExecute` / close logic runs. `AttachView` / `Initialize` are explicit interface implementations (not callable as `presenter.Initialize()`); tests host the presenter with `WinformsMVP.Testing.PresenterTestHost` (in `WinformsMVP.Core`):
```csharp
var view = new MockEditView();   // any IViewBase/IWindowView mock — no Form needed
// Attaches the view, runs the real init (RegisterViewActions, binder auto-bind, OnInitialize)
// and records RequestClose. Overload: Start(presenter, view, parameters).
using (var host = PresenterTestHost.Start(new EditPresenter(repo), view))
{
    // Action dispatch — Dispatcher stays protected; the mock view exposes a CapturingActionBinder
    // (tests/.../Mocks/CapturingActionBinder.cs) that captures the dispatcher the framework binds,
    // so tests dispatch through the same channel a real Form's bound controls use:
    view.Binder.Dispatch(CommonActions.Save);                 // not presenter.OnSave()

    // Push result — recorded by the host:
    Assert.True(host.CloseRequested);                         // also CloseCount, CloseResult
    Assert.Equal(InteractionStatus.Ok, host.CloseStatus);

    // Pull veto — runs the same gate as X / Alt+F4 (null while an async CanClose hasn't answered):
    Assert.False(host.CanClose());                            // CloseReason.Normal by default
    Assert.True(host.CanClose(CloseReason.SystemShutdown));
}   // Dispose disposes the presenter (Cleanup + Disposables)
```
If initialization throws, `Start` disposes the presenter and rethrows. `host.CanClose` throws for control presenters (they have no close gate).

The framework enforces these rules at compile time via Roslyn analyzers in [src/WinformsMVP.Analyzers/](src/WinformsMVP.Analyzers/).

## Architecture

### Presenter hierarchy

```
PresenterBase<TView>                       [base — do not inherit directly]
├─ WindowPresenterBase<TView>              [Forms, no params]
├─ WindowPresenterBase<TView, TParam>      [Forms, with params]
├─ ControlPresenterBase<TView>             [UserControls, no params]
└─ ControlPresenterBase<TView, TParam>     [UserControls, with params]
```

- **Forms** are created late-bound by `WindowNavigator` (view injected via `IViewAttacher<TView>`).
- **UserControls** are hosted via `presenter.Connect(view[, param])` (two-phase, mirroring `WindowPresenterBase`): the constructor takes only the presenter's own dependencies, then `Connect` attaches the view, initializes, and wires teardown so the presenter disposes with the control. The presenter base carries **no** `System.Windows.Forms` dependency — the lone `view is Control` boundary lives in the internal `ControlLifecycleController`. Because `Connect` runs after construction, `OnInitialize` safely observes constructor-injected fields (no constructor-ordering trap).
- A presenter that returns a result calls the base `RequestClose(result, status)` method directly — no interface to implement, no extension method. The result type is inferred from the argument.
- Parameterized presenters get runtime data through `IInitializable<TParam>.Initialize(param)` — **never** mix DI-resolved deps and runtime args in the constructor; use a Parameters class.
- Misuse fails fast: `Initialize` without an attached view throws `InvalidOperationException`; attaching a second view throws; a control `Connect` that fails disposes the presenter.

Lifecycle (Forms): create presenter → Navigator creates view → `AttachView` → `OnViewAttached` → `RegisterViewActions` → framework auto-binds `View.ActionBinder` → `OnInitialize` → window shown.

See [wiki/Reference-Presenter-Base-Classes.md](wiki/Reference-Presenter-Base-Classes.md).

### ViewAction system

A WPF-`ICommand`-like command/dispatch layer that decouples UI events from presenter logic.

- **`ViewAction`** — immutable action key via `ViewAction.Create("Scope.Name")`. Define them in static classes (e.g. `CommonActions.Save`), never raw strings.
- **`ViewActionBinder`** — maps UI controls to action keys; the Form configures it in `InitializeActionBindings()` and exposes it via the `ActionBinder` **property** (a property, not a method — accessing it must never run business logic).
- **`ViewActionDispatcher`** — routes actions to handlers registered in `RegisterViewActions()`, with optional `canExecute` predicates that drive control enable/disable.

The Presenter registers handlers; it never references controls:
```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(CommonActions.Delete, OnDelete, canExecute: () => View.HasSelection);
    // Framework auto-calls View.ActionBinder?.Bind(Dispatcher) after this method returns.
}
```
Call `Dispatcher.RaiseCanExecuteChanged()` when state changes outside an action (selection, async completion).

By default a dispatch to an unregistered key or with a mismatched payload is logged and ignored (graceful degradation). Set `Dispatcher.ValidationMode = DispatchValidationMode.Strict` — wire it by registering an `IDispatcherConfigurer` (e.g. `ActionDispatcherConfigurer`) in Debug builds — to make those wiring mistakes (forgotten `Register`, typo'd key, wrong payload type) throw instead, so they surface on first dispatch. Strict mode does not affect handler/`CanExecute` exceptions (always caught/logged) or disabled actions.

Always-on guards: `default(ViewAction)` is rejected by `Register` / `Binder.Add`, null handlers are rejected, and binding one control to two actions throws at `Add`. A throwing handler's exception is set on `DispatchContext.Exception` as it leaves the handler (outer middleware sees it in `finally`), and `CanExecuteChanged` is raised so control states refresh.

**Two handling patterns** (details in [wiki/Reference-ViewAction-System.md](wiki/Reference-ViewAction-System.md)):
- **Implicit (recommended):** `ActionBinder` returns the binder; framework auto-binds and auto-updates `CanExecute`. Less code.
- **Explicit:** `ActionBinder` returns `null` (prevents auto-binding/double-dispatch); View raises an `ActionRequest` event, Presenter subscribes with `View.ActionRequest += OnViewActionTriggered;` and manually calls `RaiseCanExecuteChanged()`.

**Middleware (advanced):** `Dispatcher.Use(...)` adds an opt-in onion pipeline (audit, perf, error dialogs). Global tier (registered `IDispatcherConfigurer` in the service provider) runs outermost; per-presenter tier in `RegisterViewActions` runs inside. Zero overhead until `Use(...)` is called. See the ViewAction wiki page and [samples/WinformsMVP.Samples/ViewActionMiddlewareExample.cs](samples/WinformsMVP.Samples/ViewActionMiddlewareExample.cs).

Samples: `ViewActionExample.cs` (implicit), `ViewActionExplicitEventExample.cs` (explicit), `ViewActionWithParametersExample.cs`, `ViewActionStateChangedExample.cs`, `CheckBoxDemo/`, `BulkBindingDemo/`.

### Navigation (`IWindowNavigator`)

```csharp
// Modal
var r = navigator.ShowWindowAsModal<TPresenter, TResult>(presenter);
var r = navigator.ShowWindowAsModal<TPresenter, TParam, TResult>(presenter, parameters);
// Non-modal (keySelector → singleton-per-key)
var w = navigator.ShowWindow<TPresenter>(presenter, keySelector: p => documentId);
```

Fluent API infers all type args except `TResult`:
```csharp
var ok = Navigator.For(presenter).WithParam(parameters).ShowAsModal<bool>();
```
`WithParam` tightens the constraint so passing a param the presenter can't consume fails to compile. A presenter that implements only `IInitializable<TParam>` (no parameterless initialization) shown through a parameterless overload throws `InvalidOperationException` instead of opening uninitialized. See [wiki/Reference-WindowNavigator.md](wiki/Reference-WindowNavigator.md).

### Window closing — two-direction model

Closing policy lives in the Presenter; Forms write zero closing code.

| Direction | Initiator | Mechanism | Trigger |
|-----------|-----------|-----------|---------|
| **Pull**  | Framework | `protected virtual bool CanClose(CloseReason reason)` override | X / Alt+F4 / shutdown |
| **Push**  | Presenter | base `RequestClose(result, status)` / `RequestClose(status)` method (sink injected by framework) | user clicked Save/Cancel |

**Pull (veto a close):** override `CanClose(CloseReason reason)` — return `false` to block, `true` to allow. The framework calls it via `ICloseParticipant.CanCloseGate` before the window closes. For decisions that need a callback (async save, server check), override the two-argument form instead:
```csharp
protected override void CanClose(CloseReason reason, Action<bool> proceed)
{
    // Call proceed(true/false), possibly from a continuation.
    // Action<bool> — never Task — keeps the API free of a Task dependency.
    // proceed may be called from any thread; the framework marshals to the UI thread.
}
```
Never block `CloseReason.SystemShutdown` or `CloseReason.TaskManager` (return `true` immediately for those). While an async decision is pending, repeat close attempts are held off (not re-asked), but system shutdown / Task Manager closes go through; an async allow re-issues owner / MDI-parent / `Application.Exit` closes that were held.

**Push (actively close, optionally with result):** call the base `RequestClose(result, InteractionStatus.Ok)` — `TResult` is inferred from the argument, no interface to implement. For a cancel or no-result close: `RequestClose(InteractionStatus.Cancel)` (C# resolves to the no-result overload; a window whose result type is literally `InteractionStatus` would disambiguate with `RequestClose<InteractionStatus>(x)`). `RequestClose(message, InteractionStatus.Error)` / `RequestClose(exception, InteractionStatus.Error)` carry the message/exception into `InteractionResult.ErrorMessage`/`Exception`. A result of the wrong type, or no result when `TResult` is a non-nullable value type, yields `InteractionStatus.Error` (not `Ok(default)`). `RequestClose` called before the framework binds the sink is held and delivered on bind.

**Invariant:** dirty-state prompts live ONLY in `CanClose` (Pull). Push handlers finalize the dirty flag (`AcceptChanges`/`RejectChanges`) **before** calling `RequestClose`, so the framework's follow-up `CanClose` call sees clean state and does not re-prompt. WinForms types are mapped to the framework's `CloseReason` once inside the internal `CloseReasonMap` and never leak to presenters.

**Hosting — two ways (single-owner rule: use one or the other, never both):**
- **Managed** — `WindowNavigator` creates and shows the window (`ShowWindowAsModal<...>` returns `InteractionResult<TResult>`; `ShowWindow<...>` non-modal). This is the default for all MVP-native windows.
- **Adopted** — for a Form you create and `Show`/`Application.Run` yourself (shell window, legacy migration): call `presenter.Connect(form)` (no result) or `presenter.Connect<TView, TResult>(form, onClosed)` (typed callback) or `presenter.Connect<TView, TParam>(form, param)`. This does attach + initialize + close wiring (it also initializes a presenter that was attached but not yet initialized). Connecting a different view than the attached one, or connecting twice, throws (the live presenter is not disposed); a failed attach/initialize disposes the presenter.

Forms need no closing code — `IWindowView` has no closing members. See [wiki/Concept-Window-Closing-Model.md](wiki/Concept-Window-Closing-Model.md), [wiki/HowTo-Handle-Window-Closing.md](wiki/HowTo-Handle-Window-Closing.md), `samples/.../WindowClosingDemo/`, and `tests/.../Presenters/CanCloseTests.cs`.

### View mapping (`IViewMappingRegister`)

Maps a View interface to its Form implementation so `WindowNavigator` can create instances.

```csharp
register.Register<ISimpleDialogView, SimpleDialogForm>();              // manual
register.RegisterFromAssembly(Assembly.GetExecutingAssembly());        // auto-scan (recommended)
register.RegisterFromNamespace(asm, "MyApp.Dialogs");                  // namespace-scoped
register.Register<IComplexView>(() => new ComplexForm(settings));      // factory (ctor params)
register.Register<ISimpleDialogView, MockForm>(allowOverride: true);   // test override
```

Auto-scan requires: inherits `Form`, implements an interface extending `IWindowView`/`IViewBase`, public parameterless ctor, not abstract. See [wiki/Reference-ViewMappingRegister.md](wiki/Reference-ViewMappingRegister.md).

### Services & platform access

All presenters have built-in access to framework services via convenience properties — no constructor injection needed for these. They resolve through `ServiceLocator.Current` (`IServiceProvider`) on first access:

- `Messages` — `IMessageService` (`ShowInfo/ShowWarning/ShowError/ConfirmYesNo`, `ShowToast(text, type, duration)`). The `ShowToast(text, type, ToastOptions)` overload is an extension method (`MessageServiceToastExtensions`, `WinformsMVP` package); call sites compile unchanged when `WinformsMVP` is referenced. `ShowToast` is safe from worker threads.
- `Dialogs` — `IDialogProvider` (open/save file, folder browser, color). Font and print dialogs (`ShowFontDialog`, `ShowPrintDialog`, `ShowPageSetupDialog`, `ShowPrintPreviewDialog`) return GDI+ types, so they live on `IWinFormsDialogProvider : IDialogProvider` in the `WinformsMVP` package (registered by default by `ServiceLocator` — same `DialogProvider` instance — and by `AddWinformsMVP`) for view-side code.
- `Files` — `IFileService`
- `Navigator` — `IWindowNavigator`
- `Logger` — `ILogger`
- Anchored (positioned) feedback is a **View-layer concern**: View code uses the `AnchoredToast` / `AnchoredMessageBox` static utilities directly. A Presenter that needs position-meaningful feedback calls a small **semantic view method** (e.g. `View.ConfirmDelete()`) whose Form implementation picks the anchor — explicit and correct for every input kind. Default presenter feedback is `Messages` (corner toast / centered dialogs), which is right regardless of how the action was triggered. See `samples/.../AnchoredMessageDemo/`.

Business-specific services (`IUserRepository`, etc.) should be injected via the constructor, not resolved through the service locator.

See [wiki/Reference-Platform-Services.md](wiki/Reference-Platform-Services.md).

### Dependency injection — three patterns

1. **Service Locator** — framework services resolve through `ServiceLocator.Current` (`IServiceProvider`); use the presenter convenience properties (`Messages`, `Dialogs`, etc.) with no constructor injection. Best for simple presenters / legacy migration. Configure at startup via `ServiceLocator.Configure(reg => ...)` or by assigning `ServiceLocator.Current` directly. The default provider picks up the WinForms built-ins (`MessageService`, `DialogProvider`, `WindowNavigator`, ...) from the `WinformsMVP` assembly at runtime through an internal hook; with only `WinformsMVP.Core` deployed you get `FileService` + `NullLoggerFactory`.
2. **Constructor Injection** — inject business services (`IUserRepository`, ...) explicitly. Best for testable production code. Keep DI-resolved deps and runtime args separate (use `IInitializable<TParam>` for the latter).
3. **Hybrid** — business services in the ctor, framework services via the convenience properties. Best for most apps.

**DI-free modular registration:** no framework interface needed — a module is just a static `Register(IServiceRegistry)` method the app calls inside `ServiceLocator.Configure(reg => { BillingModule.Register(reg); ... })`. `IServiceRegistry` offers `RegisterInstance<T>` and `RegisterFactory<T>`. (A polymorphic module interface exists only on the M.E.DI path: `IModuleRegistrar`, consumed by `RegisterModules`.)

**Global dispatcher middleware:** register an `IDispatcherConfigurer` (e.g. `new ActionDispatcherConfigurer(d => d.Use(...))`) in the service provider; the framework picks it up on each presenter's first dispatch. No extra configure call needed.

**M.E.DI integration (`WinformsMVP.DependencyInjection`, net472 / net10.0-windows):** `services.AddWinformsMVP(viewRegistry)` wires all framework defaults using `TryAdd` so host registrations take precedence (it throws if a *different* `IViewMappingRegister` is already registered). `provider.UseWinformsMVP()` sets `ServiceLocator.Current` to the built container. `IPresenterFactory.Create<T>()` resolves child-presenter ctor deps, each presenter in its own `IServiceScope` disposed with the presenter (scoped services are per presenter/window); `IModuleRegistrar` (`WinformsMVP.DependencyInjection`) lets each UI module own its registrations. View resolution (`IViewMappingRegister`) and service resolution (`IServiceProvider`) stay separate concerns — you need both. See [wiki/Reference-DependencyInjection.md](wiki/Reference-DependencyInjection.md).

### Logging

In-house BCL-only abstraction in the `WinformsMVP.Logging` namespace (in `WinformsMVP.Core`) so the core package takes no logging dependency (its only package dependency is `System.ComponentModel.Annotations`, for DataAnnotations on netstandard2.0). Method names and `{Named}` placeholders mirror Microsoft.Extensions.Logging, so migration is mechanical.

- Default: `NullLoggerFactory` (silent, zero cost). `DebugLoggerFactory` writes via `Trace.WriteLine` (VS Output window / any trace listener; works in Release builds). `{@Name}` / `{$Name}` placeholders are accepted, and a repeated name reuses its argument.
- To use the M.E.L. ecosystem (Serilog/Seq/App Insights) on any supported target, write a ~30-line adapter (`AsFrameworkLoggerFactory()`) — see [samples/MultiProjectDemo.Shell/Logging/](samples/MultiProjectDemo.Shell/Logging/).

See [wiki/Reference-Logging.md](wiki/Reference-Logging.md).

### Error handling

Layers, in order of preference:
1. **`IMessageService`** for all user-facing messages (never `MessageBox.Show()`).
2. **`InteractionResult<T>`** for failable operations (`IsOk`/`IsCancelled`/`IsError`) instead of exception-based control flow.
3. **`DialogDefaults`** for centralized/localizable default messages.
4. **Global exception handler** (`Application.ThreadException`, `AppDomain.UnhandledException`) for production crash reporting.

Validate early; catch specific exceptions before generic ones; never silently swallow. See [wiki/HowTo-Handle-Errors.md](wiki/HowTo-Handle-Errors.md).

### Change tracking (`ChangeTracker<T>`)

`where T : class`; `ICloneable` is optional. Snapshot resolution: `ICloneable.Clone()` → `ChangeTrackerDefaults.Cloner` (default reflection deep copy). Comparison: ctor `comparer` → `ChangeTrackerDefaults.Comparer` (default `ObjectComparer.ContentEquals`). `T`'s own `Equals`/`IEquatable` is deliberately ignored — entity equality is usually "same Id", which would hide edits and skip the save prompt; pass a comparer to opt in. `ContentEquals` trusts `Equals` only for value types and `System.*`/`Microsoft.*` types and compares application reference types field by field at every depth (`ObjectComparer.DeepEquals` still respects every `Equals`). Plug a third-party deep-cloner once at startup via `ChangeTrackerDefaults.Cloner`.

Key API: `CurrentValue`, `IsChanged` (computed on each read — in-place edits through `CurrentValue` are seen), `AcceptChanges()`, `RejectChanges()`, `IsChangedChanged` event (raised outside the internal lock). Used in the window-closing dirty-check pattern. See [wiki/Reference-ChangeTracker.md](wiki/Reference-ChangeTracker.md).

### Event aggregator (`IEventAggregator`)

Thread-safe pub/sub for **cross-presenter** communication: automatic UI-thread marshaling, exception isolation. Subscriptions are held **strongly** until the returned `IDisposable` token is disposed — no weak references, no GC auto-cleanup — so any handler shape (method group, capturing lambda, capturing filter) works; a token that is never disposed keeps its handler (and the presenter it captures) alive for the aggregator's lifetime. Pattern: `_events.Subscribe<T>(handler).DisposeWith(Disposables);`. Analyzer **MVP017** warns when a `Subscribe(...)` result is discarded as a bare statement (`_ = ...`, storing, chaining, or `using` are accepted). Create the aggregator on the UI thread so handlers marshal there; if it is created without a `SynchronizationContext` (e.g. in `Program.Main` before `Application.Run`), each subscription uses the subscriber's context instead. A subscription disposed while its message is queued receives nothing.

Use for cross-presenter/cross-module events. Do **not** use for parent-child coordination (call methods directly) or shared state (use a service with events). Register subscriptions with `.DisposeWith(Disposables)` at the creation line (released automatically after `Cleanup()`; no `Cleanup` override needed). See [wiki/Reference-EventAggregator.md](wiki/Reference-EventAggregator.md) and `samples/.../ComplexInteractionDemo_EventBased/`.

## Project Structure

```
/
├── src/        Core libraries (shippable)
│   ├── WinformsMVP.Core/              Platform-neutral core (netstandard2.0, no WinForms reference)
│   │   ├── MVP/Presenters|Views|Models|ViewActions/   presenter bases, ViewAction dispatcher
│   │   ├── Services/                  Service interfaces, ServiceLocator, FileService
│   │   ├── Common/                    EventAggregator, ChangeTracker, validation, utilities
│   │   └── Logging/                   In-house logging abstraction
│   ├── WinformsMVP/                   WinForms implementation (net472;net10.0-windows, depends on Core)
│   │   ├── MVP/                       ViewActionBinder, Connect hosting extensions
│   │   ├── Services/                  WindowNavigator, MessageService, DialogProvider, toasts, view mapping
│   │   └── Common/                    Toast options/renderers, control binding helpers
│   ├── WinformsMVP.DependencyInjection/   Optional M.E.DI integration (net472;net10.0-windows)
│   └── WinformsMVP.Analyzers/             Roslyn analyzers (in the .sln, build-only — bundled into WinformsMVP.Core package)
├── samples/    Sample/demo apps (WinformsMVP.Samples, MultiProjectDemo.*)
├── tests/      WinformsMVP.Samples.Tests (xUnit, net48), WinformsMVP.Analyzers.Tests (xUnit, net10.0)
├── docs/  wiki/  tools/
└── winforms-mvp.sln
```

`src/` holds only shippable libraries; samples and tests are siblings outside it.

## Development Notes

- **Target frameworks:** `WinformsMVP.Core` → `netstandard2.0`; `WinformsMVP` / `WinformsMVP.DependencyInjection` → `net472;net10.0-windows`; samples and tests → `net48`. Minimum .NET Framework is 4.7.2; net40 is no longer supported. SDK-style gives automatic file inclusion and `PackageReference`; `EnableWindowsTargeting` (in `Directory.Build.props`) lets `dotnet build` run on non-Windows hosts.
- **Where code goes:** anything a presenter needs goes in `WinformsMVP.Core` and must not reference WinForms / System.Drawing; WinForms implementations go in `WinformsMVP`.
- **Tests:** xUnit 2.9.3. `WinformsMVP.Samples.Tests` targets net48, so `dotnet test` on it needs Windows (or mono); `WinformsMVP.Analyzers.Tests` (net10.0) runs anywhere.
- **Adding a Form/Dialog:** define `IXxxView : IWindowView` → presenter extends `WindowPresenterBase<TView>[, TParam]` → implement the Form → register the mapping → show via `WindowNavigator`.
- **Adding a UserControl:** define `IXxxView : IViewBase` → presenter extends `ControlPresenterBase<TView>[, TParam]` → implement the UserControl → in the parent, `new` the presenter (its own deps only) then `presenter.Connect(view[, param])`.
- The `wiki/` folder mirrors every subsystem above with Concept / HowTo / Reference / Tutorial pages — consult it before duplicating explanation here.

## Releasing

Every push to `master` and every pull request runs `.github/workflows/ci.yml` (Windows: build + both test projects).

Releases are **tag-driven**. Pushing a tag matching `v*` triggers `.github/workflows/release.yml`, which builds, runs the test suite (gate), packs the three shippable packages (`WinformsMVP.Core`, `WinformsMVP`, `WinformsMVP.DependencyInjection`), asserts the analyzer is bundled in `WinformsMVP.Core`, publishes to **GitHub Packages**, and creates a **GitHub Release** with the `.nupkg` files attached.

```bash
git tag v1.0.0-preview.1 && git push origin v1.0.0-preview.1   # preview
git tag v1.0.0           && git push origin v1.0.0             # stable
```

- The package version is the tag minus the leading `v`. There is no version in any `.csproj` — **never** hardcode one; pass it only via the tag (the workflow injects `-p:Version=`).
- A tag containing `-` (e.g. `-preview.1`) → NuGet prerelease (hidden by default) **and** the workflow passes `--prerelease` to mark the GitHub Release as pre-release. A clean tag → stable.
- Only `WinformsMVP.Core`, `WinformsMVP` and `WinformsMVP.DependencyInjection` publish; everything else is `IsPackable=false` via `Directory.Build.props`. The analyzer ships **inside** `WinformsMVP.Core` (`analyzers/dotnet/cs/`), not standalone, and flows transitively to `WinformsMVP` consumers.
- GitHub Packages requires consumer authentication (a PAT) even for public repos; the auth-free fallback is downloading the `.nupkg` from the Release. Both routes are documented for consumers in `README.md`.
- Full maintainer guide (workflow internals, first-time setup, consuming, troubleshooting, cleanup): [wiki/HowTo-Release.md](wiki/HowTo-Release.md).
