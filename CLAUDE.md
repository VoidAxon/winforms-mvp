# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Feature map:** [FEATURES.md](FEATURES.md) lists every feature and helper with its entry point, package and wiki page — read it before searching the source for "is there already something for X?".

## Project Overview

A WinForms MVP (Model-View-Presenter) framework for .NET Framework 4.7.2+ and .NET 10 (Windows), providing infrastructure for building desktop applications with clean separation of concerns. All projects use SDK-style `.csproj`.

It ships as five packages:
- **`Kibinago.Core`** (`netstandard2.0`) — presenters, ViewAction dispatcher, EventAggregator, ChangeTracker, validation, logging and the service interfaces. It has **no WinForms reference**, so presenter code compiled against it cannot touch WinForms types. Bundles the MVP Roslyn analyzers.
- **`Kibinago`** (`net472;net10.0-windows`, depends on Core) — the WinForms implementation: `WindowNavigator`, `ViewActionBinder`, dialogs, toasts, `MessageService`, view mapping, `Connect` hosting. The analyzers flow to its consumers transitively.
- **`Kibinago.DependencyInjection`** (`net472;net10.0-windows`) — optional M.E.DI integration.
- **`Kibinago.Testing`** (`netstandard2.0`, depends on Core; test projects only) — `PresenterTestHost`, `TestServices` (`Create()`, `CreateFakes()`), `CapturingActionBinder` and optional fakes of the presenter-facing services (`FakeMessageService`, `FakeToastService`, `FakeDialogProvider`, `FakeWindowNavigator`, `InMemoryFileService`, `RecordingLoggerFactory`), plus `FakeValidationErrorDisplay` for a mock view implementing `IValidatableView`. No test-framework dependency.
- **`Kibinago.Templates`** (template package, `templates/`) — `dotnet new` templates: `kibinago` (app with service/view registration + main window, `--framework net10.0-windows|net472`, `--kibinago-version`, `--no-restore`), `mvp-window` (`--param-type`, `--validation`, `--edit`, `--namespace`), `mvp-control`, `kibinago-tests` (`--app-project`). Install with `dotnet new install Kibinago.Templates` (or a local `.nupkg` path).

Namespaces are the same in both assemblies; there are ten: `Kibinago`, `Kibinago.Presenters`, `.Views`, `.ViewActions`, `.Services` (service interfaces + their options + default implementations, toasts, view mapping, modules), `.Validation`, `.Events` (`IEventAggregator`, `ValueChanged`/`ValueChangingEventArgs`), `.Common`, `.Configuration`, `.Logging` (plus `Kibinago.DependencyInjection` / `Kibinago.Testing`). `ApiSurfaceTests` pins this list — don't add sub-namespaces such as `.Implementations`.

It implements a **Supervising Controller** variant of MVP with a clear split between Form and UserControl scenarios.

## Build and Test Commands

