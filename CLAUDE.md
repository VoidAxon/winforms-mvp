# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a WinForms MVP (Model-View-Presenter) framework for .NET Framework 4.8, providing infrastructure for building desktop applications with clean separation of concerns. All projects use SDK-style .csproj format for better maintainability.

## Build and Test Commands

### Building the Solution
```bash
# Build entire solution
dotnet build src/winforms-mvp.sln

# Build specific configuration
dotnet build src/winforms-mvp.sln -c Debug
dotnet build src/winforms-mvp.sln -c Release

# Build specific project
dotnet build src/WinformsMVP/WinformsMVP.csproj
```

### Running Tests
```bash
# Run all tests using xUnit
dotnet test src/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj

# Run tests with detailed output
dotnet test src/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj -v detailed
```

### Running the Sample Application
```bash
# Build and run the sample WinForms application
dotnet run --project src/WinformsMVP.Samples/WinformsMVP.Samples.csproj
```

### Restore NuGet Packages
```bash
# Restore packages for the entire solution
dotnet restore src/winforms-mvp.sln
```

## Architecture

### Core MVP Pattern

The framework implements a **Supervising Controller** pattern (a variant of MVP) with clear separation between Form and UserControl scenarios.

**📋 For comprehensive design rules, see the [MVP Design Rules](https://github.com/yourusername/winforms-mvp/wiki/MVP-Design-Rules) in the wiki**

This document contains 14 detailed rules covering:
- Naming conventions (XxxView, XxxPresenter)
- Responsibility separation (Presenter = use-case logic, View = UI logic)
- Communication patterns (Tell, Don't Ask principle)
- Interface design (methods vs properties, domain naming)
- Data management (all data in Model, not just in UI controls)

**CRITICAL MVP Principle**: Presenters must NEVER directly reference UI elements or WinForms APIs (MessageBox, DialogResult, etc.). All UI interactions must go through:
- **View interfaces** for displaying data and receiving user input
- **Service interfaces** (IMessageService, IDialogProvider, etc.) for dialogs and notifications

**Key Philosophy**:
> "**Presenter handles use-case logic only, not view logic.**"
>
> Modern WinForms Views are smart enough to handle data-binding and event processing. Presenter focuses on **business workflow** (validate → save → notify), while View handles **UI details** (how to display errors, which controls to use).

This ensures:
- Presenters remain testable without requiring a UI thread
- Business logic is completely decoupled from WinForms implementation
- Code can be easily unit tested and reused across different UI frameworks

**Bad Example (Violates MVP):**
```csharp
private void OnSave()
{
    SaveData();
    MessageBox.Show("Data saved!");  // ❌ WRONG - Direct UI dependency
}
```

**Good Example (Follows MVP):**
```csharp
private readonly IMessageService _messageService;

public MyPresenter(IMessageService messageService)
{
    _messageService = messageService;  // ✅ Dependency injection
}

private void OnSave()
{
    SaveData();
    _messageService.ShowInfo("Data saved!");  // ✅ CORRECT - Service abstraction
}
```

### Presenter Hierarchy

```
PresenterBase<TView>                              [Base class - do not use directly]
│
├─ WindowPresenterBase<TView>                     [Forms - no parameters]
├─ WindowPresenterBase<TView, TParam>             [Forms - with parameters]
├─ ControlPresenterBase<TView>                    [UserControls - no parameters]
└─ ControlPresenterBase<TView, TParam>            [UserControls - with parameters]
```

A presenter that returns a business result implements `IRequestClose<TResult>` on top of the base class — a single event plus a tiny private helper to raise it. See the [Window Closing Pattern](#window-closing-pattern) section for the full close model.

#### 1. **WindowPresenterBase<TView>** - For Forms (Dialogs, Windows)

Use this for presenters that manage Form views via WindowNavigator without initialization parameters.

```csharp
public class AboutDialogPresenter : WindowPresenterBase<IAboutDialogView>
{
    protected override void OnViewAttached()
    {
        // Called when view is attached (before initialization)
    }

    protected override void OnInitialize()
    {
        // Called after view is attached, before window is shown
        View.Version = GetVersion();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(AboutActions.Close, OnClose);
    }
}

// Usage
var presenter = new AboutDialogPresenter();
navigator.ShowWindowAsModal(presenter);
```

#### 2. **WindowPresenterBase<TView, TParam>** - For Parameterized Forms

Use this when the presenter requires initialization parameters (e.g., entity ID, edit mode).

```csharp
public class EditUserParameters
{
    public int UserId { get; set; }
    public bool IsReadOnly { get; set; }
}

public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>,
                                  IRequestClose<UserResult>
{
    public event EventHandler<CloseRequestedEventArgs<UserResult>> CloseRequested;

    protected override void OnInitialize(EditUserParameters parameters)
    {
        // Both View and Parameters are available here
        var user = LoadUser(parameters.UserId);
        View.UserName = user.Name;
        View.Email = user.Email;
        View.IsReadOnly = parameters.IsReadOnly;
    }

    private void OnSave()
    {
        // ... save logic ...
        RaiseClose(new UserResult { UserId = ... }, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RaiseClose(null, InteractionStatus.Cancel);

    private void RaiseClose(UserResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserResult>(result, status));
}

// Usage
var presenter = new EditUserPresenter();
var parameters = new EditUserParameters { UserId = 123, IsReadOnly = false };
var result = navigator.ShowWindowAsModal<EditUserPresenter, EditUserParameters, UserResult>(
    presenter, parameters);
```

#### 3. **ControlPresenterBase<TView>** - For UserControls

Use this for presenters that manage UserControl views (embedded controls, panels).

```csharp
public class SettingsPanelPresenter : ControlPresenterBase<ISettingsPanelView>
{
    public SettingsPanelPresenter(ISettingsPanelView view) : base(view)
    {
        // View is automatically attached and initialized
    }

    protected override void OnInitialize()
    {
        LoadSettings();
    }

    protected override void OnControlLoad(object sender, EventArgs e)
    {
        // Called when UserControl is loaded (has parent and handle)
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(SettingsActions.Save, OnSave);
    }
}

// Usage (in parent Form)
var presenter = new SettingsPanelPresenter(settingsPanelControl1);
// Presenter is automatically initialized and disposed with the control
```

#### 4. **ControlPresenterBase<TView, TParam>** - For Parameterized UserControls

Use this when UserControl presenters require initialization parameters.

```csharp
public class SearchParameters
{
    public string DefaultKeyword { get; set; }
    public DateTime? DateFrom { get; set; }
}

public class SearchPanelPresenter : ControlPresenterBase<ISearchPanelView, SearchParameters>
{
    public SearchPanelPresenter(ISearchPanelView view, SearchParameters parameters)
        : base(view, parameters)
    {
    }

    protected override void OnInitialize(SearchParameters parameters)
    {
        View.Keyword = parameters.DefaultKeyword;
        View.DateFrom = parameters.DateFrom;
    }
}

// Usage
var parameters = new SearchParameters { DefaultKeyword = "test" };
var presenter = new SearchPanelPresenter(searchPanelControl1, parameters);
```

### View-Presenter Lifecycle

**For Forms (WindowNavigator pattern):**
1. Create presenter instance
2. WindowNavigator creates view instance using `IViewMappingRegister`
3. View is injected into presenter via `IViewAttacher<TView>.AttachView()`
4. `OnViewAttached()` is called on the presenter
5. `Initialize()` or `Initialize(parameters)` is called
6. Window is shown (modal or non-modal)

**For UserControls (Manual pattern):**
1. UserControl already exists (created in designer or code)
2. Create presenter with view (and optional parameters) in constructor
3. Presenter automatically attaches view and initializes
4. Presenter subscribes to control lifecycle events
5. Presenter is automatically disposed when control is disposed

### ViewAction System

A command/action dispatch system that decouples UI events from presenter logic. This system provides WPF ICommand-like functionality for WinForms, including declarative UI binding and dynamic enable/disable based on application state.

#### Core Components

- **ViewAction**: Immutable struct representing an action key created via `ViewAction.Create(string)`
- **ViewActionBinder**: Declaratively maps UI controls (buttons, menu items) to action keys
- **ViewActionDispatcher**: Routes actions to registered handlers with optional CanExecute predicates

#### Why ActionBinder Property Belongs in IViewBase

`ActionBinder` is a **property** (not a method) that exposes the View's `ViewActionBinder` instance. This is a **framework-level abstraction**, similar to WPF's `ICommand` binding. It should be defined in `IViewBase` for these reasons:

**1. Properties Prevent Accidental Code Execution**
- **Property access doesn't execute business logic** - it just returns data
- **Testing is safer and faster** - accessing `View.ActionBinder` won't trigger database calls or network requests
- **Prevents abuse** - developers can't hide complex logic in a property getter (it would be a code smell)

**2. It maintains better MVP separation than exposing UI controls**

❌ **Anti-Pattern (Violates MVP):**
```csharp
// View interface exposes UI controls - WRONG!
public interface IMyView : IWindowView
{
    Button SaveButton { get; }  // ❌ Leaks UI implementation
    Button DeleteButton { get; }
}

// Presenter knows about Button - WRONG!
protected override void OnViewAttached()
{
    _binder.Add(CommonActions.Save, View.SaveButton);  // ❌ Couples to WinForms
}
```

✅ **Recommended Pattern (Follows MVP):**
```csharp
// View interface stays pure - CORRECT!
public interface IMyView : IWindowView
{
    ViewActionBinder ActionBinder { get; }  // ✅ Framework abstraction (property!)
}

// Presenter doesn't know about buttons - CORRECT!
protected override void RegisterViewActions()
{
    _dispatcher.Register(CommonActions.Save, OnSave);
    _dispatcher.Register(CommonActions.Delete, OnDelete);

    // Note: View.ActionBinder.Bind(_dispatcher) is automatically called by the base class
}

// View implementation handles UI details internally - CORRECT!
private ViewActionBinder _binder;
public ViewActionBinder ActionBinder => _binder;

public MyForm()
{
    InitializeComponent();
    InitializeActionBindings();  // Set up bindings in constructor
}

private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(CommonActions.Save, _saveButton);  // Internal UI detail
    _binder.Add(CommonActions.Delete, _deleteButton);
    // No Bind() call here - base presenter class calls it automatically
}
```

**3. Clear separation of responsibilities**

| Component | Responsibility | Knowledge |
|-----------|---------------|-----------|
| **Presenter** | Business logic, when to execute actions | ViewAction keys, execution timing |
| **ViewActionDispatcher** | Route actions to handlers, automatically bind to View's ActionBinder | Action → Handler mapping |
| **IViewBase/View Interface** | Exposes ActionBinder property | Needs to connect UI to actions |
| **View Implementation** | UI details, configures ActionBinder in constructor | Which buttons map to which actions |
| **ViewActionBinder** | Technical binding mechanism | Control events → Action dispatching |
| **Base Presenter Classes** | Automatically call `View.ActionBinder?.Bind(_dispatcher)` after `RegisterViewActions()` | Binding lifecycle |

**4. Testability - Even Better Than Method Approach**

Mock views can provide a simple property without any logic:

```csharp
public class MockMyView : IMyView
{
    public ViewActionBinder ActionBinder { get; } = new ViewActionBinder();
}

[Fact]
public void OnViewAttached_ShouldBindActionBinder()
{
    var mockView = new MockMyView();
    var presenter = new MyPresenter();

    presenter.AttachView(mockView);

    // Verify that the ActionBinder was bound (Presenter called Bind)
    Assert.NotNull(mockView.ActionBinder);
}
```

**Key Principle:** `ActionBinder` property is to ViewAction what `ICommand` is to WPF - a framework-level abstraction that maintains clean separation. Using a **property instead of a method** prevents accidental execution of business logic during testing or initialization.

**5. Critical: InitializeActionBindings() is for UI binding ONLY**

⚠️ **WARNING:** The private `InitializeActionBindings()` method in your Form must contain **ONLY** declarative UI binding code. Do NOT include any business logic, database operations, or expensive computations.

❌ **WRONG - Business logic in InitializeActionBindings:**
```csharp
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(CommonActions.Save, _saveButton);

    // ❌ WRONG - Database operation in InitializeActionBindings!
    var users = _database.GetAllUsers();  // DON'T DO THIS

    // ❌ WRONG - Business logic in InitializeActionBindings!
    if (users.Count > 0)
    {
        _binder.Add(UserActions.Delete, _deleteButton);
    }
}
```

✅ **CORRECT - Pure UI binding only:**
```csharp
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();

    // ✅ CORRECT - Only declarative binding
    _binder.Add(CommonActions.Save, _saveButton);
    _binder.Add(CommonActions.Delete, _deleteButton);
    _binder.Add(UserActions.Edit, _editButton);

    // ✅ No Bind() call here - Framework automatically binds after RegisterViewActions()
}
```

**Why this matters:**

| Aspect | Why Property is Better |
|--------|------------------------|
| **Performance** | Property access won't execute database calls (they'd be obviously wrong in a getter) |
| **Architecture** | View layer should NOT contain business logic - properties make this clearer |
| **Testability** | Accessing a property in tests is safer than calling a method (no hidden side effects) |
| **Maintainability** | Properties are expected to be simple - complex logic stands out as a code smell |
| **Threading** | Property access won't accidentally freeze the UI thread |

**Think of ActionBinder like a data property:**
- You wouldn't put database queries in a property getter
- `ActionBinder` is just data (a configured binder object)
- Business logic belongs in the Presenter, not in View property getters or private init methods

**If you need conditional binding based on data:**

Do the data loading in the **Presenter**, then use CanExecute predicates:

```csharp
// Presenter - loads data and controls View state
protected override void OnInitialize()
{
    var users = _userRepository.GetAll();
    View.HasUsers = users.Count > 0;  // Set View property
}

protected override void RegisterViewActions()
{
    _dispatcher.Register(
        UserActions.Delete,
        OnDelete,
        canExecute: () => View.HasUsers);  // Use CanExecute, not conditional binding

    // Note: Framework automatically calls View.ActionBinder?.Bind(_dispatcher)
    // after this method completes. No manual binding needed!
}

// View - pure binding, no business logic
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(UserActions.Delete, _deleteButton);  // Always bind
    // CanExecute controls Enabled state, not conditional Add()
}
```

#### Best Practices

**1. Use Static Classes for ActionKeys**

Always define ActionKeys in static classes, not as raw strings. Organize them by scope:

```csharp
// Global actions used across the entire application
public static class CommonActions
{
    public static readonly ViewAction Save = ViewAction.Create("Common.Save");
    public static readonly ViewAction Cancel = ViewAction.Create("Common.Cancel");
    public static readonly ViewAction Delete = ViewAction.Create("Common.Delete");
    public static readonly ViewAction Refresh = ViewAction.Create("Common.Refresh");
}

// Module/feature-specific actions
public static class UserEditorActions
{
    public static readonly ViewAction EditUser = ViewAction.Create("UserEditor.Edit");
    public static readonly ViewAction AddUser = ViewAction.Create("UserEditor.Add");
    public static readonly ViewAction ChangePassword = ViewAction.Create("UserEditor.ChangePassword");
}
```

**2. Declarative UI Binding with ActionBinder Property Pattern (Recommended)**

Use the `ActionBinder` property to let the View expose its pre-configured action bindings, keeping the Presenter decoupled from UI elements:

**View Interface:**
```csharp
public interface IMyView : IWindowView
{
    string Name { get; set; }

    // Expose ActionBinder property - framework-level abstraction
    ViewActionBinder ActionBinder { get; }
}
```

**Presenter:**
```csharp
protected override void RegisterViewActions()
{
    // Register action handlers with CanExecute predicates
    _dispatcher.Register(CommonActions.Save, OnSave,
        canExecute: () => View.HasUnsavedChanges);
    _dispatcher.Register(CommonActions.Delete, OnDelete,
        canExecute: () => View.HasSelection);

    // Note: View.ActionBinder.Bind(_dispatcher) is automatically called by the base class
    // after RegisterViewActions() completes. No manual binding needed!
}
```

**View Implementation (Form):**
```csharp
public class MyForm : Form, IMyView
{
    // Private UI controls - NOT exposed through interface
    private Button _saveButton;
    private Button _deleteButton;
    private ToolStripMenuItem _saveMenuItem;
    private ViewActionBinder _binder;  // Internal to Form

    public ViewActionBinder ActionBinder => _binder;

    public MyForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();

        // Multiple controls can bind to the same action
        _binder.Add(CommonActions.Save, _saveButton, _saveMenuItem);
        _binder.Add(CommonActions.Delete, _deleteButton);

        // No Bind() call here - base presenter class will automatically call it
        // after RegisterViewActions() completes
    }
}
```

**Why this is better:**
- ✅ Presenter has ZERO knowledge of Button, TextBox, or any UI elements
- ✅ View interface stays pure (no UI types)
- ✅ Form owns all UI binding logic internally (configured in constructor)
- ✅ Follows WPF ICommand pattern (similar to `Command` binding)
- ✅ Easy to test - mock View just returns a simple property
- ✅ **Property access won't execute unexpected code** (safer than calling a method)

**3. Register Action Handlers with CanExecute**

Register handlers in `RegisterViewActions()` with optional `canExecute` predicates. The binder automatically enables/disables controls based on these predicates:

```csharp
protected override void RegisterViewActions()
{
    // Simple action - always enabled
    _dispatcher.Register(CommonActions.Cancel, OnCancel);

    // Action with CanExecute - only enabled when conditions are met
    _dispatcher.Register(
        CommonActions.Save,
        OnSave,
        canExecute: () => View.HasUnsavedChanges);

    _dispatcher.Register(
        CommonActions.Delete,
        OnDelete,
        canExecute: () => View.HasSelectedItem);

    // Note: View.ActionBinder.Bind(_dispatcher) is automatically called by the base class
    // after RegisterViewActions() completes. No manual binding needed!
}
```

**4. Parameterized Actions**

Actions can accept typed parameters using the generic `Register<T>` method:

```csharp
// Register handler that accepts a parameter
_dispatcher.Register<string>(
    DocumentActions.Open,
    OnOpenDocument,
    canExecute: () => true);

// Dispatch with parameter
_dispatcher.Dispatch(DocumentActions.Open, filePath);

// Handler implementation
private void OnOpenDocument(string filePath)
{
    // Load document from filePath
}
```

**5. Automatic UI State Updates - Two Patterns**

The framework supports both **action-driven** and **state-driven** UI updates (similar to WPF's ICommand pattern):

**Pattern 1: Action-Driven Updates (Automatic)**

After executing an action, UI state automatically updates. No manual code needed:

```csharp
private void OnSave()
{
    SaveDocument();

    // UI state automatically updates - no manual refresh needed!
    // (ActionExecuted event is automatically triggered)
}

private void OnDelete()
{
    DeleteItem();

    // UI automatically reflects new CanExecute state
}
```

**Pattern 2: State-Driven Updates (Manual Trigger)**

When state changes outside of action execution (e.g., user selects a row, async operation completes), use `RaiseCanExecuteChanged()`:

```csharp
protected override void OnInitialize()
{
    // Subscribe to view events that change state
    View.SelectionChanged += (s, e) =>
    {
        // Notify dispatcher that CanExecute state may have changed
        // Similar to WPF's ICommand.RaiseCanExecuteChanged()
        _dispatcher.RaiseCanExecuteChanged();
    };
}

private async void OnStartBackgroundTask()
{
    _isRunning = true;

    // State changed, notify dispatcher
    _dispatcher.RaiseCanExecuteChanged();

    await DoWorkAsync();

    _isRunning = false;

    // State changed again, notify dispatcher
    _dispatcher.RaiseCanExecuteChanged();
}
```

**Comparison with WPF:**

| Feature | WPF ICommand | This Framework |
|---------|--------------|----------------|
| Event Name | CanExecuteChanged | CanExecuteChanged |
| Manual Trigger | command.RaiseCanExecuteChanged() | dispatcher.RaiseCanExecuteChanged() |
| Auto Trigger | N/A | ActionExecuted event (bonus feature!) |
| Purpose | State changes | State changes + Action completion |

#### Supported Control Types

The binder supports these control types out of the box:
- `CheckBox` - binds to **CheckedChanged** event (triggers when Checked state changes)
- `RadioButton` - binds to **CheckedChanged** event (triggers when Checked state changes)
- `ButtonBase` - binds to **Click** event (regular buttons)
- `ToolStripItem` - binds to **Click** event (ToolStripButton, ToolStripMenuItem, etc.)
- `Control` - binds to **Click** event (fallback for any control)

**Important:** CheckBox and RadioButton bind to `CheckedChanged` rather than `Click`. This means actions trigger whenever the checked state changes, whether by user click, keyboard navigation, or programmatic change.

```csharp
// CheckBox example - triggers when checked state changes
_binder.Add(SettingsActions.ToggleAutoSave, _autoSaveCheckBox);

// RadioButton example - triggers when selected
_binder.Add(ThemeActions.SelectLightTheme, _lightThemeRadio);
_binder.Add(ThemeActions.SelectDarkTheme, _darkThemeRadio);

// Regular Button - triggers on click
_binder.Add(CommonActions.Apply, _applyButton);
```

**CanExecute with CheckBox/RadioButton:**

CanExecute still controls the `Enabled` property (grayed out when false), not the `Checked` property:

```csharp
// This checkbox is only enabled when user is logged in
_dispatcher.Register(
    SettingsActions.ToggleAutoSave,
    OnToggleAutoSave,
    canExecute: () => _isUserLoggedIn);
```

Custom control binding strategies can be registered via `RegisterStrategy<T>()`.

#### Bulk Binding Extensions

When binding many similar controls (like RadioButtons in a survey), use the `AddRange` extension method to reduce code repetition:

**Method 1: AddRange with tuples (recommended)**

```csharp
_binder.AddRange(
    (ThemeActions.Light, _lightRadio),
    (ThemeActions.Dark, _darkRadio),
    (ThemeActions.Auto, _autoRadio),
    (ThemeActions.HighContrast, _highContrastRadio)
);
```

**Method 2: AddRange with dictionary (alternative syntax)**

```csharp
var mapping = new Dictionary<ViewAction, RadioButton>
{
    [QuestionActions.StronglyDisagree] = _radio1,
    [QuestionActions.Disagree] = _radio2,
    [QuestionActions.Neutral] = _radio3,
    [QuestionActions.Agree] = _radio4,
    [QuestionActions.StronglyAgree] = _radio5
};
_binder.AddRange(mapping);
```

**Benefits:**
- Reduces code from N lines to 1 line
- Easier to maintain when adding/removing options
- Type-safe and explicit (no magic conventions)
- Clear at a glance which action binds to which control

#### ViewAction Handling Patterns: Implicit vs Explicit

The framework supports **two patterns** for handling ViewActions, each with distinct trade-offs. Choose based on your team's preferences and project requirements.

##### Pattern 1: Implicit Pattern (Recommended for Most Cases)

**Overview:** The framework automatically binds the View's `ActionBinder` to the `Dispatcher` after `RegisterViewActions()` completes. This provides automatic CanExecute UI updates with minimal code.

**Key Characteristics:**
- ✅ **Automatic binding** - Framework calls `View.ActionBinder.Bind(_dispatcher)` for you
- ✅ **Automatic CanExecute updates** - Buttons automatically enable/disable when state changes
- ✅ **Less code** - No event subscriptions needed
- ❌ **Implicit connection** - Event flow happens "behind the scenes" (framework magic)
- ❌ **Harder to debug** - Cannot set breakpoint on automatic binding code

**Implementation:**

```csharp
// 1. Define ViewActions
public static class UserEditorActions
{
    public static readonly ViewAction Save = ViewAction.Create("UserEditor.Save");
    public static readonly ViewAction Delete = ViewAction.Create("UserEditor.Delete");
    public static readonly ViewAction Cancel = ViewAction.Create("UserEditor.Cancel");
}

// 2. View Interface - Exposes ActionBinder property
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    bool HasUnsavedChanges { get; }
    bool HasSelection { get; }

    // Framework-level abstraction
    ViewActionBinder ActionBinder { get; }
}

// 3. View Implementation (Form)
public class UserEditorForm : Form, IUserEditorView
{
    private ViewActionBinder _binder;
    private Button _btnSave;
    private Button _btnDelete;
    private Button _btnCancel;

    // Return the configured binder instance
    public ViewActionBinder ActionBinder => _binder;

    public UserEditorForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();

        // Map UI controls to ViewActions
        _binder.Add(UserEditorActions.Save, _btnSave);
        _binder.Add(UserEditorActions.Delete, _btnDelete);
        _binder.Add(UserEditorActions.Cancel, _btnCancel);

        // DO NOT call Bind() here - framework does it automatically
    }

    // ... other View implementation
}

// 4. Presenter
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IMessageService _messageService;

    public UserEditorPresenter(IMessageService messageService)
    {
        _messageService = messageService;
    }

    protected override void RegisterViewActions()
    {
        // Register action handlers with CanExecute predicates
        Dispatcher.Register(
            UserEditorActions.Save,
            OnSave,
            canExecute: () => View.HasUnsavedChanges);

        Dispatcher.Register(
            UserEditorActions.Delete,
            OnDelete,
            canExecute: () => View.HasSelection);

        Dispatcher.Register(UserEditorActions.Cancel, OnCancel);

        // NOTE: Framework automatically calls View.ActionBinder.Bind(_dispatcher)
        // after this method completes. No manual binding needed!
        //
        // This automatic binding provides:
        // - Control event subscriptions (button clicks → action dispatch)
        // - Automatic CanExecute UI updates (buttons auto enable/disable)
    }

    private void OnSave()
    {
        SaveUser();
        _messageService.ShowInfo("User saved successfully!", "Success");

        // UI automatically updates after action completes
        // (framework triggers CanExecute refresh)
    }

    private void OnDelete()
    {
        if (_messageService.ConfirmYesNo("Delete this user?", "Confirm"))
        {
            DeleteUser();
            _messageService.ShowInfo("User deleted", "Success");
        }
    }

    private void OnCancel()
    {
        RequestClose();
    }
}
```

**Event Flow (Implicit Pattern):**
```
User clicks button
    ↓
ViewActionBinder captures Click event (auto-subscribed by framework)
    ↓
ViewActionBinder invokes callback: action => dispatcher.Dispatch(action)
    ↓
Dispatcher checks CanExecute predicate
    ↓
If true, execute registered handler (OnSave/OnDelete)
    ↓
Dispatcher raises ActionExecuted event
    ↓
ViewActionBinder receives ActionExecuted
    ↓
ViewActionBinder automatically updates all control states (Enabled property)
```

**When to use Implicit Pattern:**
- ✅ Standard CRUD operations
- ✅ Team comfortable with framework conventions
- ✅ Automatic UI updates desired (less manual code)
- ✅ Production applications (battle-tested, reliable)

---

##### Pattern 2: Explicit Event Pattern (For Maximum Control)

**Overview:** The View explicitly raises `ActionRequest` events, and the Presenter explicitly subscribes to them. This provides complete visibility into the event flow at the cost of more code and manual CanExecute management.

**Key Characteristics:**
- ✅ **Explicit subscription** - Clear, visible event subscription code
- ✅ **F12 navigation** - Jump to handler with IDE navigation
- ✅ **Easy debugging** - Set breakpoints on event handlers
- ✅ **Familiar pattern** - Standard .NET event model
- ❌ **Manual CanExecute updates** - Must call `Dispatcher.RaiseCanExecuteChanged()` manually
- ❌ **More code** - Additional events to define and subscribe to
- ❌ **ActionBinder returns null** - Prevents framework auto-binding (avoids double-dispatch)

**Implementation:**

```csharp
// 1. Define ViewActions (same as implicit)
public static class UserEditorActions
{
    public static readonly ViewAction Save = ViewAction.Create("UserEditor.Save");
    public static readonly ViewAction Delete = ViewAction.Create("UserEditor.Delete");
    public static readonly ViewAction Cancel = ViewAction.Create("UserEditor.Cancel");
}

// 2. View Interface - Exposes ActionRequest event
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    bool HasUnsavedChanges { get; }
    bool HasSelection { get; }

    // Explicit event for ViewAction handling
    event EventHandler<ActionRequestEventArgs> ActionRequest;

    // State change events for manual CanExecute updates
    event EventHandler SelectionChanged;
    event EventHandler DataChanged;
}

// 3. View Implementation (Form)
public class UserEditorForm : Form, IUserEditorView
{
    private ViewActionBinder _binder;
    private Button _btnSave;
    private Button _btnDelete;
    private Button _btnCancel;

    // Return null to prevent framework auto-binding
    // (prevents double-dispatch: both event and callback paths active)
    public ViewActionBinder ActionBinder => null;

    // Explicit events
    public event EventHandler<ActionRequestEventArgs> ActionRequest;
    public event EventHandler SelectionChanged;
    public event EventHandler DataChanged;

    public UserEditorForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();

        // Map UI controls to ViewActions
        _binder.Add(UserEditorActions.Save, _btnSave);
        _binder.Add(UserEditorActions.Delete, _btnDelete);
        _binder.Add(UserEditorActions.Cancel, _btnCancel);

        // Subscribe to ActionTriggered and forward to public ActionRequest event
        _binder.ActionTriggered += (sender, e) =>
        {
            ActionRequest?.Invoke(this, e);
        };

        // Manually bind controls (event-only mode)
        // Framework won't auto-bind because ActionBinder returns null
        _binder.Bind();
    }

    // Raise state change events
    private void OnDataChanged()
    {
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ... other View implementation
}

// 4. Presenter
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IMessageService _messageService;

    public UserEditorPresenter(IMessageService messageService)
    {
        _messageService = messageService;
    }

    protected override void OnViewAttached()
    {
        // Explicit subscription using base class helper method
        // OnViewActionTriggered automatically forwards to Dispatcher
        View.ActionRequest += OnViewActionTriggered;
    }

    protected override void OnInitialize()
    {
        // Subscribe to state change events for manual CanExecute updates
        View.SelectionChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
        View.DataChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    }

    protected override void RegisterViewActions()
    {
        // Register handlers with CanExecute predicates (same as implicit pattern)
        Dispatcher.Register(
            UserEditorActions.Save,
            OnSave,
            canExecute: () => View.HasUnsavedChanges);

        Dispatcher.Register(
            UserEditorActions.Delete,
            OnDelete,
            canExecute: () => View.HasSelection);

        Dispatcher.Register(UserEditorActions.Cancel, OnCancel);

        // NOTE: Framework tries to call View.ActionBinder?.Bind(_dispatcher),
        // but ActionBinder returns null, so auto-binding doesn't happen.
        // This prevents double-dispatch (event + callback paths).
    }

    // No need to write manual OnActionRequest handler!
    // OnViewActionTriggered (base class helper) handles routing:
    //   1. Extracts ActionKey from event args
    //   2. Extracts payload if present (for parameterized actions)
    //   3. Calls Dispatcher.Dispatch(key, payload)

    private void OnSave()
    {
        SaveUser();
        _messageService.ShowInfo("User saved successfully!", "Success");

        // Manual CanExecute update happens via View.DataChanged event
    }

    private void OnDelete()
    {
        if (_messageService.ConfirmYesNo("Delete this user?", "Confirm"))
        {
            DeleteUser();
            _messageService.ShowInfo("User deleted", "Success");
        }
    }

    private void OnCancel()
    {
        RequestClose();
    }
}
```

**Event Flow (Explicit Pattern):**
```
User clicks button
    ↓
ViewActionBinder captures Click event (manually bound by View)
    ↓
ViewActionBinder raises ActionTriggered event
    ↓
View forwards to ActionRequest event
    ↓
Presenter's OnViewActionTriggered receives event
    ↓
OnViewActionTriggered extracts ActionKey and payload
    ↓
Calls Dispatcher.Dispatch(actionKey, payload)
    ↓
Dispatcher checks CanExecute predicate
    ↓
If true, execute registered handler (OnSave/OnDelete)
    ↓
User code manually triggers Dispatcher.RaiseCanExecuteChanged()
    ↓
(No automatic UI updates - must subscribe to state events)
```

**When to use Explicit Pattern:**
- ✅ Learning scenarios (understand event flow)
- ✅ Debugging complex action routing issues
- ✅ Team strongly prefers explicit over implicit
- ✅ Need custom routing logic (conditional dispatch)

---

##### Pattern Comparison Table

| Feature | Implicit Pattern | Explicit Pattern |
|---------|------------------|------------------|
| **Code Verbosity** | Minimal | More verbose |
| **ActionBinder Property** | Returns `_binder` instance | Returns `null` (prevents auto-binding) |
| **Binding Setup** | Automatic (framework) | Manual (`_binder.Bind()`) |
| **CanExecute UI Updates** | Automatic | Manual (`RaiseCanExecuteChanged`) |
| **Event Subscription** | None (framework handles it) | `View.ActionRequest += OnViewActionTriggered` |
| **State Events Needed** | No | Yes (SelectionChanged, DataChanged, etc.) |
| **Debugging** | Harder (implicit flow) | Easier (explicit subscriptions) |
| **F12 Navigation** | Limited | Full support |
| **Double-Dispatch Risk** | None | Prevented by returning null |
| **Production Use** | ✅ Recommended | ⚠️ Use when needed |
| **Testability** | Good | Good |

##### OnViewActionTriggered Helper Method

The base class provides `OnViewActionTriggered` helper method that simplifies explicit pattern usage:

**What it does:**
```csharp
protected void OnViewActionTriggered(object sender, ActionRequestEventArgs e)
{
    DispatchAction(e);
}

protected void DispatchAction(ActionRequestEventArgs e)
{
    if (e == null) return;

    var key = e.ActionKey;
    object payload = null;

    // Check if event has payload (parameterized action)
    if (e is IActionRequestEventArgsWithValue valueProvider)
    {
        payload = valueProvider.GetValue();
    }

    // Dispatch to registered handler
    Dispatcher.Dispatch(key, payload);
}
```

**Benefits:**
- ✅ Converts event subscription to one line: `View.ActionRequest += OnViewActionTriggered;`
- ✅ Automatically handles parameterized actions (extracts payload)
- ✅ No manual if-else chains needed
- ✅ Maintains CanExecute predicate support

**Without helper (verbose):**
```csharp
View.ActionRequest += OnActionRequest;

private void OnActionRequest(object sender, ActionRequestEventArgs e)
{
    // Manual routing with if-else chains
    if (e.ActionKey == UserEditorActions.Save && View.HasUnsavedChanges)
        OnSave();
    else if (e.ActionKey == UserEditorActions.Delete && View.HasSelection)
        OnDelete();
    else if (e.ActionKey == UserEditorActions.Cancel)
        OnCancel();
}
```

**With helper (concise):**
```csharp
View.ActionRequest += OnViewActionTriggered;

// That's it! Helper forwards to Dispatcher, which checks CanExecute
```

---

##### Which Pattern Should You Use?

**Use Implicit Pattern (Recommended) when:**
- Building standard CRUD applications
- Team is comfortable with framework conventions
- Automatic UI updates are desired
- Productivity is a priority

**Use Explicit Pattern when:**
- Debugging complex action routing
- Team strongly prefers explicit code
- Learning how the framework works
- Need to understand event flow completely

**Most applications should use the Implicit Pattern.** It's battle-tested, requires less code, and provides automatic CanExecute UI updates. The Explicit Pattern exists for teams that prefer maximum visibility and control.

#### Complete Example

**Complete Examples:**

See `src/WinformsMVP.Samples/ViewActionExample.cs` for **Implicit Pattern** example demonstrating:
- Global and module-specific static ActionKey classes
- **ActionBinder property pattern for proper MVP separation** (recommended)
- CanExecute predicates for dynamic enable/disable
- Automatic UI state updates after action execution
- Multiple controls bound to the same action
- Proper dependency injection with IMessageService
- View Interface with ActionBinder property (no UI types exposed)

See `src/WinformsMVP.Samples/ViewActionExplicitEventExample.cs` for **Explicit Event Pattern** example demonstrating:
- ActionRequest event-based ViewAction handling
- OnViewActionTriggered helper method usage (one-line subscription)
- ActionBinder returns null to prevent framework auto-binding
- Manual CanExecute updates with state change events
- Complete event flow from button click to handler execution
- When and why to use explicit pattern over implicit

See `src/WinformsMVP.Samples/ViewActionWithParametersExample.cs` for parameterized action examples.

See `src/WinformsMVP.Samples/ViewActionStateChangedExample.cs` for state-driven update examples:
- Using RaiseCanExecuteChanged() for state changes
- Handling view events (SelectionChanged, DataChanged)
- Async operations with state updates
- Comparison between action-driven and state-driven patterns

See `src/WinformsMVP.Samples/CheckBoxDemo/` for CheckBox/RadioButton examples:
- CheckBox binding with CheckedChanged events
- RadioButton groups for theme selection
- CanExecute controlling Enabled state
- **Proper MVP separation using ActionBinder property pattern** (no UI types in interface)
- Settings UI pattern with Apply/Reset buttons

See `src/WinformsMVP.Samples/BulkBindingDemo/` for bulk binding examples:
- AddRange with tuples for efficient binding
- AddRange with dictionary for alternative syntax
- Handling surveys/questionnaires with many options
- Best practices for binding multiple similar controls

**Important:** All sample code uses the **ActionBinder property pattern** as the recommended approach. Avoid exposing UI controls (Button, TextBox, etc.) through View interfaces - let the View implementation configure action bindings internally in its constructor via `InitializeActionBindings()`, and expose them through the `ActionBinder` property.

#### Dispatch Middleware Pipeline (Advanced)

`ViewActionDispatcher` exposes an opt-in middleware pipeline for cross-cutting concerns — audit logging, performance measurement, authorization, custom error strategies, and similar things that you don't want to copy-paste into every handler. **Simple presenters need none of this**; the framework's fast path stays zero-overhead until you call `Use(...)`.

##### When NOT to use middleware

| Need | Use this instead |
|------|------------------|
| Decide whether one action runs | `canExecute: () => ...` on `Register` |
| React to a state change after a successful dispatch | `Dispatcher.ActionExecuted` event |
| Log one specific handler | Put the log call in that handler |
| Plain CRUD form with no horizontal concerns | Just `Register` + `Dispatch`, skip middleware |

Reach for middleware when the same wrapping code would otherwise repeat across many handlers or many presenters.

##### Mental model: onion

Middleware uses an onion model identical to ASP.NET Core. Each middleware gets `(context, next)`. Code before `next(context)` runs pre-handler; code after (especially in `finally`) runs post-handler, in reverse order. Not calling `next` short-circuits the pipeline — the handler does not run.

```
Dispatch(Save)
  ┌─ AuditMiddleware.pre
  │  ┌─ ErrorDialogMiddleware.try {
  │  │   ┌─ PerformanceMiddleware (Stopwatch.Start)
  │  │   │   ┌─ [Handler: OnSave]      ← terminal
  │  │   │   └─
  │  │   └─ PerformanceMiddleware (Stopwatch.Stop, log if slow)
  │  └─ ErrorDialogMiddleware } catch { ShowDialog }
  └─ AuditMiddleware.post   (writes audit entry, sees ctx.Exception)
```

##### Two registration tiers

```csharp
// Global (application-wide) — runs OUTERMOST, no presenter can bypass it.
// Set on PlatformServices.Default in your composition root:
PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: register,
    loggerFactory: loggerFactory,
    serviceProvider: null,
    configureDispatcher: d => d
        .Use(new AuditMiddleware(auditSink, () => CurrentUser.Name))
        .Use(new ErrorDialogMiddleware(messages, dispatchLogger)));

// Local (per-presenter) — runs INSIDE the global layer.
// Add inside RegisterViewActions:
protected override void RegisterViewActions()
{
    _dispatcher.Use(new PerformanceMiddleware(Logger, slowThresholdMs: 50));
    _dispatcher.Register(CommonActions.Save, OnSave);
}
```

The contract is **"global registered first = outermost"**. This is enforced by `PresenterBase.SetView` invoking `Platform.ConfigureDispatcher` before user code in `RegisterViewActions` ever runs. Cross-cutting policies that must not be bypassed (audit, authorization) belong at the global tier.

##### Writing a middleware

Implement `IDispatchMiddleware` for reusable middleware:

```csharp
public sealed class PerformanceMiddleware : IDispatchMiddleware
{
    private readonly ILogger _logger;
    private readonly int _thresholdMs;

    public PerformanceMiddleware(ILogger logger, int thresholdMs = 100)
    {
        _logger = logger ?? NullLogger.Instance;
        _thresholdMs = thresholdMs;
    }

    public void Invoke(DispatchContext context, DispatchDelegate next)
    {
        var sw = Stopwatch.StartNew();
        try { next(context); }
        finally
        {
            sw.Stop();
            if (sw.ElapsedMilliseconds > _thresholdMs)
                _logger.LogWarning("Slow dispatch: {Action} took {Ms}ms",
                    context.Action, sw.ElapsedMilliseconds);
        }
    }
}
```

Or use the inline overload for one-off cases:

```csharp
_dispatcher.Use((ctx, next) =>
{
    if (_isReadOnly && IsDestructive(ctx.Action))
        return;                 // short-circuit, handler doesn't run
    next(ctx);
});
```

##### `DispatchContext`

The context object exposes only strong-typed properties. There is no `Items` dictionary — middleware that needs per-dispatch state should carry it on its own instance fields. The minimal surface:

| Property | Type | Notes |
|----------|------|-------|
| `Action` | `ViewAction` | The action being dispatched |
| `Payload` | `object` | Read-only — middleware must not "magic-convert" it |
| `ExpectedPayloadType` | `Type` | `null` for parameterless handlers |
| `HandlerExecuted` | `bool` | `true` only after the terminal handler completes successfully |
| `Exception` | `Exception` | Set by the built-in safety net or by middleware that catches |

##### Exception flow

Handler exceptions propagate up through the middleware chain. Middleware can wrap `next(context)` in `try`/`catch` to intercept them directly (e.g. `ErrorDialogMiddleware`). If no middleware catches, the framework's built-in safety net logs the exception and records it on `context.Exception` — same behavior as the fast path. `ActionExecuted` is raised only when the handler ran to completion **and** `context.Exception` is null.

##### What middleware does NOT see

The pipeline runs *after* the dispatcher's precondition checks. Middleware will not be invoked for:

- Action keys with no registered handler
- Dispatches where `CanExecute` returns `false`
- Payload type mismatches (e.g. dispatching an `int` to a `Register<string>` handler)

These rejections are logged but never reach the pipeline. If you need to audit "user attempted X but it was disabled", check `CanDispatch` from your own code before calling `Dispatch`.

##### Performance

- **Fast path** (no `Use(...)` ever called): zero allocation, no extra indirection. Existing tests in `ViewActionDispatcherPerformanceTests` assert 1M dispatches complete in well under a second.
- **Slow path** (any middleware registered): one `DispatchContext` heap allocation per dispatch, plus N delegate calls (N = middleware count). The compiled pipeline is cached on the dispatcher and rebuilt only when new middleware is added.
- **No reflection, no Expression Trees** — the pipeline is built from plain nested closures, so net40 hosts pay nothing extra.

##### Sample code

See [`src/WinformsMVP.Samples/ViewActionMiddlewareExample.cs`](src/WinformsMVP.Samples/ViewActionMiddlewareExample.cs) for runnable implementations of:

- `AuditMiddleware` — write every dispatch to an audit sink
- `PerformanceMiddleware` — warn on slow dispatches
- `ErrorDialogMiddleware` — replace the built-in safety-net catch with user-facing dialogs

Test coverage lives in [`ViewActionDispatcherMiddlewareTests`](src/WinformsMVP.Samples.Tests/ViewActions/ViewActionDispatcherMiddlewareTests.cs), [`PresenterMiddlewareIntegrationTests`](src/WinformsMVP.Samples.Tests/ViewActions/PresenterMiddlewareIntegrationTests.cs), and [`ViewActionDispatcherPerformanceTests`](src/WinformsMVP.Samples.Tests/ViewActions/ViewActionDispatcherPerformanceTests.cs).

### Navigation System

**WindowNavigator** (`IWindowNavigator`) manages window lifecycle:

**Modal windows:**
```csharp
// Without parameters
var result = navigator.ShowWindowAsModal<TPresenter, TResult>(presenter);

// With parameters
var result = navigator.ShowWindowAsModal<TPresenter, TParam, TResult>(presenter, parameters);
```

**Non-modal windows:**
```csharp
// Without parameters
var window = navigator.ShowWindow<TPresenter>(presenter);

// With parameters
var window = navigator.ShowWindow<TPresenter, TParam>(presenter, parameters);

// With singleton support (keySelector)
var window = navigator.ShowWindow<TPresenter>(
    presenter,
    keySelector: p => documentId  // Only one window per document
);
```

**Window closing flow:** Closing is a **two-direction event-driven model** with strict single-source-of-truth semantics. See the dedicated [Window Closing Pattern](#window-closing-pattern) section below for the canonical examples and rules.

#### Fluent API: omitting `TPresenter` at the call site

The 3-argument generic form above requires the caller to spell out every type argument because C# does not support partial generic type inference. For more readable call sites, the framework provides a Fluent API that splits the call into chained steps where each step infers its own type parameter — leaving only the business result type to specify explicitly.

```csharp
// 3-arg form — verbose: every type argument explicit
var result = Navigator.ShowWindowAsModal<ComposeEmailPresenter, ComposeEmailParameters, bool>(
    presenter, parameters);

// Fluent form — only TResult is explicit
var result = Navigator.For(presenter)         // TPresenter inferred from argument
                      .WithParam(parameters)  // TParam inferred from argument
                      .ShowAsModal<bool>();   // only TResult explicit
```

**How it works**

| Step | Captures via inference | Compile-time constraint |
|------|------------------------|------------------------|
| `.For(presenter)` | `TPresenter` | `TPresenter : IPresenter` |
| `.WithParam(parameters)` | `TParam` | `TPresenter : IInitializable<TParam>` |
| `.ShowAsModal<TResult>()` | — (explicit) | (none) |
| `.ShowAsModal()` | — | (none) |
| `.ShowWindow(...)` / `.ShowWindow<TResult>(...)` | — | (none) |

The `WithParam` extension method adds a **stricter** constraint than the receiver type (`NavigationContext<TPresenter>` only requires `IPresenter`). Because C# allows extension methods to tighten constraints on the receiver's type parameters, passing parameters of a type the presenter cannot consume **fails to compile** — the Fluent API has the same compile-time safety as the 3-argument instance methods.

```csharp
// FooPresenter does NOT implement IInitializable<int>
Navigator.For(fooPresenter).WithParam(42);
// ❌ Compile error: 'FooPresenter' does not satisfy IInitializable<int>
```

**Coverage**

The Fluent API covers all four navigation variants:

```csharp
// Modal, no parameters, no result
Navigator.For(presenter).ShowAsModal();

// Modal, no parameters, with result
var name = Navigator.For(presenter).ShowAsModal<string>();

// Modal, with parameters, no result
Navigator.For(presenter).WithParam(parameters).ShowAsModal();

// Modal, with parameters, with result
var ok = Navigator.For(presenter).WithParam(parameters).ShowAsModal<bool>();

// Non-modal (singleton + onClosed callback supported)
Navigator.For(presenter).ShowWindow<bool>(
    keySelector: p => p.DocumentId,   // p is strongly typed as TPresenter
    onClosed: result => { /* ... */ });

// Non-modal with parameters
Navigator.For(presenter).WithParam(parameters).ShowWindow();
```

**Coexistence with the 3-argument form**

Both styles are fully supported and delegate to the same underlying instance methods on `IWindowNavigator`. The Fluent API is purely additive — existing call sites, `MockWindowNavigator`, and tests work without modification. Choose whichever reads better for each call.

| Aspect | 3-arg form | Fluent form |
|--------|-----------|------------|
| Type parameters to spell out | All (e.g. `<TP, TParam, TResult>`) | Only `<TResult>` (or none) |
| `IInitializable<TParam>` check | Compile-time | Compile-time |
| `keySelector` parameter type | Strongly typed `TPresenter` | Strongly typed `TPresenter` |
| Existing tests/mocks affected | — | No (delegates to same instance method) |
| Reflection / runtime cast | None | None |

The Fluent API is implemented in [WindowNavigatorFluentExtensions.cs](src/WinformsMVP/Services/WindowNavigatorFluentExtensions.cs) and [NavigationContext.cs](src/WinformsMVP/Services/NavigationContext.cs).

### Window Closing Pattern

The framework models window closing as two opposite-direction events, **both purely event-based** (no public `CanClose()` method on the Presenter). Master this mental model first; the code below is a literal expression of it.

#### Mental model: Push vs Pull

| Direction | Who initiates | Event              | Carries     | Typical trigger        |
|-----------|---------------|--------------------|-------------|------------------------|
| **Push**  | Presenter     | `CloseRequested`   | `TResult` + `InteractionStatus` | User clicked Save / Cancel / OK |
| **Pull**  | Framework     | `View.Closing`     | `CloseReason` + `Cancel` flag | User clicked X / Alt+F4 / system shutdown |

Both paths end at WinForms `FormClosed` — so the *same* `InteractionResult<TResult>` flows back to the caller of `ShowWindowAsModal`. The bridge between WinForms `FormClosing` and the framework's abstraction is performed once by `WindowNavigator`; presenters never see WinForms types.

```
┌──── Push (Presenter decides) ──────────┐    ┌──── Pull (external trigger) ──────────┐
│                                        │    │                                       │
│  Presenter.OnSave()                    │    │  User clicks X                        │
│   ├─ business logic (validate/save)    │    │   ↓                                   │
│   ├─ finalize dirty state              │    │  WinForms Form.FormClosing fires      │
│   └─ RequestClose(result)              │    │   ↓                                   │
│        ↓                               │    │  WindowNavigator maps                 │
│   IRequestClose.CloseRequested event   │    │   FormClosingEventArgs ⟶ Window-      │
│        ↓                               │    │   ClosingEventArgs (frameworks own    │
│   WindowNavigator records result,      │    │   CloseReason enum, no WinForms       │
│   then calls form.Close()              │    │   types leak)                         │
│        ↓                               │    │                                       │
│   Form.FormClosing fires    ───────────┼────┤   IWindowView.OnClosing(args)         │
│                                        │    │        ↓                              │
│                                        │    │   IWindowView.Closing event fires     │
│                                        │    │        ↓                              │
│                                        │    │   Presenter handler:                  │
│                                        │    │   ├─ inspect args.Reason              │
│                                        │    │   ├─ check dirty state                │
│                                        │    │   └─ optionally args.Cancel = true    │
│                                        │    │                                       │
└────────────────────────────────────────┘    └───────────────────────────────────────┘
```

#### Single-source-of-truth invariant

The Pull-direction handler (`View.Closing`) is the **only** place dirty-state prompts live. Push-direction handlers (`OnSave`, `OnCancel`, `OnDiscard`) finalize the dirty flag *before* calling `RequestClose`, so the framework's follow-up `FormClosing → IWindowView.Closing` observes a clean state and does not re-prompt.

This was the design choice that replaced the older `IRequestClose.CanClose()` method. With `CanClose()` gone:
- The Presenter has fewer public methods (no service-provider antipattern).
- "Has dirty state?" and "what to ask?" logic lives in exactly one place.
- Tests verify the two directions independently: simulate `View.Closing` for Pull, observe `CloseRequested` for Push.

#### Canonical Presenter

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>,
                                  IRequestClose<UserResult>
{
    public event EventHandler<CloseRequestedEventArgs<UserResult>> CloseRequested;

    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnViewAttached()
    {
        View.Closing += OnViewClosing;            // Pull subscription
    }

    protected override void OnInitialize()
    {
        var user = LoadUser();
        _changeTracker = new ChangeTracker<UserModel>(user);
        View.Bind(_changeTracker.CurrentValue);
    }

    // ── Pull direction ───────────────────────────────────────────────
    private void OnViewClosing(object sender, WindowClosingEventArgs args)
    {
        // Don't block process exit on system shutdown / task manager kill.
        if (args.Reason == CloseReason.SystemShutdown ||
            args.Reason == CloseReason.TaskManager)
            return;

        if (_changeTracker.IsChanged)
        {
            if (!Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"))
                args.Cancel = true;
        }
    }

    // ── Push direction ───────────────────────────────────────────────
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Save, OnSave,
            canExecute: () => _changeTracker.IsChanged);
        Dispatcher.Register(CommonActions.Cancel, OnCancel);
    }

    private void OnSave()
    {
        SaveUser(_changeTracker.CurrentValue);
        _changeTracker.AcceptChanges();           // ← finalize BEFORE RaiseClose
        RaiseClose(new UserResult { ... }, InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();           // ← finalize BEFORE RaiseClose
        RaiseClose(null, InteractionStatus.Cancel);
    }

    private void RaiseClose(UserResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserResult>(result, status));
}
```

#### Required Form boilerplate

Every `Form` implementing `IWindowView` needs **two lines** for the close infrastructure (because WinForms `Form` already has a deprecated `Closing` event and protected `OnClosing` method, the framework members must be explicit-interface implementations to avoid name collisions):

```csharp
private EventHandler<WindowClosingEventArgs> _closing;
event EventHandler<WindowClosingEventArgs> IWindowView.Closing
{
    add => _closing += value;
    remove => _closing -= value;
}
void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
```

No `Form.FormClosing` subscription is needed in the Form — `WindowNavigator` bridges it automatically during `CreateAndBindForm`.

#### `CloseReason` enum

```csharp
public enum CloseReason
{
    Normal,           // User clicked X / Alt+F4 / Presenter triggered Form.Close — DO dirty-check
    SystemShutdown,   // Windows is shutting down — DO NOT block
    TaskManager,      // Forced termination — DO NOT block
    ParentClosing,    // Owner window closing — usually allow silently
    Unknown,
}
```

Mapping from WinForms `FormCloseReason` happens once inside `WindowNavigator.MapCloseReason`; presenter code never sees `System.Windows.Forms.CloseReason`.

#### Implementing `IRequestClose<TResult>`

The contract is intentionally small — just an event. Every Presenter that needs to push a result back implements it the same way:

```csharp
public event EventHandler<CloseRequestedEventArgs<TResult>> CloseRequested;

private void RaiseClose(TResult result, InteractionStatus status)
    => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<TResult>(result, status));
```

That's it. Three lines per Presenter. The framework keeps `IRequestClose<TResult>` as a plain interface rather than a generic base class because the boilerplate is small and a sealed hierarchy would crowd the type system with redundant variants (`<TView, TResult>`, `<TView, TParam, TResult>`, ...).

#### Testing the pattern

Pull direction — simulate the framework triggering close:

```csharp
[Fact]
public void Closing_WithUnsavedChanges_UserCancels_BlocksClose()
{
    _view.SimulateEdit("new value");        // creates dirty state
    _platform.MessageService.ConfirmYesNoResult = false;  // user says "no, keep editing"

    var args = _view.RaiseClosing(CloseReason.Normal);

    Assert.True(args.Cancel);               // Presenter blocked the close
}
```

Push direction — observe `CloseRequested`:

```csharp
[Fact]
public void Save_FiresCloseRequestedWithResult()
{
    _view.SimulateEdit("hello");
    CloseRequestedEventArgs<string> captured = null;
    _presenter.CloseRequested += (s, e) => captured = e;

    _presenter.Dispatch(CommonActions.Save);

    Assert.Equal("hello", captured.Result);
    Assert.Equal(InteractionStatus.Ok, captured.Status);
}
```

See `src/WinformsMVP.Samples/WindowClosingDemo/` for a minimal end-to-end example and `src/WinformsMVP.Samples.Tests/Presenters/WindowClosingTests.cs` for the full set of behavior tests.

### View Mapping Register

**ViewMappingRegister** (`IViewMappingRegister`) manages the mapping between View interfaces and their Form implementations. WindowNavigator uses this mapping to automatically create View instances.

#### Why View Mapping?

When using WindowNavigator, you want to write code like this:

```csharp
var presenter = new SimpleDialogPresenter();
navigator.ShowWindowAsModal(presenter);  // How does it know which Form to create?
```

The framework needs to know: `ISimpleDialogView` → `SimpleDialogForm`

This is where ViewMappingRegister comes in!

#### Registration Patterns

**Pattern 1: Manual Registration (Explicit Control)**

```csharp
var viewMappingRegister = new ViewMappingRegister();

// Register each View explicitly
viewMappingRegister.Register<ISimpleDialogView, SimpleDialogForm>();
viewMappingRegister.Register<IInputDialogView, InputDialogForm>();
viewMappingRegister.Register<IConfirmDialogView, ConfirmDialogForm>();

var navigator = new WindowNavigator(viewMappingRegister);
```

**When to use:**
- ✅ Small applications (< 10 Views)
- ✅ When you want explicit control
- ✅ When Views have custom initialization

**Pattern 2: Automatic Assembly Scanning (Recommended)**

```csharp
var viewMappingRegister = new ViewMappingRegister();

// Automatically scan and register all Views in the assembly
int registered = viewMappingRegister.RegisterFromAssembly(Assembly.GetExecutingAssembly());
// Scans for all Forms implementing IWindowView/IViewBase and registers them

var navigator = new WindowNavigator(viewMappingRegister);
```

**When to use:**
- ✅ Medium to large applications (10+ Views)
- ✅ Convention-based development
- ✅ Reduce boilerplate code

**Pattern 3: Namespace-Scoped Scanning**

```csharp
var viewMappingRegister = new ViewMappingRegister();

// Only scan specific namespaces
viewMappingRegister.RegisterFromNamespace(
    Assembly.GetExecutingAssembly(),
    "MyApp.Dialogs");  // Only Views in MyApp.Dialogs.*

var navigator = new WindowNavigator(viewMappingRegister);
```

**When to use:**
- ✅ Large applications with multiple modules
- ✅ Avoid accidental registration of test Views
- ✅ Organize Views by feature/module

#### Factory Method Registration (Advanced)

For Views that require constructor parameters or special initialization:

```csharp
// Scenario: ComplexDialogForm needs a configuration object
public class ComplexDialogForm : Form, IComplexDialogView
{
    public ComplexDialogForm(AppSettings settings)  // Needs parameter!
    {
        _settings = settings;
        InitializeComponent();
    }
}

// Solution: Use factory method
var settings = LoadAppSettings();
viewMappingRegister.Register<IComplexDialogView>(() => new ComplexDialogForm(settings));

// Now WindowNavigator can create it with parameters
var presenter = new ComplexDialogPresenter();
navigator.ShowWindowAsModal(presenter);  // Works! Uses factory to create View
```

**When to use:**
- ✅ Views need constructor parameters
- ✅ Views need dependency injection
- ✅ Views require special initialization logic

#### Override Registration (Testing)

Replace real Views with mocks for testing:

```csharp
// Production registration
viewMappingRegister.Register<ISimpleDialogView, SimpleDialogForm>();

// Test override
viewMappingRegister.Register<ISimpleDialogView, MockSimpleDialogForm>(allowOverride: true);
// Now WindowNavigator will use MockSimpleDialogForm instead
```

#### Auto-Scan Requirements

For `RegisterFromAssembly()` to work, your Form classes must:

1. ✅ Inherit from `System.Windows.Forms.Form`
2. ✅ Implement an interface that extends `IWindowView` or `IViewBase`
3. ✅ Have a **public parameterless constructor** (or use factory registration)
4. ✅ Not be abstract

**Example:**
```csharp
// ✅ Will be auto-registered
public class SimpleDialogForm : Form, ISimpleDialogView
{
    public SimpleDialogForm()  // Parameterless constructor
    {
        InitializeComponent();
    }
}

// ❌ Will NOT be auto-registered (needs parameters)
public class ComplexDialogForm : Form, IComplexDialogView
{
    public ComplexDialogForm(AppSettings settings)  // Has parameters
    {
        InitializeComponent();
    }
}
// Solution: Use factory registration for ComplexDialogForm
```

#### Complete Setup Example

**In Program.cs:**
```csharp
static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Setup ViewMappingRegister
        var viewMappingRegister = new ViewMappingRegister();

        // Option A: Auto-scan (recommended for most apps)
        viewMappingRegister.RegisterFromAssembly(Assembly.GetExecutingAssembly());

        // Option B: Manual registration (more control)
        // viewMappingRegister.Register<IMainView, MainForm>();
        // viewMappingRegister.Register<IAboutView, AboutForm>();

        // Option C: Factory registration for special cases
        var settings = LoadSettings();
        viewMappingRegister.Register<ISettingsView>(() => new SettingsForm(settings));

        // Create WindowNavigator
        var navigator = new WindowNavigator(viewMappingRegister);

        // Create and show main window
        var mainPresenter = new MainPresenter(navigator);
        navigator.ShowWindow(mainPresenter);

        Application.Run();
    }
}
```

#### Checking Registration Status

```csharp
// Check if a View is registered
if (viewMappingRegister.IsRegistered(typeof(ISimpleDialogView)))
{
    Console.WriteLine("SimpleDialogView is registered");
}

// Get registered count from auto-scan
int count = viewMappingRegister.RegisterFromAssembly(Assembly.GetExecutingAssembly());
Console.WriteLine($"Registered {count} Views automatically");
```

#### Common Patterns

**Pattern: Module-Based Registration**

```csharp
// For large apps, organize by module
public static class ViewMappingConfiguration
{
    public static void ConfigureViews(IViewMappingRegister register)
    {
        // Core dialogs
        RegisterCoreViews(register);

        // Feature modules
        RegisterUserManagementViews(register);
        RegisterReportingViews(register);
        RegisterSettingsViews(register);
    }

    private static void RegisterCoreViews(IViewMappingRegister register)
    {
        register.RegisterFromNamespace(Assembly.GetExecutingAssembly(), "MyApp.Core.Dialogs");
    }

    private static void RegisterUserManagementViews(IViewMappingRegister register)
    {
        register.RegisterFromNamespace(Assembly.GetExecutingAssembly(), "MyApp.UserManagement.Views");
    }

    // ...
}
```

**Pattern: Test Mocking**

```csharp
public class MyPresenterTests
{
    [Fact]
    public void Test_ShowDialog()
    {
        // Arrange
        var viewMappingRegister = new ViewMappingRegister();

        // Use mock Views instead of real Forms
        viewMappingRegister.Register<ISimpleDialogView, MockSimpleDialogForm>();

        var navigator = new WindowNavigator(viewMappingRegister);
        var presenter = new MyPresenter(navigator);

        // Act
        presenter.ShowDialog();

        // Assert
        // ...
    }
}
```

#### Key Benefits

| Benefit | Manual Registration | Auto-Scan | Factory Method |
|---------|---------------------|-----------|----------------|
| **Type Safety** | ✅ Compile-time | ✅ Runtime | ✅ Compile-time |
| **Less Code** | ❌ Verbose | ✅ Minimal | ⚠️ Moderate |
| **Explicit Control** | ✅ Full control | ❌ Convention-based | ✅ Full control |
| **Constructor Parameters** | ❌ Not supported | ❌ Not supported | ✅ Fully supported |
| **Best For** | Small apps | Medium/large apps | Complex Views |

#### Troubleshooting

**Error: "View インターフェース IXxxView に対応する実装型が見つかりません"**

Solution:
1. Check that you registered the View: `viewMappingRegister.Register<IXxxView, XxxForm>();`
2. Or use auto-scan: `viewMappingRegister.RegisterFromAssembly(Assembly.GetExecutingAssembly());`
3. Verify XxxForm has a parameterless constructor

**Error: "既に登録されています"**

Solution:
- Remove duplicate registrations, OR
- Use `allowOverride: true` if you want to replace existing mapping

**Auto-scan finds 0 Views**

Check that your Forms:
1. Implement an interface extending `IWindowView` or `IViewBase`
2. Have public parameterless constructors
3. Are not abstract classes
4. Are in the correct assembly/namespace

### Services Layer

Common application services accessed via `ICommonServices`. These services are essential for maintaining MVP principles by abstracting WinForms UI dependencies.

**Service Interfaces:**

- **IMessageService**: Message boxes and toast notifications
  - Use instead of `MessageBox.Show()`
  - Methods: `ShowInfo()`, `ShowWarning()`, `ShowError()`, `ConfirmYesNo()`, etc.
  - Supports both centered and positioned dialogs (`ShowInfoAt()`, etc.)

- **IDialogProvider**: Standard WinForms dialogs (OpenFile, SaveFile, FolderBrowser, etc.)
  - Use instead of directly creating dialog instances
  - Returns results through abstracted interfaces

- **IFileService**: File I/O operations
  - Abstracts file system access for testability

- **ILogger / ILoggerFactory**: Structured logging using Microsoft.Extensions.Logging
  - Use instead of `Console.WriteLine()` or `Debug.WriteLine()`
  - Access via `Logger` property in all presenters
  - Supports structured logging with parameterized messages
  - Extensible via providers (Debug, Console, File, Application Insights, Seq, etc.)
  - Default: Debug provider for development
  - See [Logging](#logging) section for details

### Logging

The framework ships with its own minimal logging abstraction in the **`WinformsMVP.Logging`** namespace. The main `WinformsMVP` package has **zero external dependencies** so it can multi-target `net40;net48`. Host applications that want a richer ecosystem either implement the contract directly or write a small adapter — see [`MultiProjectDemo.Shell/Logging/`](src/MultiProjectDemo.Shell/Logging/) for a ~30-line Microsoft.Extensions.Logging bridge you can copy.

#### Why an In-House Abstraction?

- **net40 compatibility**: `Microsoft.Extensions.Logging.Abstractions` requires net461+. The framework's own contract is BCL-only so it runs on .NET Framework 4.0.
- **Zero coupling**: The main package never references Microsoft.Extensions.Logging. Hosts pay for the dependency only if they want it.
- **Familiar shape**: Method names (`LogInformation`, `LogError`, ...), enum values (`LogLevel.Information`, ...), and the `{Name}` placeholder syntax all match `Microsoft.Extensions.Logging`, so call sites read identically and migration is mechanical.
- **Adapter, not fork**: A thin application-level adapter forwards to `Microsoft.Extensions.Logging.LoggerExtensions.Log<TState>(...)` directly, so structured properties, scopes, and providers flow through untouched when a host opts in.

#### The Contract

`WinformsMVP.Logging` defines two interfaces and one enum:

```csharp
namespace WinformsMVP.Logging
{
    public interface ILogger
    {
        bool IsEnabled(LogLevel level);
        void Log(LogLevel level, Exception exception, string message, params object[] args);
    }

    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        ILogger CreateLogger(Type type);
    }

    public enum LogLevel
    {
        Trace = 0, Debug = 1, Information = 2, Warning = 3,
        Error = 4, Critical = 5, None = 6,
    }
}
```

The numeric values of `LogLevel` deliberately match `Microsoft.Extensions.Logging.LogLevel` so the adapter can map by cast.

Convenience extension methods (`LogInformation`, `LogWarning`, `LogError(ex, ...)`, `LogCritical`, etc.) live in `WinformsMVP.Logging.LoggerExtensions` with the same signatures as their M.E.L. counterparts, so existing call sites that read `Logger.LogInformation("...", arg)` keep working unchanged.

#### Using Logger in Presenters

All presenters have automatic access to logging via the `Logger` property:

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    // No constructor needed - Logger property automatically available!

    protected override void OnInitialize()
    {
        Logger.LogInformation("MyPresenter initialized");
    }

    private void OnSave()
    {
        try
        {
            var userId = GetCurrentUserId();
            SaveData();

            // Named placeholders are supported - normalized by MessageFormatter
            Logger.LogInformation("User {UserId} saved data successfully", userId);
        }
        catch (Exception ex)
        {
            // Exception logging with context
            Logger.LogError(ex, "Failed to save data for user {UserId}", userId);
            Messages.ShowError("Failed to save data", "Error");
        }
    }

    private void OnComplexOperation()
    {
        // Different log levels
        Logger.LogDebug("Starting complex operation");
        Logger.LogInformation("Processing started");
        Logger.LogWarning("This might take a while");

        if (errorCondition)
        {
            Logger.LogError("Operation failed due to {Reason}", reason);
        }
    }
}
```

