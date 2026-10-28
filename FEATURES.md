# Kibinago — feature catalog

A map of everything the framework offers, so you can find the right API without reading the source.
One line per feature: **what it is for → entry point**. Details and examples live in the linked wiki page
(Japanese) and in [CLAUDE.md](CLAUDE.md) (the rules: what a presenter may and may not do).

**Packages** — **Core** = `Kibinago.Core` (netstandard2.0, no WinForms; everything a presenter uses) ·
**WinForms** = `Kibinago` (net472 / net10.0-windows; forms, dialogs, toasts, binding) ·
**DI** = `Kibinago.DependencyInjection` (optional M.E.DI) · **Testing** = `Kibinago.Testing` (test projects only) ·
**Templates** = `Kibinago.Templates`.
Namespaces are the same in Core and WinForms; there are ten: `Kibinago` (`MvpApplication`), `.Presenters`, `.Views`,
`.ViewActions`, `.Services` (service interfaces, their options and default implementations, toasts, view mapping),
`.Validation`, `.Events` (`IEventAggregator`, `ValueChanged`/`ValueChangingEventArgs`), `.Common` (results, change
tracking, selection, rate limiting, disposables), `.Configuration`, `.Logging` — plus `Kibinago.DependencyInjection` / `Kibinago.Testing`.

---

## 1. Presenters and their lifecycle — Core, `Kibinago.Presenters` · [wiki](wiki/Reference-Presenter-Base-Classes.md)

| Need | Use |
|---|---|
| A Form's presenter (no / with a parameter) | `WindowPresenterBase<TView>` / `WindowPresenterBase<TView, TParam>` |
| A UserControl's presenter | `ControlPresenterBase<TView>` / `ControlPresenterBase<TView, TParam>` |
| Runtime data (a record id, a shared model) | the `TParam` of the base class → `OnInitialize(TParam)`; constructor = dependencies only |
| Lifecycle hooks (all `protected override`) | `OnViewAttached`, `RegisterViewActions`, `OnInitialize`, `OnInitializeAsync(CancellationToken)`, `Cleanup` |
| Cancel async work when the window closes | `protected CancellationToken Lifetime` |
| Subscribe to a view event, auto-unsubscribed | `SubscribeViewEvent(h => View.X += h, h => View.X -= h, handler)` (+ `<TEventArgs>`) |
| Release anything with the presenter | `something.DisposeWith(Disposables)` (see §13) |
| Built-in services, no constructor injection | `Messages`, `Toasts`, `Dialogs`, `Navigator`, `Logger` (see §5) |
| Your own registered services without a DI container | `Services.Resolve<T>()` from `OnViewAttached` / `OnInitialize` on (not the constructor); constructor injection preferred |
| Validation state / change tracking | `Validation` (§8), `TrackChanges(model)` (§7) |
| Embed a panel presenter in an MVP screen (it needs services) | parent view exposes `IPanelView Panel { get; }`; parent presenter: `AttachChild(new PanelPresenter(deps), View.Panel[, param])` in `OnInitialize` — disposed with the parent or its control · [wiki](wiki/HowTo-Implement-Master-Detail.md) |
| Panels added / removed at runtime (rows, tabs) | `var handle = AttachChild(...)` keyed by the child view; remove: `handle.Dispose()` then `View.RemoveXxx(view)` |
| Host a UserControl presenter without an MVP parent (WinForms) | `presenter.Connect(view[, param])` — attaches, initializes, disposes with the control |
| Host a Form you create yourself (shell, legacy) | `presenter.Connect(form[, param][, onClosed])` (WinForms) |
| Input model the form binds to | `NotifyingObject` (`SetProperty(ref field, value)`), `Kibinago.Common` |

Public surface of a presenter: the constructor only (Rules 16/17, analyzer-enforced). Handlers are `private`.

## 2. Views — Core, `Kibinago.Views`