```bash
# Build
dotnet build Kibinago.sln
dotnet build Kibinago.sln -c Release
dotnet build src/Kibinago/Kibinago.csproj

# Test (xUnit; the test project targets net48, so running it needs Windows or mono.
# `dotnet build` works on Linux too thanks to EnableWindowsTargeting)
dotnet test tests/Kibinago.Samples.Tests/Kibinago.Samples.Tests.csproj
dotnet test tests/Kibinago.Samples.Tests/Kibinago.Samples.Tests.csproj -v detailed

# Analyzer tests (net10.0, runs anywhere)
dotnet test tests/Kibinago.Analyzers.Tests/Kibinago.Analyzers.Tests.csproj

# Templates: pack to a local feed, instantiate every template for both frameworks,
# assert namespaces/variants, build with -warnaserror (RUN_TESTS=1 also runs the generated tests; Windows only)
tests/templates/verify-templates.sh
RUN_TESTS=1 tests/templates/verify-templates.sh

# Run the sample app
dotnet run --project samples/Kibinago.Samples/Kibinago.Samples.csproj

# Restore
dotnet restore Kibinago.sln
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
- `protected override`: lifecycle hooks (`OnInitialize`, `OnInitializeAsync`, `OnViewAttached`, `RegisterViewActions`, `Cleanup`) and close overrides (`CanClose`, `CanCloseAsync`). `protected CancellationToken Lifetime` is cancelled when the presenter is disposed (window closed / control disposed) — pass it to async work that should stop with the presenter.
- `private`: action handlers (`OnSave`), helpers.
- A Presenter should expose **zero public events**. For cross-presenter notifications use a Service event or `IEventAggregator`. For close results, call the base `RequestClose(result, status)` / `RequestClose(status)` (it goes through the presenter's own `CanClose`) — no interface to implement, no extension method.

View events need no hand-written EventArgs class and never a WinForms one (MVP004): `EventHandler` when the presenter reads the value from the view, `EventHandler<T>` with a business type to send it (`event EventHandler<int> RecordActivated;` — `T` need not derive from EventArgs), a tuple for two or three related values (`EventHandler<(int RecordId, string Column)>`), `EventHandler<ValueChangedEventArgs<T>>` (`Kibinago.Events`, `OldValue` / `NewValue`) after a change when the previous value matters, `EventHandler<ValueChangingEventArgs<T>>` (+ `Cancel`) before a change the presenter may veto (the view applies it only when `!e.Cancel`), `EventHandler<CancelEventArgs>` (BCL) for a veto without values.

Subscribe to view events with the protected `SubscribeViewEvent(h => View.X += h, h => View.X -= h, handler)` (and `SubscribeViewEvent<TEventArgs>(..., e => ...)` for `EventHandler<T>`): one line instead of a `+=` plus a matching `-=`, unsubscribed when the presenter is disposed, and silent once disposal has begun (windows dispose the presenter first, then the form, whose controls can still raise events while torn down). Subscriptions registered via `.DisposeWith(Disposables)` (event tokens, `Cascade.Bind` unsubscribers, `Disposable.Create(() => view.Event -= handler)` wrappers) are released automatically after `Cleanup()`; presenters normally need no `Cleanup` override for unsubscription.

**Testing rule:** never widen visibility for tests. Drive the Presenter through its real entry points so the same `CanExecute` / close logic runs. `AttachView` / `Initialize` are explicit interface implementations (not callable as `presenter.Initialize()`); tests host the presenter with `Kibinago.Testing.PresenterTestHost` (in the `Kibinago.Testing` package):
```csharp
var view = new MockEditView();   // any IViewBase/IWindowView mock — no Form needed
// Attaches the view, runs the real init (RegisterViewActions, binder auto-bind, OnInitialize)
// and records RequestClose. Overload: Start(presenter, view, parameters).
using (var host = PresenterTestHost.Start(new EditPresenter(repo), view))
{
    // Action dispatch — Dispatcher stays protected; the mock view exposes a CapturingActionBinder
    // (Kibinago.Testing) as its ActionBinder; it captures the dispatcher the framework binds,
    // so tests dispatch through the same channel a real Form's bound controls use:
    view.Binder.Dispatch(CommonActions.Save);                 // not presenter.OnSave()
    await view.Binder.DispatchAsync(CommonActions.Load);      // async handler: awaits its completion
    await host.Initialized;                                   // waits for OnInitializeAsync

    // RequestClose — runs CanClose like ✕; the host records accepted closes:
    Assert.True(host.CloseRequested);                         // also CloseCount, CloseResult
    Assert.Equal(InteractionStatus.Ok, host.CloseStatus);

    // Pull veto — runs the same gate as X / Alt+F4 (null while CanCloseAsync hasn't answered):
    Assert.False(host.CanClose());                            // CloseReason.Normal by default
    Assert.True(host.CanClose(CloseReason.SystemShutdown));
    Assert.True(await host.CanCloseAsync());                  // awaits an async CanCloseAsync decision
}   // Dispose disposes the presenter (Cleanup + Disposables)
```
If initialization throws, `Start` disposes the presenter and rethrows. `host.CanClose` throws for control presenters (they have no close gate).

Framework services (`Messages`, `Navigator`, `Toasts`, `Logger`, ...) for one presenter under test: pass a provider as the first argument — `PresenterTestHost.Start(services, presenter, view[, parameters])`. Use `TestServices.CreateFakes()` (`fakes.Provider` plus `fakes.Messages` / `Toasts` / `Dialogs` / `Files` / `Navigator` / `Logs`; an unscripted confirmation, dialog or modal throws `UnscriptedInteractionException` unless the fake is switched to a permissive default), or start from `TestServices.Create()` (a `DefaultServiceProvider` with only a silent `ILoggerFactory`, which every presenter needs at attach) and register your own fakes; replace any service in the provider before `Start`; anything missing is an error, never a fallback to the global `ServiceLocator.Current`, so tests do not touch global state and can run in parallel. The provider stays caller-owned. A presenter that already used framework services in its constructor is rejected (move that code to `OnViewAttached` / `OnInitialize`). The binding seam (`IPresenterServicesBinding`) is internal; there is no public per-presenter setter.

The framework enforces these rules at compile time via Roslyn analyzers in [src/Kibinago.Analyzers/](src/Kibinago.Analyzers/).

## Architecture

### Presenter hierarchy

```
PresenterBase<TView>                       [base — cannot be inherited outside the framework]
├─ WindowPresenterBase<TView>              [Forms, no params]
├─ WindowPresenterBase<TView, TParam>      [Forms, with params]
├─ ControlPresenterBase<TView>             [UserControls, no params]
└─ ControlPresenterBase<TView, TParam>     [UserControls, with params]
```

- **Forms** are created late-bound by `WindowNavigator` (view injected via `IViewAttacher<TView>`).
- **UserControls** are hosted via `presenter.Connect(view[, param])` (two-phase, mirroring `WindowPresenterBase`): the constructor takes only the presenter's own dependencies, then `Connect` attaches the view, initializes, and wires teardown so the presenter disposes with the control. The presenter base carries **no** `System.Windows.Forms` dependency — the lone `view is Control` boundary lives in the internal `ControlLifecycleController`. Because `Connect` runs after construction, `OnInitialize` safely observes constructor-injected fields (no constructor-ordering trap). A control disposed while its presenter initializes disposes the presenter (`Connect` throws `ObjectDisposedException`).
- **Child presenters owned by a parent presenter** — when a window (or panel) contains panels whose presenters need business services, the parent **presenter** composes them: the parent view interface exposes the child **view interface** (`ICustomerPanelView CustomerPanel { get; }`, never a control) and the parent calls the protected `AttachChild(new CustomerPanelPresenter(deps), View.CustomerPanel, input)` in `OnInitialize` (dependencies come from the parent's constructor, so a Navigator-created Form needs no factory registration). The child is attached and initialized at once, uses the parent's services unless it has its own, takes no part in close decisions, and is disposed with the parent (after the parent's `Cleanup`, children in reverse order, before its `Disposables`) or when its control is disposed. `AttachChild` returns a handle: dynamic panels (rows, tabs) keep it, keyed by the child view, and dispose it before the view removes the control (`IXxxView AddXxx()` / `RemoveXxx(view)` on the parent view; the view never removes a child on its own). Use `Connect` when a WinForms host owns the presenter (legacy Form, standalone control); one owner per presenter.
- A disposed presenter's actions do nothing: its dispatcher stops (late clicks, in-flight async completions) and the binder the framework bound is unbound (`IViewActionBinder.Unbind()`).
- A presenter that returns a result calls the base `RequestClose(result, status)` method directly — no interface to implement, no extension method. The result type is inferred from the argument.
- Parameterized presenters get runtime data through `IInitializable<TParam>.Initialize(param)` — **never** mix DI-resolved deps and runtime args in the constructor; use a Parameters class.
- Misuse fails fast: `Initialize` without an attached view throws `InvalidOperationException`; attaching a second view throws; a control `Connect` that fails disposes the presenter.

Lifecycle (Forms): create presenter → Navigator creates view → `AttachView` → `OnViewAttached` → `RegisterViewActions` → framework auto-binds `View.ActionBinder` → `OnInitialize` → `OnInitializeAsync(Lifetime)` started (not awaited) → window shown. Override `protected virtual Task OnInitializeAsync(CancellationToken)` to load data without blocking the window; its failures are logged (handle expected errors inside with `Messages`), cancellation by disposal is silent.

See [wiki/Reference-Presenter-Base-Classes.md](wiki/Reference-Presenter-Base-Classes.md).

### ViewAction system

A WPF-`ICommand`-like command/dispatch layer that decouples UI events from presenter logic. It is optional: a simple screen can expose semantic view events (`event EventHandler SaveRequested`) instead; ViewAction pays off on screens with many operations or when `CanExecute` / async / middleware support is wanted.

- **`ViewAction`** — immutable action key. Define them in static classes (e.g. `CommonActions.Save`), never raw strings. A screen's actions class names its scope once — `private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("OrderEdit");` then `Save = Factory.Create(nameof(Save))` (key `"OrderEdit.Save"`); `ViewAction.Create("Scope.Name")` is for a single shared key. Common verbs are shipped in `StandardActions` (`Save`, `Cancel`, `Delete`, `Refresh`, `Ok`, … — keys `"Standard.Save"` etc.); use them instead of redefining. **Typed keys** (optional): `ViewAction<T>` via `Factory.Create<T>(nameof(X))` / `ViewAction.Create<T>("Scope.Name")` carry the payload type, so `binder.Add(key, control, c => T)`, `Dispatcher.Register(key, (T v) => ...)` (sync/async) and `Dispatch(key, T)` are checked at compile time; no implicit conversion to `ViewAction` — use `.Key` for the untyped API.
- **`ViewActionBinder`** — maps UI controls to action keys; the Form configures it in `InitializeActionBindings()` and exposes it via the `ActionBinder` **property** (a property, not a method — accessing it must never run business logic). Built-in triggers: buttons / ToolStrip items `Click`, `CheckBox` / `RadioButton` `CheckedChanged`, `ComboBox` `SelectionChangeCommitted`, `TextBoxBase` `TextChanged` (also on programmatic `Text` — write back only when different), `NumericUpDown` / `DateTimePicker` / `MonthCalendar` value changes, and `Click` for exactly `Label` / `LinkLabel` / `PictureBox` / `Panel` / `FlowLayoutPanel` / `TableLayoutPanel`; any other control (grid, list, `UserControl`, third-party editor) is rejected at `Add` until `RegisterStrategy<T>(...)` names its event. `Add` after `Bind` subscribes at once (Enabled follows `canExecute`); `Remove(controls)` unbinds; a disposed control is removed automatically. A bound button's `DialogResult` is reset to `None` when it is subscribed (both `Bind` overloads, `Add` after `Bind`; not restored on `Unbind`): WinForms copies it to the form before `Click`, so it would close a modal window whatever the handler decides, and `Form.CancelButton` silently sets it to `Cancel` — closing is the presenter's `RequestClose`; set `CancelButton` before `Bind` (Esc still clicks it).
- **`ViewActionDispatcher`** — routes actions to handlers registered in `RegisterViewActions()`, with optional `canExecute` predicates that drive control enable/disable.

The Presenter registers handlers; it never references controls:
```csharp
protected override void RegisterViewActions()
{
    Dispatcher.Register(CommonActions.Delete, OnDelete, canExecute: () => View.HasSelection);
    // Framework auto-calls View.ActionBinder?.Bind(Dispatcher) after this method returns.
}
```
Call `Dispatcher.RaiseCanExecuteChanged()` when state changes outside an action (selection, input changes). The framework re-evaluates `CanExecute` itself after `OnInitialize` and when `OnInitializeAsync` finishes, so data loaded there needs no manual refresh.

**Async handlers:** `Register` has async overloads — `Func<Task>`, `Func<CancellationToken, Task>`, `Func<T, Task>`, `Func<T, CancellationToken, Task>` — so `async` lambdas / `async Task` methods bind to them (never write `async void` handlers):
```csharp
Dispatcher.Register(CommonActions.Refresh, async ct =>
{
    try { View.Items = await _repository.LoadAsync(ct); }
    catch (IOException ex) { Messages.ShowError(ex.Message); }   // expected errors: handle here
});
```
While it runs, the action reports `CanDispatch == false` (bound controls greyed) and repeated triggers are ignored — no busy flag or manual `RaiseCanExecuteChanged()` for that (only to disable *other* actions). `ActionExecuted` fires on completion. An unhandled handler failure is logged, the action is re-enabled, and then the exception **propagates like an ordinary WinForms event handler's**, so `Application.ThreadException` sees it: a synchronous failure is rethrown from `Dispatch` (out of the bound control's `Click`); a failure after an `await` is posted to the `SynchronizationContext` current at dispatch, like `async void`. Handle expected errors inside the handler (`Messages`); set up `Application.ThreadException` (+ `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)`) at startup for the rest — the app template does. The token is `Lifetime` (cancelled when the window closes); that cancellation is silent. `Dispatcher.DispatchAsync(key[, payload])` returns a task that completes when the handler finishes and **faults with the handler's exception** — mainly for tests (`await Assert.ThrowsAsync<...>(() => view.Binder.DispatchAsync(...))`; prefer it to `Dispatch` for async handlers in tests, where a posted failure may not fail the test).

By default a dispatch to an unregistered key or with a mismatched payload is logged and ignored (graceful degradation). Set `Dispatcher.ValidationMode = DispatchValidationMode.Strict` — wire it by registering an `IDispatcherConfigurer` (e.g. `ActionDispatcherConfigurer`) in Debug builds — to make those wiring mistakes (forgotten `Register`, typo'd key, wrong payload type) throw instead, so they surface on first dispatch. Strict mode does not affect handler exceptions (they propagate in both modes), `CanExecute` exceptions (always logged and treated as disabled) or disabled actions.

Always-on guards: `default(ViewAction)` is rejected by `Register` / `Binder.Add`, null handlers are rejected, and binding one control to two actions throws at `Add`. A throwing handler's exception is set on `DispatchContext.Exception` as it leaves the handler (outer middleware sees it in `finally`), and `CanExecuteChanged` is raised so control states refresh. A middleware that **catches** the exception has handled it (it does not propagate; `ctx.Exception` stays a record for auditing); one that rethrows, or throws its own, propagates that.

**Two handling patterns** (details in [wiki/Reference-ViewAction-System.md](wiki/Reference-ViewAction-System.md)):
- **Implicit (recommended):** `ActionBinder` returns the binder; framework auto-binds and auto-updates `CanExecute`. Less code.
- **Explicit (mainly for migrating legacy Forms):** `ActionBinder` returns `null` and the View calls `_binder.Bind()` (event-only) itself, forwarding `ActionTriggered` to its `ActionRequest` event; Presenter subscribes with `View.ActionRequest += OnViewActionTriggered;`. Control Enabled state is not synced in this mode — express availability through semantic view members (e.g. `View.CanDelete`).

Routing is decided by the `Bind` overload, never by listeners: `Bind(dispatcher)` always dispatches and then raises `ActionTriggered` as a notification (safe to use for status bars/logging); `Bind()` only raises `ActionTriggered`.

**Middleware (advanced):** `Dispatcher.Use(...)` adds an opt-in onion pipeline (audit, perf, error dialogs). Global tier (registered `IDispatcherConfigurer` in the service provider) runs outermost; per-presenter tier in `RegisterViewActions` runs inside. Zero overhead until `Use(...)` is called. Middleware returns a `Task` (`Use(async (ctx, next) => { ...; await next(ctx); ... })`, or `IDispatchMiddleware.InvokeAsync`): `await next(ctx)` completes when an async handler has finished, so a middleware times / audits / catches the whole operation (a failure after the handler's `await` arrives there; catching it handles it). `ctx.CancellationToken` is the handler's token — a disposal cancellation arrives as `OperationCanceledException`, not an error. The action stays disabled until the whole pipeline ends; a middleware that does not await `next` still has the handler tracked to its end. See the ViewAction wiki page and [samples/Kibinago.Samples/ViewActionMiddlewareExample.cs](samples/Kibinago.Samples/ViewActionMiddlewareExample.cs).

Samples: `ViewActionExample.cs` (implicit), `ViewActionExplicitEventExample.cs` (explicit), `ViewActionWithParametersExample.cs`, `ViewActionStateChangedExample.cs`, `CheckBoxDemo/`, `BulkBindingDemo/`.

### Navigation (`IWindowNavigator`)

Prefer the fluent form — it infers the presenter and parameter types, so only `TResult` is written:
```csharp
// Modal: returns InteractionResult<TResult> when the window closes
var r = Navigator.For(new CustomerSearchPresenter(repo)).WithParam(keyword).ShowAsModal<Customer>();
// Non-modal: returns at once; owner = owned window, key = one window per key, onClosed = result
Navigator.For(editPresenter).WithParam(parameters)
         .ShowWindow<int>(owner: View, key: id, onClosed: r => { if (r.IsOk) Reload(); });