#### Log Levels

| Level | When to Use | Example |
|-------|-------------|---------|
| **Debug** | Detailed diagnostic information | `Logger.LogDebug("Cache hit for key {Key}", key)` |
| **Information** | General informational messages | `Logger.LogInformation("User {UserName} logged in", userName)` |
| **Warning** | Potentially harmful situations | `Logger.LogWarning("Retry attempt {Attempt} of {MaxAttempts}", attempt, max)` |
| **Error** | Error events that don't stop execution | `Logger.LogError(ex, "Failed to process order {OrderId}", orderId)` |
| **Critical** | Very serious errors causing app failure | `Logger.LogCritical(ex, "Database connection lost")` |

#### Structured Logging Best Practices

Both M.E.L.-style named placeholders (`{UserName}`) and `string.Format`-style indexed placeholders (`{0}`) are accepted. Named placeholders are normalized to indexed in declaration order by `WinformsMVP.Logging.MessageFormatter` before formatting, so legacy call sites that came from M.E.L. continue to format correctly.

When an application-level adapter forwards to M.E.L., structured properties flow through unchanged (the adapter calls `Ms.LoggerExtensions.Log` rather than pre-formatting the string).

**✅ Good - Structured parameters:**
```csharp
// Named placeholders read like M.E.L. and are captured as structured data
// when a forwarding adapter to M.E.L. is wired in.
Logger.LogInformation("User {UserName} opened document {DocumentId}", userName, docId);
```

