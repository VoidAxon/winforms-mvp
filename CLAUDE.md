# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A WinForms MVP (Model-View-Presenter) framework for .NET Framework 4.8 (the core library also multi-targets `net40`), providing infrastructure for building desktop applications with clean separation of concerns. All projects use SDK-style `.csproj`.

It implements a **Supervising Controller** variant of MVP with a clear split between Form and UserControl scenarios.

## Build and Test Commands

```bash
# Build
dotnet build winforms-mvp.sln
dotnet build winforms-mvp.sln -c Release
dotnet build src/WinformsMVP/WinformsMVP.csproj

# Test (xUnit)
dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj
dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj -v detailed

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
                                         ViewActionBinder ActionBinder { get; }  // framework abstraction
```

**3. Presenter does use-case logic only, not view logic.** Presenter owns the business workflow (validate → save → notify); the View decides *how* to display things. All data lives in the Model, not just in UI controls.

**4. Keep the Presenter's public surface minimal (Rules 16 & 17).** A Presenter is not a service.
- `public`: constructor (DI) and interface-contract members (e.g. `IRequestClose<T>.CloseRequested`) only.
- `protected override`: lifecycle hooks (`OnInitialize`, `OnViewAttached`, `RegisterViewActions`, `Cleanup`).
- `private`: action handlers (`OnSave`), view-event handlers (`OnViewClosing`), helpers.
- A Presenter should expose **zero public events** unless it implements `IRequestClose<TResult>`. For other notifications use a Service event or `IEventAggregator`.