```
Keys are scoped by the presenter's runtime type (internally `(presenter.GetType(), key)`), so two screens can both key by a record id; the same presenter type with the same key activates the open window; the new presenter is disposed and its `onClosed` is not registered. The direct methods (`ShowWindowAsModal<TPresenter, TParam, TResult>(presenter, parameters)`, `ShowWindow(presenter, key: id)`) do the same with every type argument written out.

Mistakes fail to compile: `For` returns a context that can show the window only for a presenter that initializes without parameters; a presenter that takes parameters gets one whose only step is `WithParam` (of the type it takes); a control presenter matches neither (`IWindowPresenter`). The direct methods carry the same constraints (`IWindowPresenter, IInitializable` / `IInitializable<TParam>`). See [wiki/Reference-WindowNavigator.md](wiki/Reference-WindowNavigator.md).

### Window closing — one gate, two initiators

Closing policy lives in the Presenter; Forms write zero closing code (a 「閉じる」 button is a ViewAction too).

| Initiator | Mechanism | Goes through `CanClose` / `CanCloseAsync`? |
|-----------|-----------|-----------|
| **User / system** | ✕ / Alt+F4 / owner closing / shutdown | yes (shutdown / Task Manager: allowed without asking) |
| **Presenter** | base `RequestClose(result, status)` / `RequestClose(status)` (sink injected by framework) | **yes** once the window is open (`CloseReason.Normal`); during the opening it cancels the opening instead (below) |

**The gate (veto a close):** override `CanClose(CloseReason reason)` — return `false` to block, `true` to allow. The framework calls it via `ICloseParticipant.CanCloseGate` before the window closes. For decisions that must await something (save before close, server check), override `CanCloseAsync` instead (it replaces the removed callback overload `CanClose(reason, Action<bool> proceed)`; its default returns the sync `CanClose(reason)`):
```csharp
protected override async Task<bool> CanCloseAsync(CloseReason reason)
{
    if (!_tracker.IsChanged) return true;
    var answer = Messages.ConfirmYesNoCancel("Save changes?");
    if (answer == ConfirmResult.Cancel) return false;
    if (answer == ConfirmResult.No) return true;
    return await SaveAsync();   // lock input while saving; false (after telling the user) blocks the close
}
```
`CloseReason.SystemShutdown` and `CloseReason.TaskManager` never reach `CanClose` / `CanCloseAsync`: the framework allows them without asking, so presenters write no reason check. During a Windows shutdown this also covers owned / MDI child windows (WinForms tells them `FormOwnerClosing`; the framework recognizes the shutdown). Known limit: when Task Manager closes an owner, its owned children still see `ParentClosing` and may prompt — WinForms exposes no public way to tell that close apart, and a prompt loses nothing. Every other reason — `Normal`, `ParentClosing` (the owner closes), `Unknown` — reaches the presenter and gets the same unsaved-changes prompt; never write `if (reason != CloseReason.Normal) return true;`, which loses an owned child's edits when its owner closes. An owned child's veto cancels the owner's close. While the task is pending the window stays open and repeat close attempts are held off (not re-asked), but system shutdown / Task Manager closes go through; an async allow re-issues owner / MDI-parent / `Application.Exit` closes that were held. A faulted or cancelled `CanCloseAsync` blocks the close (system-level closes still allowed) and is logged. See `samples/.../EmailDemo/ComposeEmailPresenter.cs`.

**Save on close with a result:** to tell the caller that X → "Save?" → Yes saved (refresh a list, return the edited value), call `RequestClose(result, status)` inside `CanClose` / `CanCloseAsync` (after the save) and return `true`. While the gate is deciding, `RequestClose` sets the result of *that* close instead of closing again; if the gate returns `false` the result is dropped and the window stays open. Without it, a gate-driven close reports `Cancel` with no result. A `RequestClose` from elsewhere (e.g. a Save button) while an async gate is pending also waits for the gate's answer. `PresenterTestHost` follows the same rules: a `RequestClose` runs the gate and `CloseRequested` / `CloseResult` / `CloseStatus` describe **accepted** closes (`await host.WaitForCloseDecisionAsync()` waits for an async gate a `RequestClose` started — `DispatchAsync` does not); requests during `Start`'s synchronous part are an accepted cancellation of the opening (no gate); `host.CanClose()` / `CanCloseAsync()` run the gate as ✕ does, and a repeat check while an async gate is pending gets the pending answer without re-asking the presenter (system shutdown / Task Manager return `true`).

**RequestClose (presenter-initiated, optionally with result):** call the base `RequestClose(result, InteractionStatus.Ok)` — `TResult` is inferred from the argument, no interface to implement. For a cancel or no-result close: `RequestClose(InteractionStatus.Cancel)` (C# resolves to the no-result overload; a window whose result type is literally `InteractionStatus` would disambiguate with `RequestClose<InteractionStatus>(x)`). `RequestClose(message, InteractionStatus.Error)` / `RequestClose(exception, InteractionStatus.Error)` carry the message/exception into `InteractionResult.ErrorMessage`/`Exception`. A result of the wrong type, or no result when `TResult` is a non-nullable value type, yields `InteractionStatus.Error` (not `Ok(default)`). On an open window it **requests** the close: it runs the gate like ✕ and supplies the candidate result — a `RequestClose` inside the gate replaces it (last wins), a veto keeps the window open and drops it, an async gate keeps the window open until it answers (further ✕ / `RequestClose` meanwhile do not ask again; a `RequestClose` updates the pending result). So a 「閉じる」 button is `Dispatcher.Register(Actions.Close, () => RequestClose(InteractionStatus.Cancel))` and gets the same 保存しますか as ✕; a gate that refuses while busy (`_isSaving`) also blocks the handler's own `RequestClose` — clear the busy state first.

**Cancelling the opening:** a `RequestClose` before the window starts showing (in `OnInitialize`, the synchronous part of `OnInitializeAsync`, or held before binding) is not a close: no gate (the user cannot have edited anything) and the window is never seen. To tell the user why, show the message first and then cancel — `Messages.ShowWarning("…"); RequestClose(InteractionStatus.Cancel); return;` (the message appears over the caller's window) — or return the reason (`RequestClose(message, InteractionStatus.Error)`) and let the caller show it and react; never decide in the Form's `Load` / `Shown`. `WindowNavigator` does not call `Show` / `ShowDialog` (modal: returns the result; modeless: `ShowWindow` returns `null` after releasing the key and calling `onClosed`). `Connect` (the caller shows the Form) makes the Form invisible in `Load` (opacity 0, moved outside every monitor) and closes it, so the caller's usual `Show` / `Application.Run` shows nothing (measured not to flash). Limits: an MDI child is closed in `Load` without hiding; a Form already shown before `Connect` gets a normal gated close; the `Application.Run` main form ends the application; if another `FormClosing` handler refuses, the Form is restored and shown.

**Invariant:** dirty-state prompts live ONLY in `CanClose` / `CanCloseAsync`. A handler that ends the edit finalizes the dirty state (`AcceptChanges()`, or `AcceptChanges(currentContent)` when the screen builds a new model each time) **before** calling `RequestClose` — Save after saving, an explicit 破棄 after deciding to discard — so the gate sees clean state and closes without asking; a plain 「閉じる」 leaves the state alone and the gate asks. A veto does not undo work already done (e.g. a discard's `AcceptChanges`). WinForms types are mapped to the framework's `CloseReason` once inside the internal `CloseReasonMap` and never leak to presenters.

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

- `Messages` — `IMessageService` (`ShowInfo/ShowWarning/ShowError/ConfirmYesNo/ConfirmOkCancel/ConfirmYesNoCancel`), modal dialogs only. Called from a worker thread (inside `Task.Run`), the dialog is shown on the UI thread (through an open form, `Invoke`) and the worker waits for the answer — never block the UI thread on such a worker; a call on a thread that runs a message loop is shown there. No async confirm API (a design choice, see wiki/FAQ.md): the synchronous dialog already pumps messages (painting, timers, posted continuations run; the code after the call waits; modally disabled windows cannot be used), so async would not help responsiveness — its gain is composing the caller's flow — while a synchronous answer fits `CanClose` and keeps presenters and tests simple. WinForms' `ShowDialogAsync` posts the same `ShowDialog` to the calling thread's context (never call it from a worker). Watch for reentrancy while a dialog is open. Every message box (and `AnchoredMessageBox`) is shown by the registered `IMessageDialogBackend` — replace that one-method interface to restyle all dialogs.
- `Toasts` — `IToastService` (`Show(text, type, int? duration = null)`; null = the configured default). Safe from worker threads: toasts are always created on the UI thread (dropped and logged when there is none). View code that needs per-call `ToastOptions` or an anchor uses `IWinFormsToastService` (`Show(text, type, options)`, `ShowAnchored`); `AnchoredToast` forwards to it.
- `Dialogs` — `IDialogProvider` (open/save file, folder browser, color). Font and print dialogs (`ShowFontDialog`, `ShowPrintDialog`, `ShowPageSetupDialog`, `ShowPrintPreviewDialog`) return GDI+ types, so they live on `IWinFormsDialogProvider : IDialogProvider` in the `Kibinago` package (registered by default by `ServiceLocator` — same `DialogProvider` instance — and by `AddKibinago`) for view-side code.
- `Files` — `IFileService`
- `Navigator` — `IWindowNavigator`
- `Logger` — `ILogger`
- Anchored (positioned) feedback is a **View-layer concern**: View code uses the `AnchoredToast` / `AnchoredMessageBox` static utilities directly. A Presenter that needs position-meaningful feedback calls a small **semantic view method** (e.g. `View.ConfirmDelete()`) whose Form implementation picks the anchor — explicit and correct for every input kind. Default presenter feedback is `Toasts` / `Messages` (corner toast / centered dialogs), which is right regardless of how the action was triggered. See `samples/.../AnchoredMessageDemo/`.

Business-specific services (`IUserRepository`, etc.) should be injected via the constructor, not resolved through the service locator.

See [wiki/Reference-Platform-Services.md](wiki/Reference-Platform-Services.md).

### Configuring the framework — one startup call

All framework customization happens once at startup, in one place; there are no mutable static `*Defaults` classes (`ChangeTrackerDefaults`, `ToastDefaults`, `DialogDefaults` were removed — don't add new ones):

```csharp
MvpApplication.Configure(                       // Kibinago package; installs ServiceLocator.Current
    framework: mvp =>                           // KibinagoBuilder — type "mvp." to see what is replaceable
    {
        mvp.Messages.UseDialogs(new CompanyStyleDialogBackend());   // IMessageDialogBackend
        mvp.Toasts.Duration = 4500;                                  // ToastHostOptions
        mvp.ChangeTracking.UseComparer(new CompareNetObjectsComparer());
    },
    services: reg => BillingModule.Register(reg));                   // business services