**❌ Bad - String interpolation:**
```csharp
// String interpolation loses structure - can't query by UserName in M.E.L. providers
Logger.LogInformation($"User {userName} opened document {docId}");
```

#### Built-In Logger Implementations

Two implementations ship in the main package:

| Type | Output | Dependencies | Use Case |
|------|--------|--------------|----------|
| `NullLogger` / `NullLoggerFactory` | Discards everything | None | Default; tests; performance-critical paths |
| `DebugLogger` / `DebugLoggerFactory` | `System.Diagnostics.Debug.WriteLine` | BCL only (works on net40) | Visual Studio Output window during development |

Reach for `NullLoggerFactory.Instance` (it is a singleton) when you want to opt out entirely; the framework already falls back to it when no factory is configured.

#### Three Configuration Paths

**Path 1 — No configuration (default, silent):**

```csharp
// PlatformServices.Default uses NullLoggerFactory automatically.
// Logger.LogInformation(...) calls compile, execute, and produce no output.
PlatformServices.Default = new DefaultPlatformServices();
```

**Path 2 — `DebugLoggerFactory` (BCL-only, net40-compatible):**

```csharp
using WinformsMVP.Logging;
using WinformsMVP.Services.Implementations;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Zero external dependencies; output goes to Visual Studio Debug window.
        PlatformServices.Default = new DefaultPlatformServices(
            viewMappingRegister: null,
            loggerFactory: new DebugLoggerFactory());

        Application.Run(new MainForm());
    }
}
```