| Need | Use |
|---|---|
| Any view (UserControl) / a view with actions / a window | `IViewBase` / `IActionableView` (`ActionBinder`) / `IWindowView` |
| A view that shows validation errors | `IValidatableView` (`ValidationErrors`) — connected to `Validation` automatically |
| View events — no hand-written EventArgs, never a WinForms one (MVP004) | `EventHandler` (presenter reads the view) · `EventHandler<int>` / any business type · a tuple `EventHandler<(int Id, string Column)>` · `EventHandler<ValueChangedEventArgs<T>>` (after a change: `OldValue`, `NewValue`) · `EventHandler<ValueChangingEventArgs<T>>` (before it; the presenter may set `Cancel`) · BCL `EventHandler<CancelEventArgs>` — `Kibinago.Events` · [wiki § Rule 4](wiki/Design-Rules.md) |
| Open a legacy (non-MVP) popup from a migrated screen | a semantic view method returning `InteractionResult<T>` (`InteractionResult<Customer> EditCustomer(Customer current)`), implemented by the Form; a form shared by many screens: a small relay service · [migration wiki, Step 7](wiki/HowTo-Migrate-From-Legacy-WinForms.md) |

View interfaces expose data and behavior, never controls. Forms need no closing code.

## 3. ViewAction (commands) — Core + WinForms, `Kibinago.ViewActions` · [wiki](wiki/Reference-ViewAction-System.md)

Optional: a simple screen can use plain view events instead.

| Need | Use |
|---|---|
| Action keys for a screen | `ViewAction.Factory.WithQualifier("Screen")` + `Factory.Create(nameof(Save))`; one shared key: `ViewAction.Create("Scope.Name")` |
| Common verbs, ready-made | `StandardActions.Save`, `.Cancel`, `.Delete`, `.Refresh`, `.Ok`, … (keys `"Standard.Save"` …); no need to redefine them |
| Typed payload checked at compile time | `ViewAction<T>` (`Factory.Create<T>(...)`, `ViewAction.Create<T>(...)`) |
| Register handlers (sync / async / with payload) | `Dispatcher.Register(key, handler, canExecute:)` in `RegisterViewActions()` |
| Async handler | `Register(key, async ct => ...)` — disabled while running, repeat triggers ignored, token = `Lifetime` |
| Re-evaluate enabled states | `Dispatcher.RaiseCanExecuteChanged()` |
| Dispatch from code / await in tests | `Dispatcher.Dispatch(key[, payload])`, `DispatchAsync(...)` |
| Map controls to keys (WinForms) | `ViewActionBinder.Add(key, controls...)`, `Add(key, control, c => value)`, bulk `AddRange(...)` |
| Controls created or removed at run time (choices, rows) | `binder.Add(...)` after `Bind` works at once; `binder.Remove(controls)`; disposed controls drop out |
| A control the binder does not know (grid, list, third-party editor) | `binder.RegisterStrategy<T>((c, h) => c.SomeEvent += (EventHandler)h, ...)` before `Add` (unknown controls are rejected at `Add`); `setEnabled: (c, on) => c.Enabled = on` makes a non-Control component (third-party toolbar command) follow `canExecute` |
| Share one binder through the app's own base form | [wiki § 基底 Form](wiki/Reference-ViewAction-System.md) (`protected ViewActionBinder Binder` + explicit `IActionableView.ActionBinder`) |
| Cross-cutting behavior (audit, timing, error dialogs) — wraps async handlers to their end | `Dispatcher.Use(async (ctx, next) => { ...; await next(ctx); ... })` / `IDispatchMiddleware.InvokeAsync`, `DispatchContext` |
| Fail fast on wiring mistakes (Debug) | `mvp.Actions.StrictValidation()` / `IDispatcherConfigurer` (`DispatchValidationMode.Strict`) |
| Legacy form migration (event-only) | Explicit pattern: `ActionBinder => null`, `binder.Bind()`, `ActionRequest` + `ActionRequestEventArgs` |

A list's double-click: raise a view event, the presenter calls `Dispatcher.Dispatch(Edit)` (keeps `canExecute`).