// M.E.DI: services.AddKibinago(viewRegistry, mvp => { ...same... });
```

Groups: `Messages` (DefaultCaption, UseDialogs), `Toasts` (ToastHostOptions properties, UseRenderer, Use), `Dialogs` (DialogProviderOptions properties), `ChangeTracking` (UseCloner, UseComparer, `For<T>(policy)`), `Events` (Use), `Logging` (Use), `Actions` (Configure, StrictValidation). Each `UseXxx(instance)` has a `UseXxx(sp => ...)` factory overload. **Precedence:** a service registered directly (`services:` block / M.E.DI) > the `mvp.` choice > built-in default; a directly registered options object replaces the builder's wholesale (no merge). The builder only produces registrations; the runtime reads the container only. Services take their options/dependencies in the constructor and snapshot them. `ServiceLocator.Configure(reg => ...)` remains as the lower-level entry (defaults only). Instances passed in (`UseXxx(instance)`, `RegisterInstance`) stay caller-owned; `DefaultServiceProvider` disposes only the singletons its factories created, and `ServiceLocator` disposes only providers it built itself (lazy default, `Configure`/`MvpApplication`) when they are replaced or `Reset`; `MvpApplication.Run(mainForm)` (Application.Run, then `MvpApplication.Shutdown()`) disposes that root at exit — an M.E.DI container is disposed by whoever built it. See [wiki/HowTo-Customize-Framework.md](wiki/HowTo-Customize-Framework.md) and `samples/.../CustomizationDemo/`.

### Dependency injection — three patterns

1. **Service Locator** — framework services resolve through `ServiceLocator.Current` (`IServiceProvider`); use the presenter convenience properties (`Messages`, `Dialogs`, etc.) with no constructor injection. Best for simple presenters / legacy migration. Configure at startup via `MvpApplication.Configure(framework:, services:)` (or the lower-level `ServiceLocator.Configure(reg => ...)`, or by assigning `ServiceLocator.Current` directly). The default provider picks up the WinForms built-ins (`MessageService`, `WinFormsToastService`, `DialogProvider`, `WindowNavigator`, ...) from the `Kibinago` assembly at runtime through an internal hook; with only `Kibinago.Core` deployed you get `FileService`, `NullLoggerFactory`, the change-tracking engines and an `EventAggregator`.
2. **Constructor Injection** — inject business services (`IUserRepository`, ...) explicitly. Best for testable production code. Keep DI-resolved deps and runtime args separate (use `IInitializable<TParam>` for the latter).
3. **Hybrid** — business services in the ctor, framework services via the convenience properties. Best for most apps.

**DI-free modular registration:** no framework interface needed — a module is just a static `Register(IServiceRegistry)` method the app calls inside the `services:` block (`MvpApplication.Configure(services: reg => { BillingModule.Register(reg); ... })`). `IServiceRegistry` offers `RegisterInstance<T>` and `RegisterSingleton<T>`. (A polymorphic module interface exists only on the M.E.DI path: `IModuleRegistrar`, consumed by `RegisterModules`.)

**Global dispatcher middleware:** `mvp.Actions.Configure(d => d.Use(...))` / `mvp.Actions.StrictValidation()` (or register an `IDispatcherConfigurer` directly); the framework picks it up on each presenter's first dispatch.

**M.E.DI integration (`Kibinago.DependencyInjection`, net472 / net10.0-windows):** `services.AddKibinago(viewRegistry[, mvp => ...])` wires all framework defaults (with the same `KibinagoBuilder` customization as `MvpApplication.Configure`) using `TryAdd` so host registrations take precedence (it throws if a *different* `IViewMappingRegister` is already registered). `provider.UseKibinago()` sets `ServiceLocator.Current` to the built container. `IPresenterFactory.Create<T>()` resolves child-presenter ctor deps, each presenter in its own `IServiceScope` disposed with the presenter (scoped services are per presenter/window), and its framework-service properties (`Messages`, `Logger`, ...) resolve from that same scope, not `ServiceLocator.Current` (register them with `AddKibinago`; a presenter that uses them in its constructor is rejected by `Create`); `IModuleRegistrar` (`Kibinago.DependencyInjection`) lets each UI module own its registrations. View resolution (`IViewMappingRegister`) and service resolution (`IServiceProvider`) stay separate concerns — you need both. See [wiki/Reference-DependencyInjection.md](wiki/Reference-DependencyInjection.md).

### Logging

In-house BCL-only abstraction in the `Kibinago.Logging` namespace (in `Kibinago.Core`) so the core package takes no logging dependency (its only package dependency is `System.ComponentModel.Annotations`, for DataAnnotations on netstandard2.0). Method names and `{Named}` placeholders mirror Microsoft.Extensions.Logging, so migration is mechanical.

- Default: `NullLoggerFactory` (silent, zero cost). `DebugLoggerFactory` writes via `Trace.WriteLine` (VS Output window / any trace listener; works in Release builds). `{@Name}` / `{$Name}` placeholders are accepted, and a repeated name reuses its argument.
- To use the M.E.L. ecosystem (Serilog/Seq/App Insights) on any supported target, write a ~30-line adapter (`AsFrameworkLoggerFactory()`) — see [samples/MultiProjectDemo.Shell/Logging/](samples/MultiProjectDemo.Shell/Logging/).

See [wiki/Reference-Logging.md](wiki/Reference-Logging.md).

### Error handling

Layers, in order of preference:
1. **`IMessageService`** for all user-facing messages (never `MessageBox.Show()`).
2. **`InteractionResult<T>`** for failable operations (`IsOk`/`IsCancelled`/`IsError`) instead of exception-based control flow.
3. **`mvp.Messages` / `mvp.Dialogs`** (`MessageDialogOptions`, `DialogProviderOptions`) for centralized/localizable default captions and messages.
4. **Global exception handler** (`Application.ThreadException`, `AppDomain.UnhandledException`) for production crash reporting.

Validate early; catch specific exceptions before generic ones; never silently swallow. See [wiki/HowTo-Handle-Errors.md](wiki/HowTo-Handle-Errors.md).

### Change tracking (`ChangeTracker<T>`)

`where T : class`. Snapshots and comparison come from a `ChangeTrackingPolicy<T>` fixed at construction. In a presenter use `TrackChanges(value[, policy | comparer])`: explicit policy → a `ChangeTrackingPolicy<T>` registered for exactly `T` (`mvp.ChangeTracking.For`) → the registered `IObjectCloner` / `IObjectContentComparer` (`mvp.ChangeTracking.UseCloner/UseComparer`; built-ins: reflection deep copy and `ObjectComparer.ContentEquals`). `new ChangeTracker<T>(value[, comparer | policy])` uses only what it is given plus the built-ins — never the startup configuration. `ICloneable` is **not** used (`Clone()` may be shallow, e.g. `Array.Clone`, hiding edits); a model with a verified deep `Clone()` opts in through a policy. `T`'s own `Equals`/`IEquatable` is deliberately ignored — entity equality is usually "same Id", which would hide edits and skip the save prompt; pass a comparer to opt in. `ContentEquals` trusts `Equals` only for value types and `System.*`/`Microsoft.*` types and compares application reference types field by field at every depth (`ObjectComparer.DeepEquals` still respects every `Equals`). Plug a third-party deep-cloner / comparer at startup with a few-line adapter implementing `IObjectCloner` / `IObjectContentComparer`.

It **only detects changes** — the presenter owns the data; the tracker keeps a *reference* to the object it was created with plus a deep-copied snapshot, and has no `CurrentValue`. Two usage styles, each with its own pair: **(1) one object edited in place** — `_tracker = TrackChanges(_order)`, edit `_order`, `IsChanged`, `AcceptChanges()` after a save; **(2) a new model from the view each time** — `IsChangedWith(ReadModel())`, `AcceptChanges(savedModel)` after a save (never parameterless `AcceptChanges()` there: it re-photographs the stale initial object). `AcceptChanges(x)` snapshots `x` only (the tracked object is untouched), which also handles async saves: snapshot what you send, accept it on completion, and edits made meanwhile stay dirty. **Grid / detail rows — pick the unit by when things are saved:** everything saved together on 登録 (rows added / removed / reordered are unsaved too) → one tracker over the whole list model; rows added / removed / reordered written at once, cell edits on 登録 → one tracker per row (`Dictionary<key, ChangeTracker<Row>>`, `IsChanged` = any, save the rows whose tracker changed, then `AcceptChanges()` on those) — not one whole-list tracker plus a mirror copy for `AcceptChanges(copy)`. Keep screen-only counters (row keys) out of the tracked model: private fields are compared. No change event and no revert: refresh `View.IsDirty` / `RaiseCanExecuteChanged()` where the data changes; to revert while staying on screen, reload and create a new tracker. Not thread-safe — use one thread (UI). **Screen-level ignores** (this tracker only): `.Ignore(x => x.UpdatedAt)`, `.Ignore<OrderLine>(l => l.CachedTotal)` (wherever that type appears, collection elements included), `.IgnoreMember("Name")` (any type), `.IgnoreType<Audit>()` — one member level per expression (paths are rejected with a hint); hand-written backing fields (`_name`, `m_name`) are found by convention. These three forms are what CompareNETObjects / ObjectsComparer also support: the tracker passes them as `ChangeTrackingIgnores` to `IObjectContentComparer.ContentEquals(left, right, ignores)`, and an adapter maps them onto the library's settings. A tracker with a hand-written comparer throws on `Ignore`. Used in the window-closing dirty-check pattern. See [wiki/Reference-ChangeTracker.md](wiki/Reference-ChangeTracker.md).

### Validation (`ValidationState` + `IValidatableView`)

Every presenter has `protected ValidationState Validation` — current errors keyed by field name (the model property name, `nameof(Model.Prop)`; `ValidationState.FormLevel` for errors that belong to no field).

- **Rules:** model DataAnnotations via `Validation.Validate(model)` (property, class-level, `IValidatableObject`), plus presenter rules via `Validation.SetError(field, msg)` / `ClearError(field)` for cross-field / database checks. Call `Validate` on every edit.
- **When errors show:** next to a field once the user has left its control, or all at once after a submit attempt — `if (!Validation.Check(model)) return;`. `Validation.Reset()` for a new/cleared form forgets visited fields.
- **Failed submit (recommended):** `if (!Validation.Check(model)) { var first = Validation.FirstError; Messages.ShowWarning(first.Message); View.FocusError(first.Field); return; }`. The order is the model's: `Check`/`Validate` remember the model type, and `ErrorsInOrder()` / `FirstError` follow its properties in declaration order (base class first), moved by `[ValidationOrder(n)]` (unnumbered = 0); presenter rules (`SetError` on a property name) take that property's place, other fields follow by name, `FormLevel` last — so the presenter keeps no field list (declare the input model's properties in screen order). A screen whose rules are all presenter rules still calls `Check(_input)` (it remembers the type). `ErrorsInOrder(params fieldOrder)` puts given fields first, for a screen that differs from its model. The Form implements `FocusError` with `_errors.TryFocusError(field)` (selects the containing `TabPage`; `false` for `FormLevel` / unmapped / disabled). Move the focus only after the message, never from `ShowErrors`.
- **Two display styles, both supported:** message only — the view does not implement `IValidatableView`, nothing is drawn next to the controls, the failed submit shows the first error and moves the focus (the usual WinForms business-screen style); or message + icons — the view implements `IValidatableView` with an `ErrorProviderDisplay`, icons appear next to visited fields and all of them after a submit.
- **Keep Submit enabled while there are errors** — a disabled Save cannot reveal errors of unvisited fields. `Validation.HasErrors` exists but must not gate Submit's `canExecute`.
- **View:** implement `IValidatableView` → `IValidationErrorDisplay ValidationErrors { get; }`. Form: `new ErrorProviderDisplay(errorProvider).Map(nameof(Model.Field), control)...Map(ValidationState.FormLevel, btnSubmit)`; form-level and unmapped-field errors appear on the FormLevel control after a submit attempt. Custom rendering: derive from `ControlErrorDisplay`, implement `SetControlError(Control, string)`.
- The framework connects `Validation` to the view automatically when it implements `IValidatableView`; each change re-evaluates `canExecute`.
- Gotchas: `[Compare]` on .NET Framework reports no member name (lands on the FormLevel control) — use `[CustomValidation]` or a presenter rule; `IValidatableObject` only runs after all property rules pass.

See [wiki/HowTo-Validate-Form-Input.md](wiki/HowTo-Validate-Form-Input.md) and `samples/.../ValidationDemo/`.

### Event aggregator (`IEventAggregator`)

Thread-safe pub/sub for **cross-presenter** communication: automatic UI-thread marshaling, exception isolation. Subscriptions are held **strongly** until the returned `IDisposable` token is disposed — no weak references, no GC auto-cleanup — so any handler shape (method group, capturing lambda, capturing filter) works; a token that is never disposed keeps its handler (and the presenter it captures) alive for the aggregator's lifetime. Pattern: `_events.Subscribe<T>(handler).DisposeWith(Disposables);`. Analyzer **MVP017** warns when a `Subscribe(...)` result is discarded as a bare statement (`_ = ...`, storing, chaining, or `using` are accepted). One `EventAggregator` per provider is registered by default on both paths — constructor-inject `IEventAggregator` (no `new`); replace it with `mvp.Events.Use(...)`. Each subscription delivers on the `SynchronizationContext` current when it subscribed (presenters subscribe on the UI thread). A subscription disposed while its message is queued receives nothing. The aggregator is `IDisposable` (disposed with its provider: subscriptions dropped, `Subscribe` throws); there is no `ClearSubscriptions` — tests create their own instance.

Use for cross-presenter/cross-module events. Do **not** use for parent-child coordination (call methods directly) or shared state (use a service with events). Register subscriptions with `.DisposeWith(Disposables)` at the creation line (released automatically after `Cleanup()`; no `Cleanup` override needed). See [wiki/Reference-EventAggregator.md](wiki/Reference-EventAggregator.md) and `samples/.../ComplexInteractionDemo_EventBased/`.

## Project Structure

```
/
├── src/        Core libraries (shippable)
│   ├── Kibinago.Core/              Platform-neutral core (netstandard2.0, no WinForms reference)
│   │   ├── Presenters|Views|ViewActions/   presenter bases, ViewAction dispatcher
│   │   ├── Services/                  Service interfaces, dialog options, ServiceLocator, FileService
│   │   ├── Validation/  Events/       ValidationState; EventAggregator, ValueChanged/ChangingEventArgs
│   │   ├── Common/                    ChangeTracker, InteractionResult, selection, rate limiting, utilities
│   │   └── Logging/                   In-house logging abstraction
│   ├── Kibinago/                   WinForms implementation (net472;net10.0-windows, depends on Core)
│   │   ├── Presenters|ViewActions/    ViewActionBinder, Connect hosting extensions
│   │   ├── Services/                  WindowNavigator, MessageService, DialogProvider, toasts, view mapping, modules
│   │   └── Validation/  Views/        ErrorProviderDisplay; control binding helpers
│   ├── Kibinago.DependencyInjection/   Optional M.E.DI integration (net472;net10.0-windows)
│   ├── Kibinago.Testing/           PresenterTestHost, TestServices, service fakes (netstandard2.0, test projects only)
│   └── Kibinago.Analyzers/             Roslyn analyzers (in the .sln, build-only — bundled into Kibinago.Core package)
├── templates/  Kibinago.Templates.csproj + content/{app,window,control,tests} (dotnet new template package; content folders are template sources, not compiled)
├── samples/    Sample/demo apps (Kibinago.Samples, MultiProjectDemo.*)
├── tests/      Kibinago.Samples.Tests (xUnit, net48), Kibinago.Analyzers.Tests (xUnit, net10.0), templates/verify-templates.sh
├── docs/  wiki/  tools/
└── Kibinago.sln
```

`src/` holds only shippable libraries; the template package lives in `templates/`; samples and tests are siblings outside it.

## Development Notes

- **Target frameworks:** `Kibinago.Core` / `Kibinago.Testing` → `netstandard2.0`; `Kibinago` / `Kibinago.DependencyInjection` → `net472;net10.0-windows`; samples and tests → `net48`. Minimum .NET Framework is 4.7.2; net40 is no longer supported. SDK-style gives automatic file inclusion and `PackageReference`; `EnableWindowsTargeting` (in `Directory.Build.props`) lets `dotnet build` run on non-Windows hosts.
- **XML docs:** the four library packages ship their XML docs (`GenerateDocumentationFile`); in `src/` a public member without a doc comment, a parameter without `<param>` or a broken `cref` is a build error (`.editorconfig`). Implementations of documented interface / base members use `/// <inheritdoc />`.
- **Where code goes:** anything a presenter needs goes in `Kibinago.Core` and must not reference WinForms / System.Drawing; WinForms implementations go in `Kibinago`.
- **Tests:** xUnit 2.9.3. `Kibinago.Samples.Tests` targets net48, so `dotnet test` on it needs Windows (or mono); `Kibinago.Analyzers.Tests` (net10.0) runs anywhere.
- **Adding a Form/Dialog:** define `IXxxView : IWindowView` → presenter extends `WindowPresenterBase<TView>[, TParam]` → implement the Form → register the mapping → show via `WindowNavigator`. In an app project, `dotnet new mvp-window -n Xxx [--param-type int] [--validation | --edit]` scaffolds the view interface, actions, presenter and Form (`--edit`: an edit dialog opened with the model, returning the saved model, asking "Save changes?" on X / Esc / Cancel) (`dotnet new mvp-control` for a UserControl).
- **WFO1000 (.NET 9+ WinForms analyzer):** public properties on a Form/UserControl — i.e. view-interface implementations — need `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`. WFO1000 is a build **error** by default on every Form/UserControl (designer used or not). On a Form it is effectively a false positive (the designer instantiates the base class), but on a UserControl placed on another form the designer really serializes readable public properties into that form's `.Designer.cs`. It cannot be suppressed by a DiagnosticSuppressor (default severity Error). Cheapest fix: in Visual Studio, Ctrl+. on one WFO1000 → "Add DesignerSerializationVisibility" → Fix all in project (WinForms' own code fix). The templates already emit the attribute; explicit interface implementation also avoids it.
- **Editable grids:** the framework ships no grid component (grids differ: DataGridView, C1FlexGrid, …). The Form translates grid events into business events keyed by a screen row key (`CellCommitting` with `ValueChangingEventArgs<T>` to reject; the presenter rewrites by calling `ShowRow` during the event); editing-control writes, reentrancy guards, key navigation and cell error rendering stay in the Form. See [wiki/HowTo-Implement-Editable-Grid.md](wiki/HowTo-Implement-Editable-Grid.md). A long-running action closed mid-run: `CanCloseAsync` confirms, stops, awaits the run; re-check state after the modal question and check `Lifetime` after each `await` before touching the view (see wiki/HowTo-Handle-Async-Operations.md).
- **Adding a UserControl:** define `IXxxView : IViewBase` → presenter extends `ControlPresenterBase<TView>[, TParam]` → implement the UserControl → inside an MVP screen, expose `IXxxView` on the parent view interface and call `AttachChild(new XxxPresenter(deps), View.Xxx[, param])` from the parent presenter; in a non-MVP host, `new` the presenter (its own deps only) then `presenter.Connect(view[, param])`.
- The `wiki/` folder mirrors every subsystem above with Concept / HowTo / Reference / Tutorial pages — consult it before duplicating explanation here.