**Path 3 — Microsoft.Extensions.Logging bridge via an application-level adapter (net48 hosts only):**

The framework does NOT ship an adapter package. Real applications write a small adapter at the composition root. See [`MultiProjectDemo.Shell/Logging/`](src/MultiProjectDemo.Shell/Logging/) for a complete, copy-pastable ~30-line implementation (`MicrosoftLoggerAdapter` + `MicrosoftLoggerFactoryAdapter` + `MicrosoftLoggingExtensions.AsFrameworkLoggerFactory()`).

Once the adapter is in the host project, the composition root looks like this:

```csharp
using Microsoft.Extensions.Logging;
using MyApp.Logging;                              // your application's adapter namespace
using WinformsMVP.Services.Implementations;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Build a Microsoft.Extensions.Logging factory the usual way ...
        var msFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddDebug()
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });

        // ... then adapt it to the framework's ILoggerFactory contract via
        // the application's own AsFrameworkLoggerFactory() extension.
        PlatformServices.Default = new DefaultPlatformServices(
            viewMappingRegister: null,
            loggerFactory: msFactory.AsFrameworkLoggerFactory());

        Application.Run(new MainForm());
    }
}
```

> **net40 hosts cannot use Path 3.** `Microsoft.Extensions.Logging.Abstractions` requires net461+, so this approach is net48-only. On net40, use Path 1 (silent) or Path 2 (`DebugLoggerFactory`).