**Testing rule:** never widen visibility for tests. Drive the Presenter through its real entry points so the same `CanExecute` / close logic runs:
```csharp
presenter.Dispatcher.Dispatch(CommonActions.Save);   // not presenter.OnSave()
view.RaiseClosing(CloseReason.Normal);                // pull-direction close
presenter.CloseRequested += (s, e) => captured = e;   // push-direction result
```

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
- **UserControls** receive their view (and optional params) via the presenter constructor and dispose with the control.
- A presenter that returns a result implements `IRequestClose<TResult>` (one event + a tiny `RaiseClose` helper).
- Parameterized presenters get runtime data through `IInitializable<TParam>.Initialize(param)` — **never** mix DI-resolved deps and runtime args in the constructor; use a Parameters class.

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
    Dispatcher.Register(CommonActions.Save, OnSave, canExecute: () => View.HasUnsavedChanges);
    // Framework auto-calls View.ActionBinder?.Bind(_dispatcher) after this method returns.
}
```
Call `Dispatcher.RaiseCanExecuteChanged()` when state changes outside an action (selection, async completion).

**Two handling patterns** (details in [wiki/Reference-ViewAction-System.md](wiki/Reference-ViewAction-System.md)):
- **Implicit (recommended):** `ActionBinder` returns the binder; framework auto-binds and auto-updates `CanExecute`. Less code.
- **Explicit:** `ActionBinder` returns `null` (prevents auto-binding/double-dispatch); View raises an `ActionRequest` event, Presenter subscribes with `View.ActionRequest += OnViewActionTriggered;` and manually calls `RaiseCanExecuteChanged()`.

**Middleware (advanced):** `Dispatcher.Use(...)` adds an opt-in onion pipeline (audit, perf, error dialogs). Global tier via `PlatformServices.Default` runs outermost; per-presenter tier in `RegisterViewActions` runs inside. Zero overhead until `Use(...)` is called. See the ViewAction wiki page and [samples/WinformsMVP.Samples/ViewActionMiddlewareExample.cs](samples/WinformsMVP.Samples/ViewActionMiddlewareExample.cs).

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
`WithParam` tightens the constraint so passing a param the presenter can't consume fails to compile. See [wiki/Reference-WindowNavigator.md](wiki/Reference-WindowNavigator.md).

### Window closing — two-direction model

Closing is event-driven with a single source of truth (no `CanClose()` method on the Presenter):

| Direction | Initiator | Event | Trigger |
|-----------|-----------|-------|---------|
| **Push**  | Presenter | `IRequestClose<T>.CloseRequested` | user clicked Save/Cancel |
| **Pull**  | Framework | `IWindowView.Closing` (carries `CloseReason` + `Cancel`) | X / Alt+F4 / shutdown |

**Invariant:** dirty-state prompts live ONLY in the Pull handler (`View.Closing`). Push handlers finalize the dirty flag (`AcceptChanges`/`RejectChanges`) **before** calling `RaiseClose`, so the follow-up `FormClosing` sees clean state and doesn't re-prompt. WinForms types are mapped to the framework's `CloseReason` once inside `WindowNavigator.MapCloseReason` and never leak to presenters.

Every Form needs the explicit-interface close boilerplate (because `Form` already has a deprecated `Closing`):
```csharp
private EventHandler<WindowClosingEventArgs> _closing;
event EventHandler<WindowClosingEventArgs> IWindowView.Closing { add => _closing += value; remove => _closing -= value; }
void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
```
See [wiki/Concept-Window-Closing-Model.md](wiki/Concept-Window-Closing-Model.md), [wiki/HowTo-Handle-Window-Closing.md](wiki/HowTo-Handle-Window-Closing.md), `samples/.../WindowClosingDemo/`, and `tests/.../Presenters/WindowClosingTests.cs`.

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

All presenters have built-in access to platform services via convenience properties — no constructor injection needed for these:

- `Messages` — `IMessageService` (`ShowInfo/ShowWarning/ShowError/ConfirmYesNo`, toast)
- `Dialogs` — `IDialogProvider` (open/save file, folder browser, ...)
- `Files` — `IFileService`
- `Navigator` — `IWindowNavigator`
- `Logger` — `ILogger`
- `Platform` — full `ICommonServices` container

See [wiki/Reference-Platform-Services.md](wiki/Reference-Platform-Services.md).

### Dependency injection — three patterns

1. **Service Locator** — use `PlatformServices.Default` via the convenience properties; no ctor. Best for simple presenters / legacy migration.
2. **Constructor Injection** — inject business services (`IUserRepository`, ...) explicitly. Best for testable production code.
3. **Hybrid** — business services in the ctor, platform services via properties. Best for most apps.

Optional `WinformsMVP.DependencyInjection` package bridges `Microsoft.Extensions.DependencyInjection`: `IPresenterFactory.Create<T>()` resolves child-presenter ctor deps, `IModuleRegistrar` lets each UI module own its registrations, `services.AddWinformsMVP(viewRegistry)` wires the framework. View resolution (`IViewMappingRegister`) and service resolution (`IServiceProvider`) stay separate concerns — you need both. See [wiki/Reference-DependencyInjection.md](wiki/Reference-DependencyInjection.md).

### Logging

In-house BCL-only abstraction in the `WinformsMVP.Logging` namespace so the core package has **zero external dependencies** and can target `net40`. Method names and `{Named}` placeholders mirror Microsoft.Extensions.Logging, so migration is mechanical.

- Default: `NullLoggerFactory` (silent, zero cost). `DebugLoggerFactory` writes to the VS Output window (net40-safe).
- To use the M.E.L. ecosystem (Serilog/Seq/App Insights) on **net48 only**, write a ~30-line adapter (`AsFrameworkLoggerFactory()`) — see [samples/MultiProjectDemo.Shell/Logging/](samples/MultiProjectDemo.Shell/Logging/). net40 hosts cannot use M.E.L.

See [wiki/Reference-Logging.md](wiki/Reference-Logging.md).

### Error handling

Layers, in order of preference:
1. **`IMessageService`** for all user-facing messages (never `MessageBox.Show()`).
2. **`InteractionResult<T>`** for failable operations (`IsSuccess`/`IsCancelled`/`IsError`) instead of exception-based control flow.
3. **`DialogDefaults`** for centralized/localizable default messages.
4. **Global exception handler** (`Application.ThreadException`, `AppDomain.UnhandledException`) for production crash reporting.

Validate early; catch specific exceptions before generic ones; never silently swallow. See [wiki/HowTo-Handle-Errors.md](wiki/HowTo-Handle-Errors.md).

### Change tracking (`ChangeTracker<T>`)

`where T : class`; `ICloneable` is optional. Snapshot resolution: `ICloneable.Clone()` → `ChangeTrackerDefaults.Cloner` (default reflection deep copy). Comparison: ctor `comparer` → `IEquatable<T>`/`Equals` → `ChangeTrackerDefaults.Comparer`. Plug a third-party deep-cloner once at startup via `ChangeTrackerDefaults.Cloner`.

Key API: `CurrentValue`, `IsChanged`, `AcceptChanges()`, `RejectChanges()`, `IsChangedChanged` event. Used in the window-closing dirty-check pattern. See [wiki/Reference-ChangeTracker.md](wiki/Reference-ChangeTracker.md).

### Event aggregator (`IEventAggregator`)

Thread-safe pub/sub for **cross-presenter** communication: weak-reference subscriptions (auto-cleanup), automatic UI-thread marshaling, exception isolation, compiled-delegate dispatch. Create it on the UI thread.

Use for cross-presenter/cross-module events. Do **not** use for parent-child coordination (call methods directly) or shared state (use a service with events). Dispose subscriptions in `Cleanup()`. See [wiki/Reference-EventAggregator.md](wiki/Reference-EventAggregator.md) and `samples/.../ComplexInteractionDemo_EventBased/`.

## Project Structure

```
/
├── src/        Core libraries (shippable)
│   ├── WinformsMVP/                   Core framework (net40;net48)
│   │   ├── MVP/Presenters|Views|Models|ViewActions/
│   │   ├── Services/                  Service interfaces + implementations
│   │   └── Common/                    Utilities, events, EventAggregator, ChangeTracker, validation
│   ├── WinformsMVP.DependencyInjection/   Optional M.E.DI integration
│   └── WinformsMVP.Analyzers/             Roslyn analyzers (not in the .sln)
├── samples/    Sample/demo apps (WinformsMVP.Samples, MultiProjectDemo.*)
├── tests/      WinformsMVP.Samples.Tests (xUnit), WinformsMVP.Net40SmokeTest
├── docs/  wiki/  tools/
└── winforms-mvp.sln
```

`src/` holds only shippable libraries; samples and tests are siblings outside it.

## Development Notes

- **Target framework:** all projects target .NET Framework 4.8 via SDK-style projects (`WinformsMVP` core also targets `net40`). SDK-style gives automatic file inclusion and `PackageReference`.
- **Tests:** xUnit 2.9.3.
- **Adding a Form/Dialog:** define `IXxxView : IWindowView` → presenter extends `WindowPresenterBase<TView>[, TParam]` → implement the Form → register the mapping → show via `WindowNavigator`.
- **Adding a UserControl:** define `IXxxView : IViewBase` → presenter extends `ControlPresenterBase<TView>[, TParam]` → implement the UserControl → create the presenter in the parent, passing the view (and params).
- The `wiki/` folder mirrors every subsystem above with Concept / HowTo / Reference / Tutorial pages — consult it before duplicating explanation here.