## Releasing

Every push to `master` and every pull request runs `.github/workflows/ci.yml` (Windows: build + both test projects + `tests/templates/verify-templates.sh` with the generated tests run).

Releases are **tag-driven**. Pushing a tag matching `v*` triggers `.github/workflows/release.yml`, which builds, runs the test suite (gate), packs the five shippable packages (`Kibinago.Core`, `Kibinago`, `Kibinago.DependencyInjection`, `Kibinago.Testing`, `Kibinago.Templates`), asserts the analyzer is bundled in `Kibinago.Core` and the XML docs are packed next to every library, publishes to **GitHub Packages**, and creates a **GitHub Release** with the `.nupkg` files attached.

```bash
git tag v1.0.0-preview.1 && git push origin v1.0.0-preview.1   # preview
git tag v1.0.0           && git push origin v1.0.0             # stable
```

- The package version is the tag minus the leading `v`. There is no version in any `.csproj` — **never** hardcode one; pass it only via the tag (the workflow injects `-p:Version=`).
- A tag containing `-` (e.g. `-preview.1`) → NuGet prerelease (hidden by default) **and** the workflow passes `--prerelease` to mark the GitHub Release as pre-release. A clean tag → stable.
- Only `Kibinago.Core`, `Kibinago`, `Kibinago.DependencyInjection`, `Kibinago.Testing` and `Kibinago.Templates` publish; everything else is `IsPackable=false` via `Directory.Build.props`. The analyzer ships **inside** `Kibinago.Core` (`analyzers/dotnet/cs/`), not standalone, and flows transitively to `Kibinago` consumers.
- GitHub Packages requires consumer authentication (a PAT) even for public repos; the auth-free fallback is downloading the `.nupkg` from the Release. Both routes are documented for consumers in `README.md`.
- Full maintainer guide (workflow internals, first-time setup, consuming, troubleshooting, cleanup): [wiki/HowTo-Release.md](wiki/HowTo-Release.md).