#### What the M.E.L. Bridge Unlocks (Provider Examples)

Once your application's `AsFrameworkLoggerFactory()` adapter is wired in on a net48 host, the entire `Microsoft.Extensions.Logging` provider ecosystem becomes available. Configure the provider on the inner `msFactory` exactly as you would in ASP.NET Core; the framework's `Logger.LogInformation(...)` calls flow through unchanged.

**Example: Azure Application Insights**

```csharp
// 1. Install NuGet: Microsoft.Extensions.Logging.ApplicationInsights
// 2. Build the M.E.L. factory:
var msFactory = LoggerFactory.Create(builder =>
{
    builder.AddApplicationInsights(
        configureTelemetryConfiguration: config =>
        {
            config.ConnectionString = "InstrumentationKey=xxx";
        },
        configureApplicationInsightsLoggerOptions: options => { });
});

// 3. Bridge to the framework:
PlatformServices.Default = new DefaultPlatformServices(
    register, msFactory.AsFrameworkLoggerFactory());

// 4. Use correlation IDs to link client and server logs:
Logger.LogInformation("Submitting order {OrderId} with correlation {CorrelationId}",
    orderId, correlationId);
// Query in Application Insights: WHERE CorrelationId = 'xxx'
```

**Example: Seq (local development)**

```csharp
// 1. Install NuGet: Seq.Extensions.Logging
// 2. Run Seq locally: docker run -d -p 5341:80 datalust/seq
var msFactory = LoggerFactory.Create(builder => builder.AddSeq("http://localhost:5341"));
PlatformServices.Default = new DefaultPlatformServices(register, msFactory.AsFrameworkLoggerFactory());
// 3. View logs in browser: http://localhost:5341
```

**Example: File logging via Serilog**

```csharp
// 1. Install NuGet: Serilog.Extensions.Logging.File
var msFactory = LoggerFactory.Create(builder =>
    builder.AddFile("logs/app-{Date}.log", minimumLevel: LogLevel.Information));
PlatformServices.Default = new DefaultPlatformServices(register, msFactory.AsFrameworkLoggerFactory());
```

#### Testing with Logging

**Default behaviour (NullLoggerFactory, no output):**

```csharp
[Fact]
public void Test_MyPresenter()
{
    // MockPlatformServices initializes LoggerFactory to NullLoggerFactory.Instance.
    var mockServices = new MockPlatformServices();
    var presenter = new MyPresenter();
    presenter.SetPlatformServices(mockServices);

    presenter.AttachView(mockView);
    presenter.Initialize();

    // Logger calls compile and execute but produce no output - zero overhead.
}
```

**Verifying log messages with a capturing logger:**

```csharp
private class MockLogger : ILogger
{
    public List<(LogLevel Level, string Message, Exception Ex)> Entries { get; } = new();
    public bool IsEnabled(LogLevel level) => true;
    public void Log(LogLevel level, Exception exception, string message, params object[] args)
        => Entries.Add((level, MessageFormatter.Format(message, args), exception));
}

private class MockLoggerFactory : ILoggerFactory
{
    public MockLogger Logger { get; } = new MockLogger();
    public ILogger CreateLogger(string categoryName) => Logger;
    public ILogger CreateLogger(Type type) => Logger;
}

[Fact]
public void OnSave_ShouldLogSuccess()
{
    var loggerFactory = new MockLoggerFactory();
    var platform = new DefaultPlatformServices(null, loggerFactory);

    var presenter = new MyPresenter();
    presenter.SetPlatformServices(platform);
    presenter.AttachView(mockView);
    presenter.Initialize();

    presenter.TestDispatcher.Dispatch(CommonActions.Save);

    Assert.Contains(loggerFactory.Logger.Entries,
        e => e.Level == LogLevel.Information && e.Message.Contains("saved"));
}
```

See `src/WinformsMVP.Samples.Tests/LoggingIntegrationTests.cs` for the full set of test patterns (mock logger, mock factory, structured-message verification, exception logging).

#### Complete Example

See `src/WinformsMVP.Samples/LoggingDemoExample.cs` for a working example demonstrating:
- Different log levels (Debug, Information, Warning, Error)
- Named-placeholder structured logging via `MessageFormatter`
- Exception logging with context
- Custom logger factory configuration

For a runnable Microsoft.Extensions.Logging bridge implemented as application code (the recommended approach for net48 hosts), see [`MultiProjectDemo.Shell/Logging/`](src/MultiProjectDemo.Shell/Logging/) and its wiring in [`MultiProjectDemo.Shell/Program.cs`](src/MultiProjectDemo.Shell/Program.cs).

#### Key Properties

| Property | Description |
|----------|-------------|
| **net40-compatible** | Main package has no M.E.L. dependency; multi-targets `net40;net48` |
| **Silent by default** | `NullLoggerFactory.Instance` — no surprise output, no startup cost |
| **Familiar surface** | Extension method names and placeholder syntax match M.E.L. |
| **Opt-in ecosystem** | M.E.L. providers light up via a ~30-line application-level adapter on net48 |
| **Testable** | Two-method `ILogger` contract is trivial to mock |
| **Structured-preserving** | A forwarding adapter calls M.E.L.'s `Log<TState>` directly, so properties survive |

### Platform Services Access

All presenters have **built-in access** to platform services through convenience properties. No constructor injection needed for basic services!