## 4. Windows: navigation, results, closing — [Navigator wiki](wiki/Reference-WindowNavigator.md) · [closing wiki](wiki/Concept-Window-Closing-Model.md)

| Need | Use |
|---|---|
| Open a modal window and get a result | `Navigator.For(presenter)[.WithParam(p)].ShowAsModal<TResult>()` → `InteractionResult<TResult>` |
| Open a non-modal window (owner, one per key, callback) | `.ShowWindow<TResult>(owner: View, key: id, onClosed: r => ...)` — `owner` may be a window's view or a panel's (a child presenter's `View`): its window owns the new one (inside an MDI child: the MDI parent) |
| Prepare a window without showing it (docking pane, tabbed document, layout restore) | `.PrepareWindow<TResult>(key: id, onClosed: ...)` → the view; the container shows it (`dockContent.Show(dockPanel, …)`) · [wiki](wiki/Reference-WindowNavigator.md) |
| Wrong navigation fails to compile (missing / wrong `WithParam`, a control presenter) | constraints on `For` / `WithParam` / the direct methods (`IWindowPresenter`, `IInitializable[<TParam>]`) |
| Close with a result / a 「閉じる」 button (goes through `CanClose` like ✕) | `RequestClose(result, InteractionStatus.Ok)` / `RequestClose(InteractionStatus.Cancel)` as a ViewAction handler |
| Ask before closing: ✕ / Alt+F4 / owner closing / RequestClose | `protected override bool CanClose(CloseReason)` / `Task<bool> CanCloseAsync(CloseReason)` |
| The screen must not open (data gone, no permission) | in `OnInitialize`: `Messages.ShowWarning(why); RequestClose(InteractionStatus.Cancel); return;` (or `RequestClose(why, Error)` for the caller to show): never shown (Navigator does not show it; `Connect` hides and closes it in `Load`); modeless `ShowWindow` returns `null` |
| Result type | `InteractionResult<T>` (`IsOk`, `IsCancelled`, `IsError`, `Value`, `ErrorMessage`), `InteractionStatus`, `CloseReason` |
| Map view interfaces to Forms (WinForms) | `IViewMappingRegister`: `RegisterFromAssembly`, `RegisterFromNamespace`, `Register<IView, Form>()`, `Register<IView>(() => new Form(deps))` |
| UI module registers its own views | `IViewModuleRegistrar` + `register.RegisterModules(...)` (`Kibinago.Services`) |

System shutdown / Task Manager never reach `CanClose`. Save-on-close: call `RequestClose(result)` inside `CanClose` and return `true`. Save / 破棄 handlers `AcceptChanges()` before `RequestClose`, so the gate does not ask; tests: `await host.WaitForCloseDecisionAsync()` for an async gate.

## 5. Services — `Kibinago.Services`; presenter-facing interfaces in Core, implementations in WinForms · [wiki](wiki/Reference-Platform-Services.md)

| Need | Use |
|---|---|
| Message boxes (never `MessageBox.Show` in a presenter) | `Messages` — `IMessageService`: `ShowInfo/Warning/Error`, `ConfirmYesNo`, `ConfirmOkCancel`, `ConfirmYesNoCancel` → `ConfirmResult` |
| Restyle every message box | `IMessageDialogBackend` (`mvp.Messages.UseDialogs(...)`) |
| Open / save / folder / color dialogs | `Dialogs` — `IDialogProvider` (+ `OpenFileDialogOptions`, `SaveFileDialogOptions`, …) |
| Font / print dialogs | not wrapped: the Form uses `FontDialog` / `PrintDialog` directly; a presenter-started flow is a semantic view method with app data types · [wiki](wiki/Reference-Platform-Services.md) |
| Files, database, HTTP | not provided: constructor-inject a purpose-shaped interface (`ICsvExporter`, `IOrderRepository`); optional architecture test recipe · [wiki](wiki/Reference-Platform-Services.md) |
| Corner toast (thread-safe) | `Toasts` — `IToastService.Show(text, ToastType, duration?)` · [wiki](wiki/Reference-Toast-Notifications.md) |
| Toast with per-call options / anchored (view-side) | `IWinFormsToastService`, `ToastOptions`; look: `ToastStyle` (`Soft`, `Card`, `Solid`), custom `ToastRenderer` |
| Busy (wait cursor, whole window takes no input — an MDI child only itself — optional overlay) | `using (ShowBusy()) { ... }` on the presenter (`IBusyIndicator`); `mvp.Busy.UseOverlay()` (centred square + ring, a `ShowBusy("…")` message under it, two lines at most; backdrop color / opacity: `new HudBusyRenderer { Backdrop = ... }`, `Color.Transparent` = none) or `UseOverlay(new MyRenderer())` (`BusyRenderer`); `mvp.Busy.Delay`, `mvp.Busy.Font` (text font; default Windows' UI font at 12 pt) · [wiki](wiki/HowTo-Show-Busy.md) |
| Feedback at a control's position (view code only) | `AnchoredToast.Show*(...)`, `AnchoredMessageBox.Show*/Confirm*(...)`; a presenter calls a semantic view method |
| Logging (M.E.L.-style names) | `Logger` — `ILogger` (`LogInformation`, `LogError(ex, "...{Name}", arg)`, …); `NullLoggerFactory` (default), `DebugLoggerFactory`; adapter to M.E.L. in `samples/MultiProjectDemo.Shell/Logging/` · [wiki](wiki/Reference-Logging.md) |

## 6. Configuration and DI — [customize wiki](wiki/HowTo-Customize-Framework.md) · [DI wiki](wiki/Reference-DependencyInjection.md)

| Need | Use |
|---|---|
| One startup call (no DI container) | `MvpApplication.Configure(framework: mvp => ..., services: reg => ...)`, then `MvpApplication.Run(mainForm)` |
| Replace / tune framework parts | `KibinagoBuilder`: `mvp.Messages`, `.Toasts`, `.Busy`, `.Dialogs`, `.ChangeTracking`, `.Events`, `.Logging`, `.Actions` |
| Register business services / a module | `IServiceRegistry.RegisterInstance<T>`, `RegisterSingleton<T>`; a module = a static `Register(IServiceRegistry)` |
| Ambient provider / built-in container | `ServiceLocator.Current`, `ServiceLocator.Configure(...)`; `DefaultServiceProvider`; `sp.Resolve<T>()`, `ResolveRequired<T>()` |
| M.E.DI | (DI) `services.AddKibinago(viewRegistry[, mvp => ...])`, `provider.UseKibinago()`, `RegisterModules(...)` with `IModuleRegistrar` |
| Child presenters built by DI, one scope each | `IPresenterFactory.Create<T>()` (implemented by the DI package) |

Precedence: a directly registered service > the `mvp.` choice > the built-in default.

## 7. Change tracking — Core, `Kibinago.Common` · [wiki](wiki/Reference-ChangeTracker.md)

| Need | Use |
|---|---|
| "Is the screen dirty?" for one model edited in place | `var t = TrackChanges(model)` → `t.IsChanged`, `t.AcceptChanges()` after saving |
| A new model read from the view each time | `t.IsChangedWith(ReadModel())`, `t.AcceptChanges(savedModel)` |
| Editing rows in a grid (DataGridView / third-party) | no grid component by design: view translates grid events to business events; [wiki](wiki/HowTo-Implement-Editable-Grid.md) (row keys, reject / rewrite a cell, row errors on cells, pitfalls, C1FlexGrid mapping) |
| Grid rows: everything saved on 登録 | one tracker over the whole list model (adds / removes / order are unsaved too) |
| Grid rows: adds / removes / order saved at once, cells on 登録 | one tracker per row; `IsChanged` = any row; save and `AcceptChanges()` the changed rows ([wiki](wiki/Reference-ChangeTracker.md#一覧-明細-を追跡する単位--保存のタイミングで決まる)) |
| Ignore members / types | `.Ignore(x => x.UpdatedAt)`, `.Ignore<Line>(l => l.Cache)`, `.IgnoreMember("Name")`, `.IgnoreType<Audit>()` |
| Custom snapshot / comparison | `ChangeTrackingPolicy<T>`, `IObjectCloner`, `IObjectContentComparer` (`mvp.ChangeTracking.UseCloner/UseComparer/For<T>`) |
| Deep copy / deep compare utilities | `ObjectCloner.DeepCopy`, `ObjectComparer.ContentEquals` / `DeepEquals` |

Detection only (no undo, no events). The model's own `Equals` is ignored by design.

## 8. Validation — `Kibinago.Validation` · [wiki](wiki/HowTo-Validate-Form-Input.md)

| Need | Use |
|---|---|
| Errors by field, shown after leaving a field or after submit | `Validation` — `ValidationState` (Core) |
| DataAnnotations on the model | `Validation.Validate(model)` on each edit; submit: `if (!Validation.Check(model)) ...` |
| Presenter rules (cross-field, database) | `Validation.SetError(field, msg)` / `ClearError(field)`; form-level key `ValidationState.FormLevel` |
| First error in screen order (message + focus) | `if (!Validation.Check(model)) { var first = Validation.FirstError; Messages.ShowWarning(first.Message); View.FocusError(first.Field); }` |
| The screen's order of errors | from the model last checked: its properties in declaration order (base class first); move one with `[ValidationOrder(n)]`; all errors: `Validation.ErrorsInOrder()` — no field list in the presenter |
| Show errors next to controls (WinForms) | `new ErrorProviderDisplay(errorProvider).Map(nameof(Model.Field), control)…`, `TryFocusError(field)`; custom: derive `ControlErrorDisplay` |

Two display styles, both supported: **message only** (the view does not implement `IValidatableView`; the usual WinForms style) or **message + icons**. Keep Submit enabled while there are errors (it reveals unvisited fields).

## 9. Binding helpers — WinForms, `Kibinago.Views` · [wiki](wiki/Reference-Data-Binding.md)

Expression-based two-way binding of controls to an `INotifyPropertyChanged` model (Supervising Controller):
`control.BindProperty(model, x => x.Prop, "ControlProperty"[, updateMode, formattingEnabled])`, `textBox.Bind(...)`,
`checkBox.Bind`, `numericUpDown.Bind`, `comboBox.Bind` (SelectedValue) / `BindText` (free text) / `BindSelectedItem` / `BindSelectedIndex`,
`dateTimePicker.Bind` (incl. `DateTime?` with the check box), `label.Bind` (one-way), `trackBar`/`progressBar`/`richTextBox`/`maskedTextBox`/`listBox` overloads,
`pictureBox.BindImageLocation`, `pairs.BindRadioGroup(model, x => x.Value)`. One property per expression (no paths).

A grid bound to a list of objects that sorts by header click / several columns: `SortableBindingList<T>` (Core, `Kibinago.Common`; stable, copies the source list, never throws on mixed values, `ApplySort(s => s.By(r => r.Ward).ThenByDescending(r => r.No))` (compiler-checked columns; run-time names: `ApplySort(ListSortDescriptionCollection)`), `ReplaceAll(items)` for a reload keeping the sort, edited rows stay put until `Resort()` (the chosen sort and glyph stay) or move to their place with `KeepSorted = true`, `SetComparer(r => r.Name, comparer)`) · [wiki](wiki/Reference-Data-Binding.md#一覧グリッド-sortablebindinglistt)

Several columns chosen in the header (click = that column, Shift+click = add a column, Ctrl+click = remove it, as DevExpress / Telerik / ag-Grid do): about 30 lines of View code passing `ApplySort(ListSortDescriptionCollection)`; no framework part needed · [wiki](wiki/Reference-Data-Binding.md#見出しの-shiftクリックで複数列-view-のコード)

Move one row (上へ / 下へ / 最上へ / 最下へ buttons): `rows.MoveUp(row)` / `MoveDown` / `MoveToTop` / `MoveToBottom` / `Move(from, to)` on any `IList<T>` (Core, `Kibinago.Common.ListMoveExtensions`; `false` when nothing moved). A `SortableBindingList<T>` moves with one `ItemMoved` (the grid's current row follows), a `BindingList<T>` with RemoveAt + Insert (the view re-selects the row). wiki: Reference-Data-Binding (行の移動).

## 10. Screen composition: UserControls, selection, cascades

| Need | Use |
|---|---|
| Split a screen into UserControls that edit the same data | each panel's presenter takes the parent's model as `TParam`; panels react to its `PropertyChanged` (shared model; no direct calls) · [communication wiki](wiki/HowTo-Communicate-Between-Presenters.md) |
| Share "the current selection" (master-detail) | `Selection<T>` / `ISelection<T>` (`Current`, `Select`; `CurrentChanged` = a different object incl. the same record reloaded → refresh a detail view; `IdentityChanged` = another record or cleared → reload dependants, used by `Cascade`); stable identity: `ISelectable.Key`, value types: `SelectableItem.Of/From/FromEnum` · [wiki](wiki/HowTo-Implement-Master-Detail.md) |
| Parent → child reloads (category → subcategory) | `Cascade.Bind(from, target, reload)`, `Cascade.Combine(a, b, target, reload)` · [wiki](wiki/HowTo-Handle-Cascading-Selection.md) |

## 11. Cross-presenter events — Core, `Kibinago.Events` · [wiki](wiki/Reference-EventAggregator.md)

`IEventAggregator` (constructor-inject; one per provider): `Subscribe<T>(handler[, filter]).DisposeWith(Disposables)`, `Publish<T>(msg)`.
Delivered on the subscriber's UI thread; held strongly until the token is disposed (analyzer MVP017).
For modules / screens that do not know each other — not for parent-child or panels of one window (use a shared model).

## 12. Rate limiting — Core, `Kibinago.Common` · [wiki](wiki/Reference-RateLimiting.md)

| Need | Use |
|---|---|
| Run once the user stops typing / clicking ▲▼ (search as you type) | `RateLimit.Debounce(300, action)` → `Signal()`; search now: `Signal()` + `Flush()` |
| Async action; a newer run cancels the stale one | `RateLimit.Debounce(300, async ct => ...)` (`cancelPrevious: true` by default) |
| At most once per interval while it continues (drag preview) | `RateLimit.Throttle(150, action)` |
| Release with the presenter | `.DisposeWith(Disposables)` (`Dispose` cancels an async run in flight; analyzer MVP018 warns when a field one is never disposed) |

Callbacks run on the creating thread's context; without `onError` a failure reaches `Application.ThreadException`.

## 13. Disposables — Core, `Kibinago.Common`

`CompositeDisposable` (`Disposables` in presenters), `x.DisposeWith(Disposables)`, `Disposable.Create(() => ...)` (e.g. unsubscribe from a model that outlives the presenter).

## 14. Testing — `Kibinago.Testing` (netstandard2.0, test projects only) · [wiki](wiki/HowTo-Test-A-Presenter.md)

| Need | Use |
|---|---|
| Run a presenter's real init with a mock view (no Form) | `using var host = PresenterTestHost.Start([services,] presenter, view[, param])` |
| Fake every framework service at once | `var fakes = TestServices.CreateFakes();` → `Start(fakes.Provider, …)`; `fakes.Messages` / `Toasts` / `Dialogs` / `Navigator` / `Busy` / `Logs` |
| Only the services you register (own fakes, Moq, …) | `var sp = TestServices.Create(); sp.RegisterInstance<IMessageService>(mine);` (or replace one in `fakes.Provider` before `Start`) |
| Trigger actions like a bound control | the mock view exposes `CapturingActionBinder Binder` as its `ActionBinder` → `view.Binder.Dispatch(...)`, `await DispatchAsync(...)`, `CanDispatch(...)`; `IsBound`, `BindCount` / `UnbindCount` (view handed to a new presenter) |
| Results and closing | `host.CloseRequested`, `CloseStatus`, `CloseResult`; `host.CanClose(reason)`, `await host.CanCloseAsync(reason)`; `await host.Initialized` |
| Messages | `FakeMessageService`: `Calls` (`Kind`, `Text`, `Caption`, `IsConfirmation`); answers `EnqueueYesNo(...)` → `YesNoHandler` → `AnswerUnscripted(yes)`, otherwise `UnscriptedInteractionException` |
| Dialogs | `FakeDialogProvider`: `EnqueueSaveFile(InteractionResult<string>.Ok(path))`, `SaveFileHandler = o => …`, `CancelUnscripted()`; `Calls` |
| Child windows | `FakeWindowNavigator`: modal `RespondTo<TPresenter, TResult>(r => InteractionResult<TResult>.Ok(…))` or typed by the parameters `RespondTo<TPresenter, TParam, TResult>(p => …)`, `LastModalParameters<T>()`; non-modal `Windows[i].CloseWithResult(value)` / `Close()` / `CloseWithError(...)`; same key / disposal / result-type rules as `WindowNavigator` |
| Logs and toasts | `RecordingLoggerFactory.Entries` (`Level`, `Message`, `Exception`), `FakeToastService.Calls` |
| Validation errors | the mock view returns a `FakeValidationErrorDisplay` as `ValidationErrors` (`IValidatableView`) → `Errors`, `RevealAll`, `ErrorsFor(field)`, `HasError(field)`, `ShowCount` / `ResetCount` |

Never widen presenter visibility for tests.

## 15. Tooling

**Roslyn analyzers** (bundled in Core, flow to WinForms users; all warnings):
MVP001 view interface ends with `View` · MVP002 presenter ends with `Presenter` · MVP003 presenter creates UI controls ·
MVP004 UI types in view interfaces / presenters · MVP006 public presenter methods return void · MVP007 presenter uses a concrete Form ·
MVP008 public Form / UserControl method not in its view interface (test doubles that are not controls are exempt) · MVP013 UI control names in interface method names ·
MVP014 presenter references WinForms types · MVP016 presenter declares public events · MVP017 `Subscribe` result discarded ·
MVP018 a `Debouncer` / `Throttler` stored in a field / property is never disposed.
With a `ProjectReference`, reference `Kibinago.Analyzers` as an analyzer explicitly (see `samples/`).

**`dotnet new` templates** (`dotnet new install Kibinago.Templates`) · [wiki](wiki/HowTo-Use-Templates.md):
`kibinago` (app; `--framework net10.0-windows|net472`, `--kibinago-version`, `--no-restore`) ·
`mvp-window` (`--param-type`, `--validation`, `--edit` = edit dialog with result + unsaved-changes prompt, `--namespace`) · `mvp-control` (`--namespace`) ·
`kibinago-tests` (`--framework`, `--kibinago-version`, `--app-project`).

## 16. Where to read more

Concepts: [MVP pattern](wiki/Concept-MVP-Pattern.md) · [architecture](wiki/Concept-Architecture-Overview.md) · [presenter responsibilities](wiki/Concept-Presenter-Responsibilities.md) · [design rules](wiki/Design-Rules.md) ·
How-tos: [large edit screen](wiki/HowTo-Build-Large-Edit-Screen.md) · [async](wiki/HowTo-Handle-Async-Operations.md) · [errors](wiki/HowTo-Handle-Errors.md) · [window closing](wiki/HowTo-Handle-Window-Closing.md) · [subscriptions](wiki/HowTo-Manage-Presenter-Subscriptions.md) · [legacy migration](wiki/HowTo-Migrate-From-Legacy-WinForms.md) ·
Start: [getting started](wiki/Getting-Started.md) · [tutorial](wiki/Tutorial-Building-Your-First-App.md) · [FAQ](wiki/FAQ.md) · [troubleshooting](wiki/Troubleshooting.md) · [glossary](wiki/Glossary.md) ·
Samples: `samples/Kibinago.Samples` (one folder per feature), `samples/MultiProjectDemo.*` (modules, logging adapter).
