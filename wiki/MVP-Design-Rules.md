# MVP Design Rules

**📋 17 Essential Rules for Clean MVP Architecture**

This guide covers the complete set of Model-View-Presenter design rules based on the Supervising Controller pattern. These rules ensure maintainable, testable, and well-structured WinForms applications.

## 📚 Reference

These design rules are based on the classic MVP pattern as described in:
- **[Design Rules for Model-View-Presenter](https://kjellsj.blogspot.com/2008/05/design-rules-for-model-view-presenter.html)** by Kjell-Sverre Jerijærvi

The rules have been adapted and extended for the WinForms MVP Framework with modern .NET practices.

---

## Table of Contents

1. [Rule 1: View Naming Convention](#rule-1-view-naming-convention)
2. [Rule 2: Presenter Naming Convention](#rule-2-presenter-naming-convention)
3. [Rule 3: Responsibility Separation](#rule-3-responsibility-separation)
4. [Rule 4: No UI Element Types in View Interfaces and Presenters](#rule-4-no-ui-element-types-in-view-interfaces-and-presenters)
5. [Rule 5: OnXxx() Naming for Event Handlers](#rule-5-onxxx-naming-for-event-handlers)
6. [Rule 6: Minimize View-to-Presenter Calls](#rule-6-minimize-view-to-presenter-calls)
7. [Rule 7: No Return Values from Presenter Methods](#rule-7-no-return-values-from-presenter-methods)
8. [Rule 8: Access View Only Through Interface](#rule-8-access-view-only-through-interface)
9. [Rule 9: View Method Visibility](#rule-9-view-method-visibility)
10. [Rule 10: Only Presenter Accesses View](#rule-10-only-presenter-accesses-view)
11. [Rule 11: Long Meaningful Names](#rule-11-long-meaningful-names)
12. [Rule 12: Prefer Methods Over Properties](#rule-12-prefer-methods-over-properties)
13. [Rule 13: All Data in Model](#rule-13-all-data-in-model)
14. [Rule 14: No UI Control Names in Interface](#rule-14-no-ui-control-names-in-interface)
15. [Rule 15: Domain-Driven Naming](#rule-15-domain-driven-naming)
16. [Rule 16: Presenter Method Visibility](#rule-16-presenter-method-visibility)
17. [Rule 17: Presenter Event Visibility](#rule-17-presenter-event-visibility)

---

## Rule 1: View Naming Convention

**All View interfaces must end with 'View' suffix.**

### ✅ Correct Examples

```csharp
public interface ITaskView : IWindowView { }
public interface IUserEditorView : IWindowView { }
public interface ICustomerListView : IWindowView { }
public interface ISettingsPanelView : IViewBase { }
```

### ❌ Incorrect Examples

```csharp
public interface ITaskDialog : IWindowView { }          // ❌ Should be ITaskDialogView
public interface IUserEditor : IWindowView { }          // ❌ Should be IUserEditorView
public interface ICustomerManagement : IWindowView { }  // ❌ Should be ICustomerManagementView
```

### Why This Matters

- **Clarity**: Immediately identifies View interfaces in the codebase
- **Consistency**: Follows .NET naming conventions (like `XxxController`, `XxxService`)
- **Tooling**: Enables automated scanning and code generation
- **Communication**: Makes architecture discussions clearer ("the TaskView", not "the Task")

### Analyzer Support

**Diagnostic**: `MVP001`
**Severity**: Warning

The Roslyn analyzer automatically detects View interfaces that don't follow this convention.

---

## Rule 2: Presenter Naming Convention

**All Presenter classes must end with 'Presenter' suffix.**

### ✅ Correct Examples

```csharp
public class TaskPresenter : WindowPresenterBase<ITaskView> { }
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView> { }
public class CustomerListPresenter : ControlPresenterBase<ICustomerListView> { }
```

### ❌ Incorrect Examples

```csharp
public class TaskController : WindowPresenterBase<ITaskView> { }      // ❌ Should be TaskPresenter
public class UserEditorLogic : WindowPresenterBase<IUserEditorView> { }  // ❌ Should be UserEditorPresenter
public class CustomerManager : ControlPresenterBase<ICustomerListView> { }  // ❌ Should be CustomerListPresenter
```

### Why This Matters

- **Pattern Recognition**: Clearly identifies business logic layer
- **Separation**: Distinguishes Presenters from Controllers, Services, and Managers
- **Testability**: Makes it easy to find and test all use-case logic

### Analyzer Support

**Diagnostic**: `MVP002`
**Severity**: Warning

---

## Rule 3: Responsibility Separation

**Presenter handles use-case logic only. View handles UI logic.**

This is the **most critical rule** in MVP architecture.

### Presenter Responsibilities (Use-Case Logic)

- Business rule validation
- Coordinating data flow
- Calling services/repositories
- Orchestrating user workflows
- Managing application state

### View Responsibilities (UI Logic)

- Creating and arranging controls
- Data binding
- Visual styling (colors, fonts, layout)
- Animations and transitions
- Responding to View events

### ✅ Correct Example

```csharp
// Presenter - handles business logic
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSubmitOrder()
    {
        // ✅ Business validation
        if (!ValidateOrder(View.Order))
        {
            View.ShowValidationErrors(_validationErrors);
            return;
        }

        // ✅ Business logic
        _orderService.Submit(View.Order);

        // ✅ Tell View what to do
        View.ShowSuccessMessage();
        View.ClearForm();
    }
}

// View - handles UI details
public class OrderForm : Form, IOrderView
{
    public void ShowValidationErrors(IEnumerable<string> errors)
    {
        // ✅ View decides how to display errors
        _errorPanel.BackColor = Color.LightPink;
        _errorLabel.ForeColor = Color.DarkRed;
        _errorLabel.Text = string.Join("\n", errors);
        _errorPanel.Visible = true;
    }
}
```

### ❌ Incorrect Example

```csharp
// ❌ Presenter creating UI controls - WRONG!
public class OrderPresenter : WindowPresenterBase<IOrderView>
{
    private void OnSubmitOrder()
    {
        // ❌ Creating UI controls in Presenter
        var errorLabel = new Label();
        errorLabel.Text = "Validation failed";
        errorLabel.ForeColor = Color.Red;

        // ❌ Manipulating UI properties
        View.ErrorPanel.BackColor = Color.LightPink;
        View.ErrorPanel.Controls.Add(errorLabel);
    }
}
```

### Common Violations

| ❌ Violation | ✅ Correct Approach |
|-------------|-------------------|
| `new Button()` in Presenter | Let View create controls |
| `View.TextBox.BackColor = Color.Red` | `View.HighlightError()` |
| `View.Grid.Rows[0].Cells[1].Value = x` | `View.UpdateCell(row, col, value)` |
| `MessageBox.Show()` in Presenter | Use `IMessageService` |

### Analyzer Support

**Diagnostic**: `MVP003`
**Severity**: Error

The analyzer detects when Presenters create UI control instances (`new Button()`, `new TextBox()`, etc.).

---

## Rule 4: No UI Element Types in View Interfaces and Presenters

**View interfaces and Presenters must NEVER expose WinForms UI types (Button, TextBox, Control, etc.) in properties, fields, parameters, or return types.**

This is a **fundamental rule** for maintaining proper separation of concerns and testability.

### Why This Rule Exists

Exposing UI element types breaks the abstraction layer:
- **View interfaces** should define behavior and data, not UI implementation
- **Presenters** should work with domain concepts, not UI controls
- Both must remain testable without requiring WinForms infrastructure

### ✅ Correct Examples

```csharp
// ✅ View interface - no UI types
public interface ITaskView : IWindowView
{
    // Properties expose data, not controls
    string TaskTitle { get; set; }
    DateTime DueDate { get; set; }
    bool IsHighPriority { get; set; }

    // Methods describe behavior, not UI details
    void DisplayTasks(IEnumerable<TaskModel> tasks);
    void HighlightTask(int taskId);
    void ShowValidationError(string message);

    // Events for state changes
    event EventHandler TaskSelected;
}

// ✅ Presenter - no UI types
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    // Fields contain business logic dependencies
    private readonly ITaskRepository _repository;
    private readonly IMessageService _messageService;

    // Methods work with domain models
    private void OnSave()
    {
        var task = new TaskModel
        {
            Title = View.TaskTitle,
            DueDate = View.DueDate,
            IsHighPriority = View.IsHighPriority
        };

        _repository.Save(task);
        _messageService.ShowInfo("Task saved!");
    }
}
```

### ❌ Incorrect Examples

```csharp
// ❌ View interface exposing UI types - WRONG!
public interface ITaskView : IWindowView
{
    Button SaveButton { get; }           // ❌ Exposes Button
    TextBox TitleTextBox { get; }        // ❌ Exposes TextBox
    DataGridView TaskGrid { get; }       // ❌ Exposes DataGridView

    // ❌ Methods with UI types in signature
    void UpdateButton(Button button);    // ❌ Button parameter
    Control GetControl(string name);     // ❌ Returns Control
}

// ❌ Presenter with UI types - WRONG!
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    // ❌ Fields with UI types
    private Button _saveButton;          // ❌ UI element in Presenter
    private TextBox _titleTextBox;       // ❌ UI element in Presenter

    // ❌ Methods with UI types
    private void ConfigureButton(Button button)  // ❌ Button parameter
    {
        button.Text = "Save";            // ❌ Direct UI manipulation
        button.BackColor = Color.Blue;   // ❌ Direct UI manipulation
    }
}
```

### Real-World Migration Example

**Before (Violates Rule 4):**
```csharp
// ❌ Bad design
public interface IUserEditorView : IWindowView
{
    TextBox NameTextBox { get; }        // ❌ Exposes TextBox
    Button SaveButton { get; }          // ❌ Exposes Button
}

public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void OnViewAttached()
    {
        // ❌ Presenter manipulates UI controls directly
        View.SaveButton.Click += (s, e) => OnSave();
        View.NameTextBox.TextChanged += (s, e) => ValidateName();
    }
}
```

**After (Follows Rule 4):**
```csharp
// ✅ Good design
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }       // ✅ Exposes data, not control

    event EventHandler SaveRequested;   // ✅ Events for user actions
    event EventHandler NameChanged;

    ViewActionBinder ActionBinder { get; }  // ✅ Framework abstraction (property)
}

public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void OnViewAttached()
    {
        // ✅ Presenter works through interface
        View.SaveRequested += (s, e) => OnSave();
        View.NameChanged += (s, e) => ValidateName();
        // Note: Framework automatically binds View.ActionBinder after RegisterViewActions()
    }

    private void ValidateName()
    {
        // ✅ Works with data, not controls
        if (string.IsNullOrEmpty(View.UserName))
        {
            View.ShowValidationError("Name is required");
        }
    }
}

// View implementation - UI details hidden
public class UserEditorForm : Form, IUserEditorView
{
    // ✅ Private UI controls - not exposed through interface
    private TextBox _nameTextBox;
    private Button _saveButton;
    private ViewActionBinder _binder;

    public ViewActionBinder ActionBinder => _binder;

    public UserEditorForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();
        _binder.Add(CommonActions.Save, _saveButton);
        // Framework will bind automatically
    }

    public string UserName
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }

    public event EventHandler SaveRequested
    {
        add => _saveButton.Click += value;
        remove => _saveButton.Click -= value;
    }
}
```

### Common UI Types to Avoid

Never expose these types in View interfaces or Presenters:

**Basic Controls:**
- `Button`, `TextBox`, `Label`, `CheckBox`, `RadioButton`
- `ComboBox`, `ListBox`, `NumericUpDown`, `DateTimePicker`

**Container Controls:**
- `Panel`, `GroupBox`, `TabControl`, `SplitContainer`
- `FlowLayoutPanel`, `TableLayoutPanel`

**Complex Controls:**
- `DataGridView`, `TreeView`, `ListView`
- `RichTextBox`, `WebBrowser`, `PropertyGrid`

**Base Types:**
- `Control`, `UserControl`, `Form`
- `ToolStrip`, `MenuStrip`, `StatusStrip`

### ❌ CRITICAL: No Exceptions - Not Even for Framework Operations

**There are NO exceptions to Rule 4. Form and Control are UI element types and must NEVER appear in Presenters.**

**❌ WRONG - Do NOT do this:**
```csharp
// ❌ WRONG - Breaks testability and reusability
public class MyPresenter : WindowPresenterBase<IMyView>
{
    private void OnClose()
    {
        // ❌ WRONG - Presenter depends on Form type
        if (View is Form form)
        {
            form.Close();  // Breaks mock testing, breaks UserControl migration
        }
    }
}
```

**Why this is wrong:**

1. **Breaks Testability**
   ```csharp
   // Unit test fails - mock is not a Form
   var mockView = new Mock<IMyView>();
   var presenter = new MyPresenter();
   presenter.AttachView(mockView.Object);
   presenter.OnClose();  // ❌ Crashes - mockView is not Form
   ```

2. **Breaks Reusability**
   - Want to use UserControl? ❌ Crashes (UserControl ≠ Form)
   - Want to port to WPF? ❌ Impossible (no Form in WPF)
   - Want to use in web? ❌ Impossible

3. **Violates MVP Principles**
   - Presenter knows about UI implementation
   - Can't swap View implementations
   - Tightly coupled to WinForms

**✅ CORRECT - Use the framework's event-driven close model:**

**Option 1: Implement `IRequestClose<TResult>` (Recommended)**
```csharp
// ✅ Correct - declare the event, raise it with a small private helper.
//    No public methods (other than the event) leak from the Presenter.
public class MyPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;

    private void OnSave()
        => RaiseClose(new MyResult { ... }, InteractionStatus.Ok);

    private void OnCancel()
        => RaiseClose(null, InteractionStatus.Cancel);

    private void RaiseClose(MyResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<MyResult>(result, status));
}
```

For the Pull direction (user clicks X, system shutdown), subscribe to `View.Closing`:
```csharp
protected override void OnViewAttached()
{
    View.Closing += (s, args) =>
    {
        if (args.Reason != CloseReason.Normal) return;   // bypass system shutdown
        if (_changeTracker.IsChanged &&
            !Messages.ConfirmYesNo("Discard changes?", "Confirm"))
        {
            args.Cancel = true;
        }
    };
}
```

The Presenter never exposes a public `CanClose()` method — that would turn it into a service provider. The Pull-direction handler is a private subscription to a framework event.

**Option 2: View Interface Method**
```csharp
// ✅ Correct - Abstract behavior in interface
public interface IMyView : IWindowView
{
    void CloseView();  // ✅ Behavior, not UI type
}

public class MyForm : Form, IMyView
{
    public void CloseView() => this.Close();  // Implementation detail
}

public class MyPresenter : WindowPresenterBase<IMyView>
{
    private void OnClose()
    {
        View.CloseView();  // ✅ Works with any IMyView implementation
    }
}

// ✅ Now testable
public class MockMyView : IMyView
{
    public bool WasClosed { get; private set; }
    public void CloseView() => WasClosed = true;
}
```

**Key Principle:** Can depend on `System.Windows.Forms` namespace, but **NEVER use UI element types** (`Form`, `Control`, `Button`, etc.) in Presenter code.

### What About IWin32Window?

**IWin32Window does NOT violate Rule 4.** Here's why:

```csharp
// ✅ IWin32Window in IWindowNavigator is acceptable
public interface IWindowNavigator
{
    InteractionResult ShowWindowAsModal<TPresenter>(
        TPresenter presenter,
        IWin32Window owner = null)  // ✅ OK - Window handle abstraction
        where TPresenter : IPresenter;
}
```

**Why IWin32Window is acceptable:**

1. **Not a UI Control Type**
   - `IWin32Window` is a window handle abstraction, not a UI implementation detail
   - It's equivalent to `readonly struct { IntPtr Handle; }`
   - Compare: `Button` exposes UI implementation (text, color, events), `IWin32Window` only exposes OS window handle

2. **OS-Level Concept**
   - Window ownership is an operating system concept, not WinForms-specific
   - Modal dialog positioning requires parent window handle (HWND on Windows)
   - This is platform infrastructure, not UI framework detail

3. **Already an Interface**
   - `IWin32Window` itself is an abstraction with a single member: `IntPtr Handle { get; }`
   - It provides type safety over raw `IntPtr`
   - Creating `IWindowOwner` would be redundant - same single property, same purpose

4. **Framework-Level Dependency**
   - This is **WinformsMVP** framework - WinForms dependency is by design
   - If cross-platform support is needed, much more than this interface would need changing
   - The framework's purpose is to bring MVP pattern to WinForms

**What Rule 4 Actually Prohibits:**

| ❌ Prohibited | ✅ Allowed |
|--------------|-----------|
| `Button`, `TextBox`, `DataGridView` - UI controls | `IWin32Window` - Window handle |
| `Control`, `Form` in View interface members | `IWin32Window` for modal ownership |
| UI implementation details | OS/Platform abstractions |

**Real Violation Example:**
```csharp
// ❌ This WOULD violate Rule 4
public interface IMyView : IWindowView
{
    Button SaveButton { get; }      // ❌ UI control type
    TextBox NameBox { get; }        // ❌ UI control type
}

// ✅ IWin32Window is different - it's infrastructure
public interface IWindowNavigator
{
    void ShowModal(IPresenter p, IWin32Window owner);  // ✅ OK
}
```

### Benefits of Following This Rule

| Aspect | Benefit |
|--------|---------|
| **Testability** | Mock Views don't need WinForms infrastructure |
| **Portability** | Can implement View with different UI frameworks |
| **Encapsulation** | View controls can change without affecting Presenter |
| **Maintainability** | Clear contracts make refactoring safer |
| **Team Development** | Presenter developers don't need UI knowledge |

### Analyzer Support

**Diagnostic**: `MVP004`
**Severity**: Error

The analyzer automatically detects UI element types in:
- View interface properties and method signatures
- Presenter fields, properties, and method signatures

**Detected types**: Button, TextBox, Label, Control, Form, and 30+ more WinForms types

---

## Rule 5: OnXxx() Naming for Event Handlers

**Private/protected methods that handle events should be named `OnXxx()`.**

### ✅ Correct Examples

```csharp
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    protected override void OnViewAttached()
    {
        View.SaveRequested += OnSave;
        View.CancelRequested += OnCancel;
    }

    private void OnSave(object sender, EventArgs e)
    {
        SaveTask();
    }

    private void OnCancel(object sender, EventArgs e)
    {
        RaiseClose();   // private helper that fires IRequestClose.CloseRequested
    }

    private void OnTaskSelectionChanged()
    {
        UpdateTaskDetails();
    }
}
```

### ❌ Incorrect Examples

```csharp
// ❌ Missing 'On' prefix
private void Save() { }
private void HandleSave() { }
private void SaveEventHandler() { }

// ❌ Incorrect prefix
private void DoSave() { }
private void ProcessSave() { }
```

### Acceptable Exceptions

Some helper methods don't need the `On` prefix:

```csharp
// ✅ Helper methods (not event handlers)
private void ValidateInput() { }
private void CalculateTotal() { }
private void UpdateDisplay() { }
private void RaiseClose() { }    // Helper that raises IRequestClose.CloseRequested
```

### Why This Matters

- **Readability**: Clearly identifies event handlers at a glance
- **Consistency**: Follows .NET Framework conventions (`OnLoad`, `OnClick`, etc.)
- **Searchability**: Easy to find all event handlers in a class

---

## Rule 6: Minimize View-to-Presenter Calls

**View should rarely call Presenter methods directly. Use ViewAction system instead.**

### The Problem

In traditional code, Views are littered with presenter method calls:

```csharp
// ❌ Anti-pattern - View knows too much about Presenter
public class TaskForm : Form, ITaskView
{
    private TaskPresenter _presenter;

    private void saveButton_Click(object sender, EventArgs e)
    {
        _presenter.OnSave();  // ❌ Direct coupling
    }

    private void deleteButton_Click(object sender, EventArgs e)
    {
        _presenter.OnDelete();  // ❌ Direct coupling
    }

    private void refreshButton_Click(object sender, EventArgs e)
    {
        _presenter.OnRefresh();  // ❌ Direct coupling
    }
}
```

### The Solution: ViewAction System

```csharp
// ✅ Correct - View uses ActionBinder property pattern
public interface ITaskView : IWindowView
{
    ViewActionBinder ActionBinder { get; }
}

public class TaskForm : Form, ITaskView
{
    private ViewActionBinder _binder;

    public ViewActionBinder ActionBinder => _binder;

    public TaskForm()
    {
        InitializeComponent();
        InitializeActionBindings();
    }

    private void InitializeActionBindings()
    {
        _binder = new ViewActionBinder();

        // Declarative binding - no Presenter knowledge needed
        _binder.Add(CommonActions.Save, _saveButton);
        _binder.Add(CommonActions.Delete, _deleteButton);
        _binder.Add(CommonActions.Refresh, _refreshButton);

        // Framework will call Bind() automatically
    }
}

// Presenter - registers handlers
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Save, OnSave);
        Dispatcher.Register(CommonActions.Delete, OnDelete);
        Dispatcher.Register(CommonActions.Refresh, OnRefresh);
        // Framework automatically calls View.ActionBinder?.Bind(_dispatcher)
    }
}
```

### Benefits

- **Decoupling**: View doesn't need reference to Presenter
- **Testability**: View can be tested without Presenter
- **Reusability**: Same View can work with different Presenters
- **CanExecute**: Automatic button enable/disable based on state

### See Also

- [ViewAction System Documentation](https://github.com/pasysxa/winforms-mvp/blob/master/CLAUDE.md#viewaction-system)
- [ViewAction Example](Example-ViewAction-Pattern)

---

## Rule 7: No Return Values from Presenter Methods

**Public Presenter methods should return `void` (Tell, Don't Ask principle).**

### ✅ Correct Examples

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    // ✅ Tell View what to do, don't ask for data
    private void OnSave()
    {
        var user = new User
        {
            Name = View.UserName,
            Email = View.Email
        };

        _userService.Save(user);

        View.ShowSuccessMessage();  // Tell
        View.ClearForm();           // Tell
    }

    // ✅ Query View state through properties
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            CommonActions.Save,
            OnSave,
            canExecute: () => View.HasUnsavedChanges);  // Query via property
    }
}
```

### ❌ Incorrect Examples

```csharp
// ❌ Asking View for data via method return
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    // ❌ Returns data - violates Tell, Don't Ask
    public bool OnSave()
    {
        if (!IsValid())
            return false;  // ❌ Asking

        SaveUser();
        return true;  // ❌ Asking
    }

    // ❌ Returning validation result
    public ValidationResult ValidateInput()
    {
        // ❌ Should use View properties or Tell pattern
        return new ValidationResult();
    }
}
```

### Why This Matters

- **Command-Query Separation**: Methods either do something (command) or return data (query), not both
- **Simplicity**: Caller doesn't need to handle return values
- **Testability**: Easier to mock and verify behavior
- **Async-Friendly**: `void` methods can be easily converted to `async Task`

### Acceptable Exceptions

Framework methods and override requirements:

```csharp
// ✅ Framework lifecycle methods (overridable)
protected override void OnInitialize() { ... }
protected override void OnViewAttached() { ... }

// ✅ Private helper methods (not public API)
private bool IsValid() { return true; }
```

> **Note on closing**: The framework no longer requires a public `CanClose()` method.
> See the [Window Closing Pattern](../CLAUDE.md#window-closing-pattern) — for the
> Pull direction the Presenter privately subscribes to `View.Closing`; for the Push
> direction it raises the `IRequestClose<TResult>.CloseRequested` event (typically
> through a small private helper). The only public surface the Presenter exposes
> beyond `Initialize` / `Dispose` is the `CloseRequested` event itself.

### Analyzer Support

**Diagnostic**: `MVP006`
**Severity**: Warning

---

## Rule 8: Access View Only Through Interface

**Presenter must never reference concrete Form types. Always use View interfaces.**

### ✅ Correct Examples

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    // ✅ Access View through interface
    protected override void OnInitialize()
    {
        View.UserName = LoadUserName();  // IUserEditorView
        View.Email = LoadEmail();        // IUserEditorView
    }

    // ✅ No fields of concrete Form type
    // View property is IUserEditorView (from base class)
}
```

### ❌ Incorrect Examples

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    // ❌ Field with concrete Form type
    private UserEditorForm _form;  // WRONG!

    public UserEditorPresenter(UserEditorForm form)  // ❌ WRONG!
    {
        _form = form;
    }

    protected override void OnInitialize()
    {
        // ❌ Accessing concrete Form
        _form.textBoxUserName.Text = "John";  // Violates encapsulation
        _form.BackColor = Color.Blue;         // UI manipulation in Presenter
    }
}
```

### Why This Matters

- **Testability**: Can mock View interface without creating Form
- **Substitutability**: Can swap View implementations (WinForms, WPF, Web)
- **Encapsulation**: Presenter doesn't know about UI implementation details
- **Flexibility**: View can change internal controls without affecting Presenter

### Correct Pattern for View Operations

**❌ WRONG - Do NOT use pattern matching to access Form:**

```csharp
// ❌ WRONG - Breaks testability and reusability
protected override void OnCancel()
{
    if (View is Form form)
    {
        form.Close();  // ❌ Violates Rule 4 - Form is a UI element type
    }
}
```

**✅ CORRECT - Implement `IRequestClose<TResult>` or use a View interface method:**

```csharp
// Option 1: IRequestClose<TResult> pattern (recommended)
public class MyPresenter : WindowPresenterBase<IMyView>, IRequestClose<MyResult>
{
    public event EventHandler<CloseRequestedEventArgs<MyResult>> CloseRequested;

    private void OnCancel()
        => CloseRequested?.Invoke(this,
               new CloseRequestedEventArgs<MyResult>(null, InteractionStatus.Cancel));
    private void OnSave()
        => CloseRequested?.Invoke(this,
               new CloseRequestedEventArgs<MyResult>(new MyResult { ... }, InteractionStatus.Ok));
}

// Option 2: View interface method
public interface IMyView : IWindowView
{
    void CloseView();
}

public class MyPresenter : WindowPresenterBase<IMyView>
{
    protected override void OnCancel()
    {
        View.CloseView();  // ✅ Works with any View implementation
    }
}
```

See **Rule 4** for complete details on why `Form` must never appear in Presenter code.

### Analyzer Support

**Diagnostic**: `MVP007`
**Severity**: Error

The analyzer detects fields and properties with concrete Form types in Presenter classes.

---

## Rule 9: View Method Visibility

**Public methods in View implementations must be defined in the View interface.**

### ✅ Correct Examples

```csharp
// View interface
public interface ITaskView : IWindowView
{
    void AddTask(TaskModel task);      // ✅ Defined in interface
    void RemoveTask(int taskId);       // ✅ Defined in interface
    void HighlightTask(int taskId);    // ✅ Defined in interface
}

// View implementation
public class TaskForm : Form, ITaskView
{
    // ✅ Public methods are in interface
    public void AddTask(TaskModel task)
    {
        _listView.Items.Add(CreateTaskItem(task));
    }

    public void RemoveTask(int taskId)
    {
        var item = FindTaskItem(taskId);
        _listView.Items.Remove(item);
    }

    // ✅ Private helpers - OK
    private ListViewItem CreateTaskItem(TaskModel task)
    {
        return new ListViewItem(task.Title);
    }
}
```

### ❌ Incorrect Examples

```csharp
// View interface
public interface ITaskView : IWindowView
{
    void AddTask(TaskModel task);
    // ❌ UpdateTaskColor not defined
}

// View implementation
public class TaskForm : Form, ITaskView
{
    public void AddTask(TaskModel task) { }

    // ❌ Public method not in interface
    public void UpdateTaskColor(int taskId, Color color)
    {
        // This breaks the contract!
    }
}
```

### Why This Matters

- **Contract Enforcement**: Interface defines the complete View API
- **Testability**: Mock Views must implement all public methods
- **Discoverability**: All View capabilities are documented in interface
- **Refactoring Safety**: Can't accidentally break Presenter by changing View

### Exceptions

WinForms framework methods are excluded:

```csharp
// ✅ Framework methods don't need to be in interface
public void Dispose() { }              // IDisposable
protected override void OnLoad() { }   // Form lifecycle
public void InitializeComponent() { }  // Designer-generated
```

### Analyzer Support

**Diagnostic**: `MVP008`
**Severity**: Warning

---

## Rule 10: Only Presenter Accesses View

**View should never be accessed from outside the Presenter.**

### ✅ Correct Example

```csharp
// ✅ Only Presenter talks to View
public class MainPresenter : WindowPresenterBase<IMainView>
{
    private readonly INavigator _navigator;
    private readonly TaskPresenter _taskPresenter;

    private void OnShowTasks()
    {
        // ✅ Tell child Presenter to show, not View directly
        _navigator.ShowWindow(_taskPresenter);

        // ❌ NEVER do: _taskPresenter.View.Show();
    }
}
```

### ❌ Incorrect Example

```csharp
// ❌ Other components accessing View directly
public class ReportGenerator
{
    public void GenerateReport(TaskPresenter presenter)
    {
        // ❌ Accessing View from outside Presenter
        var tasks = presenter.View.GetSelectedTasks();  // WRONG!

        // ❌ This breaks encapsulation
        presenter.View.ShowProgress(true);  // WRONG!
    }
}
```

### Why This Matters

- **Single Responsibility**: Only Presenter coordinates View behavior
- **Testability**: Clear ownership of View interactions
- **Maintainability**: Changes to View-Presenter interaction are localized
- **Thread Safety**: Presenter can ensure proper UI thread marshaling

---

## Rule 11: Long Meaningful Names

**Use descriptive, domain-specific names that clearly express intent.**

### ✅ Correct Examples

```csharp
public interface IOrderView : IWindowView
{
    // ✅ Long, descriptive, business-focused
    void DisplayCustomerOrderHistory(IEnumerable<OrderModel> orders);
    void HighlightOverdueOrders();
    void ShowOrderShippedNotification(string orderNumber);
    void MarkOrderAsReadyForShipping(int orderId);
    void UpdateEstimatedDeliveryDate(int orderId, DateTime date);
}
```

### ❌ Incorrect Examples

```csharp
public interface IOrderView : IWindowView
{
    // ❌ Too short, unclear intent
    void Show(List<Order> o);
    void Update();
    void Set(int id);
    void Get();

    // ❌ Generic, non-descriptive
    void DoWork();
    void Process();
    void Handle();
}
```

### Guidelines

| Pattern | ✅ Good | ❌ Bad |
|---------|--------|--------|
| **Verbs** | Display, Highlight, Calculate, Validate | Do, Handle, Process |
| **Length** | 3-6 words | 1 word or abbreviations |
| **Domain Terms** | "Customer", "Order", "Shipment" | "Data", "Item", "Thing" |
| **Specificity** | `MarkOrderAsShipped` | `UpdateOrder` |

### Why This Matters

- **Self-Documenting**: Code is easier to understand without comments
- **Searchability**: Unique names are easier to find
- **Communication**: Business stakeholders can understand method names
- **Refactoring**: Clear intent makes it easier to identify responsibilities

---

## Rule 12: Prefer Methods Over Properties

**Interfaces should contain methods, not properties. Exception: WinForms data binding scenarios.**

### Framework Position

**The WinForms MVP Framework makes a deliberate exception to the classic MVP rule** for bidirectional properties.

### ✅ Preferred Pattern

```csharp
// ✅ Setter-only properties (one-way flow)
public interface IReportView : IWindowView
{
    string StatusMessage { set; }
    int ProgressPercentage { set; }
    bool IsProcessing { set; }
}

// ✅ Getter-only properties (state queries)
public interface ITaskView : IWindowView
{
    bool HasSelection { get; }
    bool HasUnsavedChanges { get; }
    bool IsValid { get; }
}

// ✅ Methods for complex operations
public interface ITaskView : IWindowView
{
    void DisplayTasks(IEnumerable<TaskModel> tasks);
    void ClearForm();
    void ShowValidationErrors(IEnumerable<string> errors);
}
```

### ⚠️ Accepted Pattern (WinForms Data Binding)

```csharp
// ⚠️ Bidirectional properties - ACCEPTED for form inputs
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }  // ⚠️ Warning, but acceptable
    string Email { get; set; }     // Enables data binding
    DateTime BirthDate { get; set; }
}
```

### Why Bidirectional Properties Are Acceptable

**1. WinForms Data Binding Support**

```csharp
// View implementation uses data binding
_textBox.DataBindings.Add("Text", viewModel, nameof(UserName));
```

**2. Read-Back for Validation**

```csharp
// Presenter needs to read current input
private void OnSave()
{
    if (string.IsNullOrEmpty(View.UserName))  // Need getter
    {
        View.ShowValidationError("Name required");
        return;
    }
}
```

**3. CanExecute Predicates**

```csharp
Dispatcher.Register(
    CommonActions.Save,
    OnSave,
    canExecute: () => View.HasUnsavedChanges);  // Need getter
```

### When to Avoid Bidirectional Properties

```csharp
// ❌ Complex objects should use methods
public interface IReportView : IWindowView
{
    ReportData Data { get; set; }  // ❌ Use DisplayReport(ReportData) instead
}

// ✅ Better: Method makes intent clear
public interface IReportView : IWindowView
{
    void DisplayReport(ReportData data);
    void ClearReport();
}
```

### Summary Table

| Pattern | Status | Use Case |
|---------|--------|----------|
| **Setter-only** `{ set; }` | ✅ Preferred | One-way data flow from Presenter to View |
| **Getter-only** `{ get; }` | ✅ Preferred | State queries (HasSelection, IsValid) |
| **Bidirectional** `{ get; set; }` | ⚠️ Accepted | Form inputs, data binding scenarios |
| **Methods** | ✅ Preferred | Complex operations, collections, void operations |

---

## Rule 13: All Data in Model

**Business data should be in Model classes, not just in UI controls.**

### ✅ Correct Example

```csharp
// Model - owns the data
public class TaskModel
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime DueDate { get; set; }
    public TaskStatus Status { get; set; }
}

// Presenter - works with Model
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    private TaskModel _task;

    private void OnSave()
    {
        // ✅ Data lives in Model
        _task.Title = View.TaskTitle;
        _task.Description = View.TaskDescription;
        _task.DueDate = View.DueDate;

        _taskRepository.Save(_task);
    }
}

// View - displays Model data
public interface ITaskView : IWindowView
{
    string TaskTitle { get; set; }
    string TaskDescription { get; set; }
    DateTime DueDate { get; set; }
}
```

### ❌ Incorrect Example

```csharp
// ❌ No Model - data only exists in View/controls
public class TaskPresenter : WindowPresenterBase<ITaskView>
{
    private void OnSave()
    {
        // ❌ Pulling data directly from View without Model
        var title = View.TaskTitle;
        var description = View.TaskDescription;
        var dueDate = View.DueDate;

        // ❌ Passing individual fields instead of Model object
        _taskRepository.Save(title, description, dueDate);
    }
}
```

### Why This Matters

- **Separation of Concerns**: Business data separate from UI representation
- **Testability**: Can test business logic without UI
- **Reusability**: Same Model can be used across different Views
- **Validation**: Business rules belong with the data
- **Persistence**: Easy to serialize/deserialize Model objects

### Advanced Pattern: ChangeTracker

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnInitialize()
    {
        var user = _userRepository.GetById(userId);
        _changeTracker = new ChangeTracker<UserModel>(user);

        // Bind Model to View
        View.Model = _changeTracker.CurrentValue;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(
            CommonActions.Save,
            OnSave,
            canExecute: () => _changeTracker.IsChanged);  // Track changes
    }

    private void OnReset()
    {
        _changeTracker.RejectChanges();  // Revert to original
        View.Model = _changeTracker.CurrentValue;
    }
}
```

---

## Rule 14: No UI Control Names in Interface

**View interface methods should use domain-specific names, not UI control type names.**

### ✅ Correct Examples

```csharp
public interface INavigationView : IWindowView
{
    // ✅ Domain language, not UI types
    void AddNavigationItem(string text, string category);
    void RemoveNavigationItem(string id);
    void HighlightCurrentSection(string sectionName);
    void ExpandCategory(string categoryName);
}

public interface ICustomerView : IWindowView
{
    // ✅ Business operations, not control names
    void DisplayCustomers(IEnumerable<CustomerModel> customers);
    void HighlightSelectedCustomer(int customerId);
    void UpdateCustomerStatus(int customerId, string status);
}
```

### ❌ Incorrect Examples

```csharp
public interface INavigationView : IWindowView
{
    // ❌ Exposes UI control types
    void AddTreeViewNode(string text);           // ❌ "TreeView"
    void RemoveListBoxItem(int index);           // ❌ "ListBox"
    void SetDataGridViewColor(Color color);      // ❌ "DataGridView"
    void UpdateTextBox(string text);             // ❌ "TextBox"
}

public interface ICustomerView : IWindowView
{
    // ❌ Control-centric naming
    void FillComboBox(IEnumerable<CustomerModel> customers);  // ❌ "ComboBox"
    void SelectListViewItem(int index);                       // ❌ "ListView"
    void EnableButton(bool enable);                           // ❌ "Button"
}
```

### Why This Matters

- **Abstraction**: View can change UI controls without affecting contract
- **Testability**: Mock Views don't need to know about WinForms controls
- **Maintainability**: If you change TreeView to ListView, interface doesn't change
- **Business Focus**: Interface describes what happens, not how

### Migration Example

```csharp
// ❌ Before (UI-centric)
public interface ITaskView : IWindowView
{
    void AddTreeViewNode(string taskName);  // ❌ Tied to TreeView
}

// ✅ After (domain-centric)
public interface ITaskView : IWindowView
{
    void AddTask(TaskModel task);  // ✅ Can use any control internally
}

// View can now freely change implementation:
public class TaskForm : Form, ITaskView
{
    // Internal implementation detail - can change anytime
    public void AddTask(TaskModel task)
    {
        // Could be TreeView, ListView, DataGridView, or custom control
        _taskTreeView.Nodes.Add(CreateTaskNode(task));
    }
}
```

### Analyzer Support

**Diagnostic**: `MVP013`
**Severity**: Warning

The analyzer automatically detects UI control type names in interface methods.

**Detected control types**: Button, TextBox, Label, ListBox, DataGrid, DataGridView, TreeView, ListView, ComboBox, CheckBox, RadioButton, and 20+ more.

---

## Rule 15: Domain-Driven Naming

**Use business domain language throughout the interface, not technical UI terms.**

### ✅ Correct Examples

```csharp
public interface IOrderManagementView : IWindowView
{
    // ✅ Business domain language
    void DisplayPendingOrders(IEnumerable<OrderModel> orders);
    void MarkOrderAsShipped(int orderId);
    void HighlightOverdueOrders();
    void ShowShippingLabelPrintDialog(int orderId);
    void UpdateInventoryStatus(string sku, int quantity);
    void CalculateAndDisplayOrderTotal(OrderModel order);
}

public interface ICustomerSupportView : IWindowView
{
    // ✅ Domain-specific actions
    void DisplayCustomerTickets(IEnumerable<TicketModel> tickets);
    void EscalateTicketToPriority(int ticketId);
    void AssignTicketToAgent(int ticketId, string agentName);
    void ShowCustomerCommunicationHistory(int customerId);
}
```

### ❌ Incorrect Examples

```csharp
public interface IOrderManagementView : IWindowView
{
    // ❌ Technical UI language
    void SetDataSource(IEnumerable<object> data);      // ❌ "DataSource"
    void SetRowColor(int rowIndex, Color color);       // ❌ "Row"
    void UpdateLabel(string text);                     // ❌ "Label"
    void ShowDialog();                                 // ❌ Generic
    void RefreshGrid();                                // ❌ "Grid"
}
```

### Comparison Table

| ❌ UI-Centric | ✅ Domain-Centric |
|--------------|------------------|
| `SetDataSource()` | `DisplayCustomers()` |
| `UpdateGrid()` | `RefreshOrderList()` |
| `SetRowColor()` | `HighlightOverdueOrder()` |
| `FillComboBox()` | `LoadCategories()` |
| `EnableButton()` | `AllowOrderSubmission()` |
| `SetLabelText()` | `UpdateOrderStatus()` |

### Why This Matters

- **Communication**: Business stakeholders can understand the interface
- **Documentation**: Interface serves as domain glossary
- **Maintainability**: Changes in UI technology don't affect domain language
- **Onboarding**: New developers understand business logic faster

### Example: Before and After

```csharp
// ❌ Before - Technical language
public interface IReportView : IWindowView
{
    void PopulateDropDown(IEnumerable<string> items);
    void SetProgressBarValue(int value);
    void UpdateStatusLabel(string text);
    void EnableGenerateButton(bool enabled);
}

// ✅ After - Domain language
public interface IReportView : IWindowView
{
    void LoadAvailableReportTypes(IEnumerable<ReportType> types);
    void UpdateReportGenerationProgress(int percentComplete);
    void DisplayReportStatus(string statusMessage);
    void AllowReportGeneration(bool isReady);
}
```

---

## Rule 16: Presenter Method Visibility

**Presenter methods should be as private as possible. Default to `private`; only escalate visibility when there is a concrete reason.**

The Presenter is **not a service** that exposes operations to the outside world — it is an event-driven coordinator. Every additional `public` method increases the surface area that callers can rely on, weakens the framework's lifecycle guarantees, and tempts test code to bypass the real event flow.

### Visibility Decision Table

| Method Kind | Visibility | Reason |
|-------------|-----------|--------|
| Constructor | `public` | Required for DI / instantiation |
| Interface contract implementation (e.g. `IRequestClose<T>` event) | `public` | Required by the contract |
| Lifecycle hooks (`OnInitialize`, `OnViewAttached`, `RegisterViewActions`, `Cleanup`) | `protected override` | Inherited from base class, only the framework should call them |
| ViewAction handlers (`OnSave`, `OnCancel`, …) | `private` | Invoked exclusively through `Dispatcher` |
| View event handlers (`OnViewClosing`, `OnSelectionChanged`, …) | `private` | Subscribed in `OnViewAttached`; not called from outside |
| Helpers (`RaiseClose`, validation, formatters) | `private` | Internal implementation detail |

### ❌ Anti-Patterns

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    // ❌ Direct public methods - lets external code bypass the ViewAction flow
    public void Save() { ... }
    public void Delete() { ... }
    public void Reset() { ... }

    // ❌ Public state queries - encourages Tell-Don't-Ask violations
    public bool CanSave => _changeTracker.IsChanged;
    public string CurrentUserName => View.UserName;

    // ❌ "Just in case" public method
    public void RefreshData() { ... }
}
```

**Problems:**
- Test code is tempted to call `presenter.Save()` directly, bypassing the real `Dispatcher → CanExecute → handler` path.
- The View (or another Presenter) can ask the Presenter for its internal state — the opposite of *Tell, Don't Ask*.
- The Presenter ends up looking like a service / facade, blurring the responsibility boundary with domain services.

### ✅ Correct Pattern

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>,
                                    IRequestClose<UserResult>
{
    // ✅ Required by interface contract
    public event EventHandler<CloseRequestedEventArgs<UserResult>> CloseRequested;

    // ✅ Required for dependency injection
    public UserEditorPresenter(IUserRepository repository) { ... }

    // ✅ Framework-only callers — protected override
    protected override void OnViewAttached() { ... }
    protected override void OnInitialize() { ... }
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Save, OnSave,
            canExecute: () => _changeTracker.IsChanged);
        Dispatcher.Register(CommonActions.Cancel, OnCancel);
    }

    // ✅ Dispatcher-only callers — private
    private void OnSave() { ... }
    private void OnCancel() { ... }
    private void OnViewClosing(object sender, WindowClosingEventArgs args) { ... }

    // ✅ Internal helper — private
    private void RaiseClose(UserResult result, InteractionStatus status)
        => CloseRequested?.Invoke(this, new CloseRequestedEventArgs<UserResult>(result, status));
}
```

### Testing Without Exposing Internals

A common counter-argument is "I need `public` to test it." This is the wrong fix — test through the real entry points:

```csharp
// ❌ Wrong: changes visibility just for tests
presenter.OnSave();                       // requires OnSave to be internal/public

// ✅ Right: drive the Presenter the way production code does
presenter.Dispatcher.Dispatch(CommonActions.Save);   // exercises CanExecute + handler
view.RaiseClosing(CloseReason.Normal);               // exercises the Pull-direction close
presenter.CloseRequested += (s, e) => captured = e;  // observes Push-direction close
```

Testing through the public surface (events + Dispatcher + View events) verifies the real behavior, including CanExecute predicates that direct calls would skip.

### Why This Matters

- **Lifecycle integrity**: Methods called outside the expected event flow can run before `OnInitialize` completes, after `Cleanup`, or while CanExecute is `false`.
- **Discoverability**: When the only public surface is the constructor + interface contract, reading the type tells you exactly how it is supposed to be used.
- **Refactoring safety**: Private/protected methods can be renamed, merged, or removed without affecting any other code in the solution.
- **YAGNI**: Promotion is cheap (`private` → `public`), demotion is a breaking change.

### Default Rule

> **When in doubt, make it `private`.** If a real caller appears later, escalate to `protected` (subclasses) or `internal` (same-assembly tests) before reaching for `public`.

---

## Rule 17: Presenter Event Visibility

**Public events on a Presenter must be limited to interface-contract events. Cross-component notification belongs in services or the Event Aggregator, not in Presenter-level events.**

Events are more dangerous than methods: every public event creates a hidden subscriber graph, a potential memory-leak path (forgotten `-=`), and a back channel through which external code can observe Presenter state. The framework already provides better-targeted mechanisms — use them first.

### Allowed Public Events (Whitelist)

| Pattern | Example | Why It's Allowed |
|---------|---------|-----------------|
| `IRequestClose<TResult>.CloseRequested` | [`ComposeEmailPresenter.cs:47`](../src/WinformsMVP.Samples/EmailDemo/ComposeEmailPresenter.cs#L47), [`WindowClosingDemoPresenter.cs:48`](../src/WinformsMVP.Samples/WindowClosingDemo/WindowClosingDemoPresenter.cs#L48) | Required by the framework contract; consumed only by `WindowNavigator` |
| Custom close-result variants (`IRequestClose<MyResult>`) | Any modal Presenter returning a typed result | Same contract — just a different `TResult` |

Anything outside this whitelist needs justification. In the entire sample suite the only public Presenter events are `CloseRequested`.

### ❌ Anti-Patterns

```csharp
public class OrderEditorPresenter : WindowPresenterBase<IOrderEditorView>
{
    // ❌ Leaks internal state - any subscriber can observe Presenter lifecycle
    public event EventHandler IsDirtyChanged;
    public event EventHandler DataLoaded;
    public event EventHandler ValidationFailed;

    // ❌ Re-publishes a View event - subscribers should listen to the View directly
    public event EventHandler SelectionChanged;

    // ❌ "Just in case" public event - YAGNI violation
    public event EventHandler<int> CounterChanged;
}
```

**Problems:**
- **Who subscribes?** The Presenter has no idea; ownership of cleanup becomes unclear.
- **Memory leaks**: If a long-lived component subscribes and forgets to unsubscribe, the Presenter (and through it, the View) is held forever.
- **Inverted dependency**: A Presenter publishing internal state events makes other components depend on its internals, defeating MVP isolation.

### ✅ Use the Right Channel

Before adding a public event to a Presenter, walk this decision tree:

```
A Presenter needs to notify "something" of a change.
    │
    ├─ Is it a window close result? ────────────────► Implement IRequestClose<T>.CloseRequested
    │
    ├─ Does a single parent Presenter own this child? ► Call a public method on the parent OR
    │                                                  let the parent subscribe and have the
    │                                                  child raise a single event (last resort)
    │
    ├─ Does shared state change (orders, auth, etc.)? ► Put the state in a Service; raise the
    │                                                  event from the Service.
    │
    └─ Is this a cross-module notification?           ► Publish via IEventAggregator (weak refs,
                                                       UI-thread marshaled, see CLAUDE.md).
```

### Concrete Alternatives (from this framework)

**Shared service with events** — used in [`ComplexInteractionDemo/Services/OrderManagementService.cs`](../src/WinformsMVP.Samples/ComplexInteractionDemo/Services/OrderManagementService.cs):

```csharp
public class OrderManagementService : IOrderManagementService
{
    public event EventHandler<ProductAddedEventArgs> ProductAdded;
    public event EventHandler<ItemRemovedEventArgs> ItemRemoved;
    public event EventHandler<TotalChangedEventArgs> TotalChanged;
    public event EventHandler OrderCleared;
    // ...
}
```

Service owns the state; Presenters subscribe to the service, not to each other.

**Event Aggregator** — used in [`ComplexInteractionDemo_EventBased`](../src/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/):

```csharp
// Publisher (Presenter A) — no event needed on the Presenter
_eventAggregator.Publish(new ProductAddedMessage { Product = p, Quantity = q });

// Subscriber (Presenter B) — uses weak references, auto-cleanup on GC
_eventAggregator.Subscribe<ProductAddedMessage>(OnProductAdded);
```

Decouples publisher and subscriber entirely; no public surface on either Presenter.

### When a Public Event Is the Right Answer

If, after the decision tree above, you still need a public event on a Presenter, follow these rules:

1. **Use a `XxxEventArgs` derived type**, not `EventHandler<int>` / `EventHandler<string>`. Future fields can be added without breaking subscribers.
2. **Pair every `+=` with `-=`** in `Cleanup()` / `Dispose()`. Forgotten unsubscribes are the #1 source of WinForms memory leaks.
3. **Document the expected subscriber** in an XML doc comment. If you cannot name a single legitimate subscriber type, the event probably shouldn't exist.
4. **Prefer a weak-reference channel** (Event Aggregator) for long-lived subscriptions.

### Why This Matters

- **MVP isolation**: A Presenter publishing internal state events couples external code to its implementation; refactoring the Presenter ripples outward.
- **Memory safety**: WinForms apps frequently leak through forgotten event subscriptions; the framework's services and Event Aggregator are designed to handle this for you.
- **Discoverability**: When the only public events on Presenters are `CloseRequested`, code reviewers instantly know what every Presenter contributes to the outside world.
- **Framework consistency**: Sticking to the whitelist keeps every Presenter in the codebase shaped the same way, which is a major aid to onboarding and refactoring.

### Default Rule

> **A Presenter has zero public events unless it implements `IRequestClose<TResult>`.** Anything else needs a service or the Event Aggregator first.

---

## Automated Enforcement

### Roslyn Analyzers

All rules marked with **Analyzer Support** are automatically enforced by the WinForms MVP Analyzers NuGet package.

**Installation**:

```bash
dotnet add package WinformsMVP.Analyzers
```

**Diagnostics**:

| Rule | Diagnostic ID | Severity |
|------|---------------|----------|
| Rule 1: View Naming | MVP001 | Warning |
| Rule 2: Presenter Naming | MVP002 | Warning |
| Rule 3: No UI in Presenter | MVP003 | Error |
| Rule 6: No Return Values | MVP006 | Warning |
| Rule 7: View Interface Only | MVP007 | Error |
| Rule 8: View Method Visibility | MVP008 | Warning |
| Rule 13: No UI Control Names | MVP013 | Warning |

### PowerShell Compliance Checker

For comprehensive compliance checking, run the PowerShell script:

```powershell
cd tools
.\check-mvp-compliance.ps1 -Verbose
```

This generates a detailed compliance report covering all 14 rules.

---

## Related Documentation

- **[MVP-DESIGN-RULES.md](https://github.com/pasysxa/winforms-mvp/blob/master/MVP-DESIGN-RULES.md)** - Complete reference guide
- **[CLAUDE.md](https://github.com/pasysxa/winforms-mvp/blob/master/CLAUDE.md)** - Framework architecture
- **[MVP-COMPLIANCE-REPORT.md](https://github.com/pasysxa/winforms-mvp/blob/master/MVP-COMPLIANCE-REPORT.md)** - Compliance analysis

---

## Quick Reference Checklist

Use this checklist during code reviews:

- [ ] **Rule 1**: All View interfaces end with `View`
- [ ] **Rule 2**: All Presenters end with `Presenter`
- [ ] **Rule 3**: Presenter handles use-case logic only
- [ ] **Rule 4**: Event handlers named `OnXxx()`
- [ ] **Rule 5**: Using ViewAction system (minimal direct calls)
- [ ] **Rule 6**: No return values from Presenter methods
- [ ] **Rule 7**: Access View only through interface
- [ ] **Rule 8**: View public methods are in interface
- [ ] **Rule 9**: Only Presenter accesses View
- [ ] **Rule 10**: Long meaningful method names
- [ ] **Rule 11**: Properties used appropriately
- [ ] **Rule 12**: Data in Model, not just UI controls
- [ ] **Rule 13**: No UI control names in interface
- [ ] **Rule 14**: Domain-driven naming
- [ ] **Rule 16**: Presenter methods default to `private`; `public` only for constructor + interface contract
- [ ] **Rule 17**: No public events on Presenter except `IRequestClose<T>.CloseRequested`

---

**📊 Framework Compliance**: The WinForms MVP Framework sample code achieves **100% compliance** with all 17 design rules.