**Using services in presenters:**

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    // No constructor needed - Platform services automatically available!

    protected override void OnInitialize()
    {
        // Use convenience properties
        Messages.ShowInfo("Welcome!", "App");  // IMessageService

        var file = Dialogs.ShowOpenFileDialog("Select file");  // IDialogProvider
        if (file != null)
        {
            var content = Files.ReadAllText(file);  // IFileService
        }
    }

    private void OnSave()
    {
        SaveData();
        Messages.ShowInfo("Data saved!");  // Simple and clean!
    }
}
```

**Available convenience properties:**
- `Messages` - IMessageService (ShowInfo, ShowError, ConfirmYesNo, etc.)
- `Dialogs` - IDialogProvider (OpenFileDialog, SaveFileDialog, FolderBrowser, etc.)
- `Files` - IFileService (ReadAllText, WriteAllText, file operations)
- `Platform` - ICommonServices (full service container for custom services)

**Testing with mock services:**

```csharp
[Fact]
public void Test_MyPresenter()
{
    // 1. Create mock services
    var mockServices = new MockCommonServices();

    // 2. Create presenter with mock platform
    var presenter = new MyPresenter()
        .WithPlatformServices(mockServices);  // Inject mocks before initialization

    // 3. Attach view and initialize
    presenter.AttachView(mockView);
    presenter.Initialize();

    // 4. Verify mock calls
    Assert.True(mockServices.MessageService.InfoMessageShown);
}
```

**Legacy pattern (still supported):**

If you need to inject services manually (e.g., for business-specific services), you can still use constructor injection:

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private readonly IUserRepository _users;  // Business service

    public MyPresenter(IUserRepository users)
    {
        _users = users;  // Manual injection for custom services
    }

    private void OnSave()
    {
        _users.Save(userData);
        Messages.ShowInfo("Saved!");  // Platform services still available
    }
}
```

### Dependency Injection Patterns

The framework supports **three dependency injection patterns** to accommodate different scenarios from legacy code to modern practices.

#### Pattern 1: Service Locator (Recommended for Simple Presenters)

Use the built-in `PlatformServices.Default` for platform services. No constructor needed!

```csharp
public class SimpleDemoPresenter : WindowPresenterBase<ISimpleDemoView>
{
    // No constructor - uses PlatformServices.Default automatically

    protected override void OnInitialize()
    {
        // Direct access via convenience properties
        Messages.ShowInfo("Hello!");
        var file = Dialogs.ShowOpenFileDialog();
    }
}
```

**When to use:**
- ✅ Simple presenters that only need platform services (IMessageService, IDialogProvider, IFileService)
- ✅ Rapid prototyping
- ✅ Legacy code migration (no constructor changes needed)

**Advantages:**
- Zero boilerplate - no constructor needed
- Automatic service access via properties (`Messages`, `Dialogs`, `Files`)
- Easy for beginners

**Disadvantages:**
- Harder to unit test (requires `WithPlatformServices()` helper)
- Global state dependency

#### Pattern 2: Constructor Injection (Recommended for Testable Code)

Explicitly inject dependencies through the constructor for better testability and clarity.

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private readonly IMessageService _messageService;
    private readonly IUserRepository _userRepository;

    public UserEditorPresenter(
        IMessageService messageService,
        IUserRepository userRepository)
    {
        _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    protected override void OnInitialize()
    {
        var users = _userRepository.GetAll();
        View.Users = users;
    }

    private void OnSave()
    {
        _userRepository.Save(userData);
        _messageService.ShowInfo("User saved!", "Success");
    }
}
```

**When to use:**
- ✅ Production code that needs unit testing
- ✅ When you have custom business services (IUserRepository, IOrderService, etc.)
- ✅ When dependencies are explicit and documented

**Advantages:**
- Excellent testability - easy to inject mocks
- Explicit dependencies visible in constructor
- Follows SOLID principles
- Platform services still available via `Messages`, `Dialogs`, `Files` properties

**Disadvantages:**
- More boilerplate code
- Requires manual instantiation or DI container

#### Pattern 3: Hybrid Pattern (Best of Both Worlds)

Use constructor injection for business services, convenience properties for platform services.

```csharp
public class OrderProcessorPresenter : WindowPresenterBase<IOrderProcessorView>
{
    private readonly IOrderService _orderService;  // Business service via constructor

    public OrderProcessorPresenter(IOrderService orderService)
    {
        _orderService = orderService;
    }

    protected override void OnInitialize()
    {
        var orders = _orderService.GetPendingOrders();
        View.Orders = orders;
    }

    private void OnProcess()
    {
        try
        {
            _orderService.ProcessOrders(selectedOrders);
            Messages.ShowInfo("Orders processed!", "Success");  // Platform service via property
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Processing failed: {ex.Message}", "Error");
        }
    }

    private void OnExport()
    {
        var result = Dialogs.ShowSaveFileDialog("Export Orders");  // Platform service via property
        if (result.IsSuccess)
        {
            _orderService.ExportTo(result.Value);
        }
    }
}
```

**When to use:**
- ✅ Most production scenarios
- ✅ Mix of business logic and UI interactions
- ✅ Keep constructor focused on business dependencies

**Advantages:**
- Clean constructor (only business services)
- Easy access to platform services (Messages, Dialogs, Files)
- Great testability
- Best readability

#### Migration from Legacy Code

**Scenario 1: Migrating from MessageBox.Show()**

Before (Legacy WinForms):
```csharp
private void btnSave_Click(object sender, EventArgs e)
{
    try
    {
        SaveData();
        MessageBox.Show("Data saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
```

After (MVP with Service Locator - easiest migration):
```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    // No constructor change needed!

    private void OnSave()
    {
        try
        {
            SaveData();
            Messages.ShowInfo("Data saved!", "Success");  // Replace MessageBox with Messages property
        }
        catch (Exception ex)
        {
            Messages.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

After (MVP with Constructor Injection - better testability):
```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private readonly IMessageService _messageService;

    public MyPresenter(IMessageService messageService)
    {
        _messageService = messageService;
    }

    private void OnSave()
    {
        try
        {
            SaveData();
            _messageService.ShowInfo("Data saved!", "Success");
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"Error: {ex.Message}", "Error");
        }
    }
}
```

**Scenario 2: Migrating from OpenFileDialog**

Before (Legacy):
```csharp
private void btnOpen_Click(object sender, EventArgs e)
{
    using (var dialog = new OpenFileDialog())
    {
        dialog.Filter = "Text files (*.txt)|*.txt";
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadFile(dialog.FileName);
        }
    }
}
```

After (MVP):
```csharp
private void OnOpenFile()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "Text files (*.txt)|*.txt"
    });

    if (result.IsSuccess)
    {
        LoadFile(result.Value);
    }
}
```

#### Testing Patterns

**Testing with Service Locator (requires helper):**

```csharp
[Fact]
public void Test_SimpleDemoPresenter()
{
    // Arrange
    var mockServices = new MockPlatformServices();
    var mockView = new MockSimpleDemoView();

    var presenter = new SimpleDemoPresenter()
        .WithPlatformServices(mockServices);  // Inject mocks before Initialize

    // Act
    presenter.AttachView(mockView);
    presenter.Initialize();

    // Assert
    Assert.True(mockServices.MessageService.InfoMessageShown);
}
```

**Testing with Constructor Injection (natural):**

```csharp
[Fact]
public void Test_UserEditorPresenter()
{
    // Arrange
    var mockMessageService = new MockMessageService();
    var mockUserRepository = new MockUserRepository();
    var mockView = new MockUserEditorView();

    var presenter = new UserEditorPresenter(mockMessageService, mockUserRepository);

    // Act
    presenter.AttachView(mockView);
    presenter.Initialize();

    // Assert
    Assert.True(mockMessageService.InfoMessageShown);
    Assert.Equal(3, mockView.Users.Count);
}
```

#### Integration with Microsoft.Extensions.DependencyInjection (Optional)

For projects that want to use a full DI container, you can integrate `Microsoft.Extensions.DependencyInjection` while maintaining compatibility with legacy code.

**1. Setup in Program.cs:**

```csharp
using Microsoft.Extensions.DependencyInjection;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Build DI container
        var services = new ServiceCollection();

        // Register framework services
        services.AddSingleton<IPlatformServices, DefaultPlatformServices>();
        services.AddSingleton<IMessageService, MessageService>();
        services.AddSingleton<IDialogProvider, DialogProvider>();
        services.AddSingleton<IFileService, FileService>();

        // Register business services
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrderService, OrderService>();

        // Register presenters
        services.AddTransient<UserEditorPresenter>();
        services.AddTransient<OrderProcessorPresenter>();

        var serviceProvider = services.BuildServiceProvider();

        // Configure PlatformServices to use the container
        PlatformServices.Default = serviceProvider.GetRequiredService<IPlatformServices>();

        // Create and show main form
        var mainForm = new MainForm(serviceProvider);
        Application.Run(mainForm);
    }
}
```

**2. Using the container in MainForm:**

```csharp
public class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    private void btnEditUser_Click(object sender, EventArgs e)
    {
        // Resolve presenter from container
        var presenter = _serviceProvider.GetRequiredService<UserEditorPresenter>();

        // Show window using WindowNavigator
        var navigator = new WindowNavigator();
        navigator.ShowWindowAsModal(presenter);
    }
}
```

**Benefits of DI container integration:**
- ✅ Centralized service registration
- ✅ Lifetime management (Singleton, Scoped, Transient)
- ✅ Automatic dependency resolution
- ✅ Legacy code still works (uses PlatformServices.Default)

**Important:** DI container integration is **optional**. The framework works perfectly without it using Service Locator or manual Constructor Injection.

#### Choosing the Right Pattern

| Scenario | Recommended Pattern | Reason |
|----------|---------------------|--------|
| Simple demo/prototype | Service Locator | Fastest, least code |
| Legacy code migration | Service Locator | No constructor changes |
| Testable business logic | Constructor Injection | Explicit dependencies |
| Production app (mixed) | Hybrid | Business services in constructor, platform services via properties |
| Large enterprise app | DI Container + Constructor Injection | Centralized configuration, automatic resolution |

#### Advanced: Multi-Project DI Integration (`WinformsMVP.DependencyInjection`)

For large solutions with multiple UI projects, the framework offers an **optional companion package** `WinformsMVP.DependencyInjection` that integrates cleanly with `Microsoft.Extensions.DependencyInjection`. The main `WinformsMVP` package itself only depends on the BCL `IServiceProvider` abstraction, so legacy projects pay nothing for this.

##### Two orthogonal layers of resolution

The framework intentionally separates two concerns that are often conflated:

| Layer | Solves | Provided By |
|-------|--------|-------------|
| **View resolution** | "Given `IFooView`, which `Form` class?" | `IViewMappingRegister` (main package) |
| **Service / Presenter resolution** | "Resolve constructor dependencies" | `IServiceProvider` (any DI container) |

Legacy projects use `IViewMappingRegister` alone (with optional `Func<T>` factories for manual DI). DI-enabled projects bridge the two layers via:

- `WithServiceProvider(provider)` — decorates the View register so unregistered View interfaces fall back to the DI container.
- `IPresenterFactory` — bridges the gap when a **parent Presenter needs to create a child Presenter** whose constructor deps come from DI.

The two bridges are independent — you can use either, both, or neither.

##### IPresenterFactory: child Presenter creation

A parent Presenter that opens a child window cannot just call `new ChildPresenter()` when the child has DI-managed constructor deps. Inject `IPresenterFactory`:

```csharp
public class MainPresenter : WindowPresenterBase<IMainView>
{
    private readonly IPresenterFactory _presenters;

    public MainPresenter(IPresenterFactory presenters)
    {
        _presenters = presenters;
    }

    private void OnEditUser(int userId)
    {
        // (1) IPresenterFactory resolves constructor deps (IUserRepository, ILogger, ...)
        var presenter = _presenters.Create<EditUserPresenter>();

        // (2) Navigator passes runtime parameters via IInitializable<TParam>.Initialize
        var parameters = new EditUserParameters { UserId = userId, IsReadOnly = false };
        var result = Navigator.For(presenter)
                              .WithParam(parameters)
                              .ShowAsModal<UserResult>();
    }
}
```

**Key separation**:

| Constructor (DI managed) | `Initialize(TParam)` (Navigator managed) |
|-------------------------|------------------------------------------|
| Stable deps: repositories, services, loggers | Runtime data: ids, paths, modes, contexts |
| One per container lifetime | Different on every window open |
| `IPresenterFactory.Create<T>()` resolves them | `Navigator.WithParam(...)` passes them |

The framework's official answer to "runtime parameters in a Presenter" is the `IInitializable<TParam>` pattern — **never** mix DI-resolved deps and runtime args in the same constructor. Use a Parameters class.

##### Module-based registration with `IModuleRegistrar`

Each UI sub-project owns its View and service registrations through one entry point:

```csharp
// MyApp.UserModule.UI/UserModuleRegistrar.cs
public class UserModuleRegistrar : IModuleRegistrar
{
    public void RegisterViews(IViewMappingRegister registry)
        => registry.RegisterFromAssembly(typeof(UserModuleRegistrar).Assembly);

    public void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<UserListPresenter>();
        services.AddTransient<EditUserPresenter>();
        services.AddSingleton<IUserRepository, UserRepository>();
    }
}
```

For modules that don't need DI integration (legacy modules in an otherwise-DI app), implement the smaller `IViewModuleRegistrar` from the main package instead — it only has `RegisterViews`.

##### Full bootstrap example (Shell project)

```csharp
// MyApp.Shell/Program.cs
[STAThread]
static void Main()
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    // 1. Create the shared View registry.
    var viewRegistry = new ViewMappingRegister();

    // 2. Create the DI container and let every module contribute.
    var services = new ServiceCollection();
    services.RegisterModules(viewRegistry,
        new UserModuleRegistrar(),
        new OrderModuleRegistrar(),
        new ReportModuleRegistrar());

    // 3. Register the framework's own services (IPresenterFactory, etc.).
    services.AddWinformsMVP(viewRegistry);

    // 4. Build the provider and wire everything into PlatformServices.
    var provider = services.BuildServiceProvider();
    var loggerFactory = LoggerFactory.Create(b => b.AddDebug());

    PlatformServices.Default = new DefaultPlatformServices(
        viewMappingRegister: viewRegistry,
        loggerFactory: loggerFactory,
        serviceProvider: provider);

    // 5. Resolve the root Presenter from the container and run.
    var mainPresenter = provider.GetRequiredService<MainShellPresenter>();
    PlatformServices.Default.WindowNavigator.ShowWindow(mainPresenter);
    Application.Run();
}
```

##### Legacy project bootstrap (zero DI)

```csharp
[STAThread]
static void Main()
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);

    var viewRegistry = new ViewMappingRegister();
    viewRegistry.RegisterModules(
        new UserModuleRegistrar(),        // implements IViewModuleRegistrar only
        new OrderModuleRegistrar());

    PlatformServices.Default = new DefaultPlatformServices(viewRegistry);

    var mainPresenter = new MainShellPresenter();
    PlatformServices.Default.WindowNavigator.ShowWindow(mainPresenter);
    Application.Run();
}
```

##### Frequently-confused points

- **"Should I register Presenters in `IViewMappingRegister`?"** No, only Views. Presenters go in the DI container (or are `new`-ed manually).
- **"If I use a DI container, can I drop `IViewMappingRegister`?"** No, both are needed. View resolution and service resolution are separate concerns.
- **"Can I resolve Views from `IServiceProvider` directly?"** Not recommended. Use `WithServiceProvider(...)` to let the View registry consult the container; explicit registrations in the registry always win.
- **"Do legacy projects need to take a dependency on `WinformsMVP.DependencyInjection`?"** No — that package exists precisely so legacy projects don't have to.

### Error Handling Best Practices

The framework provides **multiple layers of error handling** to ensure robust applications and good user experience.

#### Layer 1: IMessageService - User-Facing Error Display

Always use `IMessageService` instead of `MessageBox.Show()` for displaying errors to users.

**Basic error display:**

```csharp
private void OnSave()
{
    try
    {
        ValidateInput();
        SaveData();
        Messages.ShowInfo("Data saved successfully!", "Success");
    }
    catch (ValidationException ex)
    {
        Messages.ShowWarning(ex.Message, "Validation Failed");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to save data: {ex.Message}", "Error");
    }
}
```

**Confirmation before destructive actions:**

```csharp
private void OnDelete()
{
    if (!Messages.ConfirmYesNo("Are you sure you want to delete this item?", "Confirm Delete"))
    {
        return;  // User cancelled
    }

    try
    {
        DeleteItem();
        Messages.ShowInfo("Item deleted.", "Success");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to delete: {ex.Message}", "Error");
    }
}
```

**Available methods:**
- `ShowInfo(text, caption)` - Informational messages
- `ShowWarning(text, caption)` - Warnings that don't block workflow
- `ShowError(text, caption)` - Error messages
- `ConfirmYesNo(text, caption)` - Returns `true` if user clicks Yes
- `ShowToast(text, type, duration)` - Non-blocking notifications (optional)

#### Layer 2: InteractionResult<T> - Operation Result Wrapping

Use `InteractionResult<T>` for operations that can fail (dialogs, file I/O, network calls).

**File operations:**

```csharp
private void OnOpenFile()
{
    var result = Dialogs.ShowOpenFileDialog(new OpenFileDialogOptions
    {
        Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
        Title = "Select a file to open"
    });

    if (result.IsSuccess)
    {
        LoadFile(result.Value);  // result.Value is the file path
    }
    else if (result.IsCancelled)
    {
        // User clicked Cancel - no action needed
        return;
    }
    else if (result.IsError)
    {
        Messages.ShowError(result.ErrorMessage, "Error");
    }
}
```

**Custom operations:**

```csharp
public InteractionResult<Customer> LoadCustomer(int customerId)
{
    try
    {
        var customer = _repository.GetById(customerId);
        if (customer == null)
        {
            return InteractionResult<Customer>.Error("Customer not found");
        }
        return InteractionResult<Customer>.Ok(customer);
    }
    catch (Exception ex)
    {
        return InteractionResult<Customer>.Error("Failed to load customer", ex);
    }
}

// Usage
private void OnEditCustomer(int id)
{
    var result = LoadCustomer(id);
    if (result.IsSuccess)
    {
        View.Customer = result.Value;
    }
    else
    {
        Messages.ShowError(result.ErrorMessage, "Error");
    }
}
```

**Benefits:**
- ✅ Avoids exception-based control flow
- ✅ Explicit success/cancel/error states
- ✅ Preserves exception information when needed
- ✅ Composable and chainable

#### Layer 3: Centralized Error Messages (DialogDefaults)

Use `DialogDefaults` to configure error messages globally for consistency.

**Setup in Program.cs or app startup:**

```csharp
static void Main()
{
    // Configure default error messages
    DialogDefaults.FileOpenErrorMessage = "ファイルを開くに失敗しました";
    DialogDefaults.FileSaveErrorMessage = "ファイルの保存に失敗しました";
    DialogDefaults.FolderBrowseErrorMessage = "フォルダの選択に失敗しました";
    DialogDefaults.DefaultErrorCaption = "エラー";

    Application.Run(new MainForm());
}
```

**Benefits:**
- ✅ Consistent error messages across the app
- ✅ Easy localization
- ✅ Single place to update messages

#### Layer 4: Global Exception Handler (Optional)

For catching unhandled exceptions at the application level.

**Setup in Program.cs:**

```csharp
static class Program
{
    [STAThread]
    static void Main()
    {
        // Configure global exception handlers
        Application.ThreadException += OnThreadException;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        HandleUnhandledException(e.Exception);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            HandleUnhandledException(ex);
        }
    }

    private static void HandleUnhandledException(Exception ex)
    {
        // Log the exception (use your logging framework)
        LogException(ex);

        // Show user-friendly message
        var messageService = PlatformServices.Default.MessageService;
        messageService.ShowError(
            $"An unexpected error occurred:\n\n{ex.Message}\n\nThe application may be in an unstable state.",
            "Unhandled Error");

        // Optionally exit the application
        // Application.Exit();
    }

    private static void LogException(Exception ex)
    {
        // TODO: Integrate with logging framework (NLog, Serilog, etc.)
        System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {ex}");
    }
}
```

**When to use:**
- ✅ Production applications that need crash reporting
- ✅ When integrating with logging frameworks
- ✅ To prevent application crashes from unknown bugs

**Caution:**
- Catching all exceptions can hide bugs during development
- Consider using only in Release builds or with conditional compilation

#### Error Handling Patterns by Scenario

**Scenario 1: Validation Errors**

```csharp
private void OnSave()
{
    // Validate early, fail fast
    var validationErrors = ValidateInput();
    if (validationErrors.Any())
    {
        var errorMessage = string.Join("\n", validationErrors);
        Messages.ShowWarning(errorMessage, "Validation Failed");
        return;  // Don't proceed with save
    }

    try
    {
        SaveData();
        Messages.ShowInfo("Data saved!", "Success");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to save: {ex.Message}", "Error");
    }
}
```

**Scenario 2: Network/Database Operations**

```csharp
private async void OnRefreshData()
{
    View.ShowLoadingIndicator(true);

    try
    {
        var data = await _repository.GetDataAsync();
        View.Data = data;
        Messages.ShowInfo($"Loaded {data.Count} items", "Success");
    }
    catch (TimeoutException)
    {
        Messages.ShowError("The operation timed out. Please check your network connection.", "Timeout");
    }
    catch (UnauthorizedAccessException)
    {
        Messages.ShowError("You don't have permission to access this data.", "Access Denied");
    }
    catch (Exception ex)
    {
        Messages.ShowError($"Failed to load data: {ex.Message}", "Error");
    }
    finally
    {
        View.ShowLoadingIndicator(false);
    }
}
```

**Scenario 3: File Operations with InteractionResult**

```csharp
private void OnExport()
{
    var fileResult = Dialogs.ShowSaveFileDialog(new SaveFileDialogOptions
    {
        Filter = "CSV files (*.csv)|*.csv",
        DefaultExt = "csv",
        Title = "Export Data"
    });

    if (fileResult.IsCancelled)
    {
        return;  // User cancelled
    }

    if (fileResult.IsError)
    {
        Messages.ShowError(fileResult.ErrorMessage, "Error");
        return;
    }

    try
    {
        ExportToCsv(fileResult.Value);
        Messages.ShowInfo($"Data exported to {Path.GetFileName(fileResult.Value)}", "Success");
    }
    catch (IOException ex)
    {
        Messages.ShowError($"Failed to write file: {ex.Message}", "Export Failed");
    }
}
```

**Scenario 4: Long-Running Operations with Progress**

```csharp
private async void OnProcessBatch()
{
    var itemsToProcess = View.SelectedItems;
    if (!itemsToProcess.Any())
    {
        Messages.ShowWarning("Please select items to process.", "No Selection");
        return;
    }

    View.ShowProgress(true);
    int successCount = 0;
    int errorCount = 0;

    foreach (var item in itemsToProcess)
    {
        try
        {
            await ProcessItemAsync(item);
            successCount++;
            View.UpdateProgress(successCount, itemsToProcess.Count);
        }
        catch (Exception ex)
        {
            errorCount++;
            // Log error but continue processing
            LogError($"Failed to process item {item.Id}: {ex.Message}");
        }
    }

    View.ShowProgress(false);

    // Show summary
    var summary = $"Processing complete:\n\nSuccess: {successCount}\nFailed: {errorCount}";
    if (errorCount > 0)
    {
        Messages.ShowWarning(summary, "Processing Complete");
    }
    else
    {
        Messages.ShowInfo(summary, "Success");
    }
}
```

#### Testing Error Handling

**Testing that errors are displayed correctly:**

```csharp
[Fact]
public void OnSave_WhenExceptionThrown_ShowsError()
{
    // Arrange
    var mockMessageService = new MockMessageService();
    var mockRepository = new MockUserRepository();
    mockRepository.SaveShouldThrow = true;  // Simulate exception

    var presenter = new UserEditorPresenter(mockMessageService, mockRepository);
    presenter.AttachView(new MockUserEditorView());
    presenter.Initialize();

    // Act
    presenter.OnSave();

    // Assert
    Assert.True(mockMessageService.ErrorMessageShown);
    Assert.Contains("Failed to save", mockMessageService.LastErrorMessage);
}
```

**Testing InteractionResult handling:**

```csharp
[Fact]
public void OnOpenFile_WhenUserCancels_DoesNotLoadFile()
{
    // Arrange
    var mockDialogs = new MockDialogProvider();
    mockDialogs.OpenFileDialogResult = InteractionResult<string>.Cancel();

    var presenter = new MyPresenter(mockDialogs);
    presenter.AttachView(new MockMyView());
    presenter.Initialize();

    // Act
    presenter.OnOpenFile();

    // Assert
    Assert.False(presenter.FileWasLoaded);
}
```

#### Key Principles

1. **Never use MessageBox.Show() in presenters** - Always use `IMessageService`
2. **Use InteractionResult<T> for failable operations** - Avoid exception-based control flow
3. **Validate early** - Check inputs before expensive operations
4. **Handle specific exceptions first** - Catch specific exceptions before generic `Exception`
5. **Always inform the user** - Don't silently swallow exceptions
6. **Log errors for debugging** - Especially in production
7. **Test error paths** - Write unit tests for error scenarios
8. **Keep error messages user-friendly** - No stack traces to end users

### Change Tracking

**ChangeTracker<T>** (`WinformsMVP.Common.ChangeTracker`) は編集/キャンセルシナリオのための堅牢な変更追跡を提供します。`IChangeTracking`および`IRevertibleChangeTracking`インターフェースを実装しています。

**型制約:** `where T : class`(参照型のみ)。`ICloneable` の実装は **任意**(必須ではありません)。

**スナップショット(複製)の解決順序:**
1. T が `ICloneable` を実装 → その `Clone()` を使用(高速パス、推奨)
2. 未実装 → グローバルフック `ChangeTrackerDefaults.Cloner`(既定 = 組み込みリフレクション深いコピー `ObjectCloner`)

**比較の解決順序:**
1. コンストラクタに渡した `comparer` → それを使用
2. T が値等価性(`IEquatable<T>`/`IComparable<T>`/`Equals` オーバーライド)を持つ → それを使用
3. いずれも無い → グローバルフック `ChangeTrackerDefaults.Comparer`(既定 = リフレクション深い比較)

**第三者ライブラリの差し込み(起動時に一度だけ):**
```csharp
ChangeTrackerDefaults.Cloner = o => o.DeepClone();   // 例: Force.DeepCloner
```

> **動作変更の注意:** `Equals` を override していないモデルは、旧実装では構築直後に `IsChanged == true`(参照比較によるバグ)でしたが、新実装ではリフレクション深い比較により正しく判定されます。`ICloneable`+`Equals` を実装済みのモデルは挙動不変です。

**ChangeTrackerの使用：**

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnInitialize()
    {
        var user = LoadUser(userId);
        _changeTracker = new ChangeTracker<UserModel>(user);

        View.Model = _changeTracker.CurrentValue;
    }

    protected override void RegisterViewActions()
    {
        _dispatcher.Register(
            CommonActions.Save,
            OnSave,
            canExecute: () => _changeTracker.IsChanged);

        _dispatcher.Register(CommonActions.Reset, OnReset);
    }

    private void OnSave()
    {
        SaveUser(_changeTracker.CurrentValue);
        _changeTracker.AcceptChanges();  // 新しいベースライン
    }

    private void OnReset()
    {
        _changeTracker.RejectChanges();  // ベースラインに戻す
        View.Model = _changeTracker.CurrentValue;
    }
}
```

**深いコピーが必要な理由：**

浅いコピーを使用すると、元のオブジェクトとコピーで参照型プロパティが共有されます。
これにより、`RejectChanges()`を呼び出しても元の値に戻らない問題が発生します：

```csharp
// 浅いコピーの問題例
var user = new UserModel { Address = new Address { City = "Tokyo" } };
var tracker = new ChangeTracker<UserModel>(user);

// AddressがCloneされていないため、同じインスタンスを共有
tracker.CurrentValue.Address.City = "Osaka";

// RejectChanges()を呼び出しても...
tracker.RejectChanges();

// 元の値も変更されてしまう！
Console.WriteLine(tracker.CurrentValue.Address.City);  // "Osaka" (期待値: "Tokyo")
```

**ベストプラクティス：**

1. **すべての参照型プロパティを深くコピー**
   - ネストされたオブジェクト: `Address = this.Address?.Clone() as Address`
   - コレクション: `Tags = this.Tags != null ? new List<string>(this.Tags) : null`

2. **MemberwiseCloneを使用しない**
   - 参照型プロパティが共有される
   - 変更追跡が正しく動作しない

3. **実装をテストする**
   ```csharp
   [Fact]
   public void Clone_CreatesIndependentCopy()
   {
       var original = new UserModel { Name = "John" };
       var clone = original.Clone() as UserModel;

       clone.Name = "Jane";

       Assert.Equal("John", original.Name);  // 元は変更されない
       Assert.Equal("Jane", clone.Name);
   }
   ```

**主要API：**

- `CurrentValue` - 現在追跡している値（読み取り専用）
- `UpdateCurrentValue(T)` - 現在の値を更新（IsChangedChangedイベントを発火）
- `IsChanged` - 現在の値が元の値と異なるかどうか（キャッシュ付き）
- `AcceptChanges()` - 現在の値を新しいベースラインとして確定
- `RejectChanges()` - ベースラインに戻す
- `IsChangedWith(T)` - 指定された値が元の値と異なるかチェック
- `GetOriginalValue()` - 元の値のコピーを取得
- `CanAcceptChanges(out string)` - 変更を受け入れ可能かチェック（検証用）
- `CanRejectChanges(out string)` - 変更を破棄可能かチェック（検証用）

**イベント：**

- `IsChangedChanged` - IsChanged 状態が変わったときに発火

**高度な機能：**

1. **変更通知イベント**
   ```csharp
   var tracker = new ChangeTracker<UserModel>(user);

   // IsChanged 状態の変化を監視
   tracker.IsChangedChanged += (s, e) =>
   {
       // UI更新（例：保存ボタンの有効化/無効化）
       _dispatcher.RaiseCanExecuteChanged();
   };
   ```

2. **スレッドセーフ**
   - すべての操作は内部でロック保護されており、マルチスレッド環境で安全に使用可能

3. **パフォーマンス最適化**
   - IsChanged プロパティは結果をキャッシュし、値が変更されるまで再計算しない

4. **検証サポート**
   ```csharp
   // カスタム検証ロジック（派生クラスで実装）
   public class ValidatedChangeTracker<T> : ChangeTracker<T> where T : class
   {
       public override bool CanAcceptChanges(out string error)
       {
           if (CurrentValue is IValidatable validatable && !validatable.IsValid)
           {
               error = "Validation failed";
               return false;
           }
           error = null;
           return true;
       }
   }
   ```

### Event Aggregator

**EventAggregator** (`WinformsMVP.Common.EventAggregator.EventAggregator`) provides high-performance, thread-safe pub-sub messaging for decoupled component communication. It's an essential pattern for complex MVP applications where presenters need to communicate without direct references.

**Core Features:**

- ✅ **Weak Reference Subscriptions** - Automatically cleans up when subscribers are GC'd, preventing memory leaks
- ✅ **UI Thread Marshaling** - Background thread messages automatically marshaled to UI thread (critical for WinForms)
- ✅ **High Performance** - Uses expression tree compiled delegates (10-100x faster than reflection)
- ✅ **Exception Isolation** - One subscriber's exception doesn't affect others
- ✅ **Thread Safety** - Supports concurrent publishing and subscribing
- ✅ **Filtered Subscriptions** - Subscribe only to messages matching specific criteria

**When to Use Event Aggregator:**

Use Event Aggregator for **cross-presenter communication** when presenters need to collaborate but should remain decoupled:

```
✅ Use Event Aggregator when:
- Multiple presenters need to react to the same event
- Presenters are in different parts of the UI hierarchy
- You want to avoid parent-presenter managing child-presenter references
- Components need to communicate across modules

❌ Don't use Event Aggregator when:
- Simple parent-child presenter communication (use public methods instead)
- View-to-Presenter communication (use View events or ViewActions)
- Within a single presenter (use private methods)
```

**Basic Usage:**

```csharp
// 1. Define message types
public class ProductAddedMessage
{
    public Product Product { get; set; }
    public int Quantity { get; set; }
}

public class OrderClearedMessage { }

// 2. Create shared EventAggregator instance (in Program.Main or composition root)
var eventAggregator = new EventAggregator();  // Must create on UI thread!

// 3. Presenter A: Subscribe to messages
public class OrderSummaryPresenter : ControlPresenterBase<IOrderSummaryView>
{
    private readonly IEventAggregator _eventAggregator;
    private IDisposable _productAddedSubscription;

    public OrderSummaryPresenter(IOrderSummaryView view, IEventAggregator eventAggregator)
        : base(view)
    {
        _eventAggregator = eventAggregator;
    }

    protected override void OnViewAttached()
    {
        // Subscribe to messages
        _productAddedSubscription = _eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
    }

    private void OnProductAdded(ProductAddedMessage msg)
    {
        // This runs on UI thread automatically!
        View.AddItem(msg.Product, msg.Quantity);
    }

    protected override void Cleanup()
    {
        // Clean up subscriptions
        _productAddedSubscription?.Dispose();
    }
}

// 4. Presenter B: Publish messages
public class ProductSelectorPresenter : ControlPresenterBase<IProductSelectorView>
{
    private readonly IEventAggregator _eventAggregator;

    public ProductSelectorPresenter(IOrderSummaryView view, IEventAggregator eventAggregator)
        : base(view)
    {
        _eventAggregator = eventAggregator;
    }

    private void OnAddToOrder()
    {
        var product = View.SelectedProduct;
        var quantity = View.Quantity;

        // Publish message - all subscribers will be notified
        _eventAggregator.Publish(new ProductAddedMessage
        {
            Product = product,
            Quantity = quantity
        });
    }
}
```

**Filtered Subscriptions:**

Subscribe only to messages matching specific criteria:

```csharp
// Only receive messages for high-priority products
_eventAggregator.Subscribe<ProductAddedMessage>(
    msg => View.HighlightProduct(msg.Product),
    filter: msg => msg.Product.Priority > 5
);

// Only receive messages for specific customer
_eventAggregator.Subscribe<OrderPlacedMessage>(
    msg => UpdateCustomerOrders(msg.OrderId),
    filter: msg => msg.CustomerId == _currentCustomerId
);
```

**Background Thread Publishing (Automatic UI Marshaling):**

Messages published from background threads are automatically marshaled to the UI thread:

```csharp
// Background thread
Task.Run(() =>
{
    var data = LoadDataFromDatabase();

    // Publish from background thread
    _eventAggregator.Publish(new DataLoadedMessage { Data = data });
    // Subscribers' handlers will execute on UI thread automatically!
});

// Subscriber (runs on UI thread)
_eventAggregator.Subscribe<DataLoadedMessage>(msg =>
{
    View.Data = msg.Data;  // Safe! Automatically on UI thread
});
```

**Request-Response Pattern:**

Use messages for queries:

```csharp
// Request message with response property
public class GetOrderSnapshotRequest
{
    public IList<OrderItem> Snapshot { get; set; }  // Response goes here
}

// Publisher (requestor)
private List<OrderItem> GetCurrentOrderSnapshot()
{
    var request = new GetOrderSnapshotRequest();
    _eventAggregator.Publish(request);
    return request.Snapshot?.ToList() ?? new List<OrderItem>();
}

// Subscriber (responder)
_eventAggregator.Subscribe<GetOrderSnapshotRequest>(request =>
{
    request.Snapshot = _orderItems.ToList();  // Fill response
});
```

**Architecture Comparison:**

| Pattern | Coupling | Use Case | Example |
|---------|----------|----------|---------|
| **Public Methods** | Direct reference | Parent-child coordination | `childPresenter.AddProduct()` |
| **Shared Service** | Service reference | Shared state | `orderService.AddProduct()` |
| **Event Aggregator** | No coupling | Cross-presenter events | `eventAggregator.Publish(...)` |

**Event Aggregator vs Shared Service:**

```csharp
// ❌ Event Aggregator for State Management (Wrong!)
_eventAggregator.Publish(new AddProductMessage(product));  // Who owns the state?
var snapshot = GetOrderSnapshot();  // Request-response - awkward!

// ✅ Shared Service for State Management (Correct!)
_orderService.AddProduct(product);  // Service owns state
var items = _orderService.OrderItems;  // Direct query

// ✅ Event Aggregator for Notifications (Correct!)
_orderService.ProductAdded += (s, e) => { };  // Direct event subscription, OR
_eventAggregator.Publish(new ProductAddedNotification(...));  // Decoupled notification
```

**Best Practices:**

1. **Create EventAggregator on UI thread** - Must capture SynchronizationContext
   ```csharp
   // In Program.Main() or main form constructor
   var eventAggregator = new EventAggregator();  // ✅ On UI thread
   ```

2. **Always dispose subscriptions** - Use `IDisposable` pattern
   ```csharp
   protected override void Cleanup()
   {
       _subscription?.Dispose();  // ✅ Explicit cleanup
   }
   ```

3. **Use strongly-typed messages** - Create specific message classes
   ```csharp
   // ✅ Good - Specific message type
   public class ProductAddedMessage { ... }

   // ❌ Bad - Generic dictionary
   _eventAggregator.Publish(new Dictionary<string, object> { ... });
   ```

4. **Message naming convention** - Use `XxxMessage` or `XxxNotification`
   ```csharp
   ProductAddedMessage      // ✅ Action notification
   OrderClearedMessage      // ✅ Event notification
   DataLoadedMessage        // ✅ Data notification
   GetOrderSnapshotRequest  // ✅ Request/query
   ```

5. **Keep messages immutable** - Use read-only or init properties
   ```csharp
   public class ProductAddedMessage
   {
       public Product Product { get; init; }  // ✅ Immutable
       public int Quantity { get; init; }
   }
   ```

6. **Don't overuse** - Event Aggregator adds indirection; use sparingly
   - For parent-child: Call public methods directly
   - For shared state: Use shared service with events
   - For cross-cutting concerns: Use Event Aggregator

**Performance Characteristics:**

Based on comprehensive stress tests (`EventAggregatorTests.cs`):

- **10,000 subscribers**: ~1 second to publish one message
- **100,000 messages**: ~16ms with 1 subscriber
- **Throughput**: > 50,000 messages/second
- **Thread-safe**: Concurrent subscribe/publish operations supported
- **Memory**: Weak references prevent leaks, automatic cleanup on GC

**Testing:**

```csharp
[Fact]
public void ProductSelector_PublishesMessage_OrderSummaryReceives()
{
    // Arrange
    var eventAggregator = new EventAggregator();
    var productSelectorView = new MockProductSelectorView();
    var orderSummaryView = new MockOrderSummaryView();

    var productSelectorPresenter = new ProductSelectorPresenter(
        productSelectorView, eventAggregator);

    var orderSummaryPresenter = new OrderSummaryPresenter(
        orderSummaryView, eventAggregator);

    productSelectorPresenter.AttachView(productSelectorView);
    productSelectorPresenter.Initialize();

    orderSummaryPresenter.AttachView(orderSummaryView);
    orderSummaryPresenter.Initialize();

    // Act
    productSelectorPresenter.AddToOrder();  // Publishes ProductAddedMessage

    // Assert
    Assert.Equal(1, orderSummaryView.Items.Count);
    Assert.Equal("Laptop", orderSummaryView.Items[0].Product.Name);
}
```

**See Also:**

- `src/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/` - Full Event Aggregator example
- `src/WinformsMVP.Samples/ComplexInteractionDemo_ServiceBased/` - Service-based comparison
- `src/WinformsMVP.Samples.Tests/Common/EventAggregatorTests.cs` - Comprehensive tests including stress tests

## Project Structure

- `src/WinformsMVP/`: Core framework library (SDK-style project)
  - `MVP/Presenters/`: Presenter base classes and interfaces
    - `PresenterBase.cs` - Common base (do not inherit directly)
    - `WindowPresenterBase.cs` - For Forms without parameters
    - `WindowPresenterBaseWithParams.cs` - For Forms with parameters
    - `ControlPresenterBase.cs` - For UserControls without parameters
    - `ControlPresenterBaseWithParams.cs` - For UserControls with parameters
  - `MVP/Views/`: View interfaces
  - `MVP/Models/`: Model base classes
  - `MVP/ViewActions/`: Action dispatch system
  - `Services/`: Application service interfaces and implementations
  - `Common/`: Shared utilities, events, helpers

- `src/WinformsMVP.Samples/`: Sample WinForms application (SDK-style project)

- `src/WinformsMVP.Samples.Tests/`: xUnit test project (SDK-style project)

## Development Notes

### MVP Principles - CRITICAL

**📋 See the [MVP Design Rules](https://github.com/yourusername/winforms-mvp/wiki/MVP-Design-Rules) in the wiki for the complete 17-rule design guide.**

When writing MVP code, you MUST follow these rules:

**❌ NEVER do this in Presenters or View Interfaces:**
- `MessageBox.Show()` in Presenter - Use `IMessageService` instead
- `new OpenFileDialog()` in Presenter - Use `IDialogProvider` instead
- `Button`, `TextBox`, `Control` in View Interface - Expose only data and behavior
- Direct WinForms API calls in Presenter - Use service abstractions instead
- Any UI element types in Presenter or View Interface

**❌ NEVER expose UI elements in View Interfaces:**
```csharp
// WRONG - View interface exposes UI types
public interface IMyView : IWindowView
{
    Button SaveButton { get; }      // ❌ Never expose Button
    TextBox NameTextBox { get; }    // ❌ Never expose TextBox
    ListBox ItemsListBox { get; }   // ❌ Never expose ListBox
}
```

**✅ ALWAYS do this in View Interfaces:**
```csharp
// CORRECT - View interface exposes only data and behavior
public interface IMyView : IWindowView
{
    // Data properties (no UI types)
    string Name { get; set; }
    bool HasSelection { get; }

    // Behavior methods
    void AddItem(string item);
    void ClearItems();
    void UpdateStatus(string message);

    // Events for state changes
    event EventHandler SelectionChanged;

    // ViewAction integration (Form exposes configured action binder)
    ViewActionBinder ActionBinder { get; }
}
```

**✅ Presenter interacts only through abstracted interfaces:**
- Inject services (IMessageService, IDialogProvider, etc.) via constructor
- Access View only through the interface (data properties and methods)
- Framework automatically binds View.ActionBinder to Dispatcher after RegisterViewActions()
- Use ViewAction system for command binding (see "ViewAction System" section)
- Presenter has ZERO knowledge of Button, TextBox, or any UI elements

**✅ View implementation (Form) owns UI elements:**
```csharp
public class MyForm : Form, IMyView
{
    // Private UI controls - NOT exposed through interface
    private Button _saveButton;
    private TextBox _nameTextBox;
    private ViewActionBinder _binder;  // Internal to Form

    public ViewActionBinder ActionBinder => _binder;

    public MyForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    // Configure action bindings in constructor - Form knows about buttons, Presenter doesn't
    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();
        _binder.Add(MyActions.Save, _saveButton);  // Map button to action
        // No Bind() call here - Framework automatically binds after RegisterViewActions()
    }
}
```

**✅ Presenter public API surface — keep it minimal (Rules 16 & 17):**

The Presenter is **not a service**. Every additional `public` member widens the contract and tempts callers to bypass the framework's event-driven lifecycle.

**Methods (Rule 16):**

| Method kind | Visibility |
|-------------|-----------|
| Constructor | `public` (DI requires it) |
| Interface contract (e.g. `IRequestClose<T>.CloseRequested`) | `public` (contract requires it) |
| Lifecycle hooks (`OnInitialize`, `OnViewAttached`, `RegisterViewActions`, `Cleanup`) | `protected override` |
| ViewAction handlers (`OnSave`, `OnCancel`, …) | `private` |
| View event handlers (`OnViewClosing`, …) | `private` |
| Helpers (`RaiseClose`, validation, formatters) | `private` |

**Events (Rule 17):**

A Presenter should expose **zero public events** unless it implements `IRequestClose<TResult>`. For other notification needs, use the right channel:

- Window close result → implement `IRequestClose<T>.CloseRequested`
- Parent owns the child → parent calls a method on the child (or subscribes to a single event as a last resort)
- Shared state (orders, auth, etc.) → put state in a Service and raise the event from the Service
- Cross-module notification → publish via `IEventAggregator` (weak refs, UI-thread marshaled)

```csharp
// ❌ Avoid: turning the Presenter into a service / state observer
public class BadPresenter : WindowPresenterBase<IMyView>
{
    public void Save() { ... }                           // ❌ bypasses Dispatcher / CanExecute
    public bool CanSave => _changeTracker.IsChanged;     // ❌ Tell-Don't-Ask violation
    public event EventHandler IsDirtyChanged;            // ❌ leaks internal state
    public event EventHandler SelectionChanged;          // ❌ re-publishing a View event
}

// ✅ Correct: constructor + interface contract event only
public class GoodPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;  // ✅ contract

    public GoodPresenter(IMyRepository repo) { ... }                              // ✅ DI

    protected override void RegisterViewActions() { ... }                         // ✅ framework
    private void OnSave() { ... }                                                 // ✅ Dispatcher only
    private void RaiseClose(MyResult r, InteractionStatus s) => ...;              // ✅ internal helper
}
```

**Testing rule:** Don't widen visibility for tests. Drive the Presenter through its real entry points:

```csharp
// ❌ Wrong:   presenter.OnSave();
// ✅ Right:   presenter.Dispatcher.Dispatch(CommonActions.Save);
// ✅ Right:   view.RaiseClosing(CloseReason.Normal);
// ✅ Right:   presenter.CloseRequested += (s, e) => captured = e;
```

This exercises the same CanExecute predicates and close flow that production code uses — bypassing them via direct calls produces tests that pass while the app is broken.

All sample code in this repository demonstrates proper MVP principles with complete separation of concerns.

### Target Framework
All projects target .NET Framework 4.8 using SDK-style project format. This provides:
- Automatic file inclusion (no need to manually list .cs files)
- Simplified project files
- Modern tooling support
- PackageReference instead of packages.config

### Testing Framework
Tests use xUnit 2.9.3 with Visual Studio test runner.

### Common Extension Points

When implementing a new feature:

**For Forms/Dialogs:**
1. Define view interface extending `IWindowView`
2. Create presenter extending `WindowPresenterBase<TView>` or `WindowPresenterBase<TView, TParam>`
3. Implement WinForms form with the view interface
4. Register view-to-implementation mapping in `IViewMappingRegister`
5. Use `WindowNavigator` to show the window

**For UserControls:**
1. Define view interface extending `IViewBase`
2. Create presenter extending `ControlPresenterBase<TView>` or `ControlPresenterBase<TView, TParam>`
3. Implement UserControl with the view interface
4. Create presenter in parent form/control, passing the view (and optional parameters)
5. Presenter handles its own lifecycle

### Key Architectural Decisions

1. **Form vs UserControl Separation**: Forms use WindowNavigator for dynamic creation; UserControls use constructor injection
2. **Parameter Support**: Both Forms and UserControls support optional parameterized initialization
3. **Lifecycle Management**: UserControl presenters automatically dispose with their controls
4. **View Injection**: Forms use late binding via WindowNavigator; UserControls use constructor injection
