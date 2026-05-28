# 🎯 WinForms MVP Framework

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](https://github.com/pasysxa/winforms-mvp)
[![Tests](https://img.shields.io/badge/tests-131%20passing-success.svg)](https://github.com/pasysxa/winforms-mvp)

A modern, enterprise-grade **Model-View-Presenter (MVP)** framework for WinForms applications, bringing WPF-style command binding and clean architecture to .NET Framework 4.8.

[What is MVP?](#-what-is-mvp) • [Features](#-core-features) • [Quick Start](QUICKSTART.md) 🚀 • [Documentation](CLAUDE.md) • [Examples](#-examples)

---

## 📖 Overview

WinForms MVP Framework solves the classic problems of WinForms development:
- **No separation of concerns** → Business logic mixed with UI code
- **Hard to test** → UI controls tightly coupled to logic
- **Event explosion** → Dozens of button click handlers
- **MessageBox hell** → Direct UI dependencies everywhere

This framework provides a **clean, testable architecture** with **minimal boilerplate** and **maximum productivity**.

---

## 🎓 What is MVP?

**MVP (Model-View-Presenter)** is an architectural pattern that separates your application into three distinct components, making your code more maintainable, testable, and scalable.

### 📐 The Three Components

```
    ┌─────────────────────────────────────────────────────────────────────┐
    │                             MVP Pattern                             │
    └─────────────────────────────────────────────────────────────────────┘

    ┌──────────────┐           ┌──────────────┐           ┌──────────────┐
    │              │           │              │           │              │
    │    Model     │◄──────────│  Presenter   │──────────►│     View     │
    │              │           │              │           │  (Interface) │
    │  Data & BL   │           │  Use Cases   │           │              │
    └──────────────┘           └──────────────┘           └──────────────┘
           ▲                          ▲                          ▲
           │                          │                          │
           │                          │                          │
    ┌──────┴────────┐          ┌──────┴──────┐           ┌───────┴────────┐
    │  Repositories │          │  Services   │           │  Form/Control  │
    │  DTOs/Entities│          │ Validation  │           │ (WinForms UI)  │
    └───────────────┘          └─────────────┘           └────────────────┘
```

#### 🎯 **Model** - Your Business Data
- **What**: Business entities, data transfer objects (DTOs), domain logic
- **Responsibility**: Represents the data and business rules
- **Examples**: `Customer`, `Order`, `UserProfile`
- **No knowledge of**: UI, Forms, Controls

#### 🖼️ **View** - The User Interface
- **What**: The visual representation (Forms, UserControls)
- **Responsibility**: Display data and capture user input
- **Examples**: `UserEditorForm`, `CustomerListControl`
- **Exposes**: Interface with data properties and events (NOT UI controls!)
- **No knowledge of**: Business logic, data validation

#### 🎮 **Presenter** - The Orchestrator
- **What**: The bridge between Model and View
- **Responsibility**: Use-case logic, orchestration, state management
- **Examples**: `UserEditorPresenter`, `CustomerListPresenter`
- **Coordinates**: Data retrieval, business logic, view updates
- **No knowledge of**: WinForms controls (Button, TextBox, etc.)

### 🔄 How MVP Works

```
User Action Flow:
──────────────────

1. User clicks button
        ↓
2. View raises event (via ViewAction or traditional event)
        ↓
3. Presenter handles event
        ↓
4. Presenter calls Model/Service
        ↓
5. Presenter updates View through interface
        ↓
6. View displays updated data
```

### ❌ Traditional WinForms vs ✅ MVP

<table>
<tr>
<td width="50%">

**❌ Traditional WinForms**
```csharp
// Form code-behind
public class UserForm : Form
{
    private void btnSave_Click(object sender, EventArgs e)
    {
        // 😱 Everything mixed together!

        // Validation
        if (string.IsNullOrEmpty(txtName.Text))
        {
            MessageBox.Show("Name required!");
            return;
        }

        // Business logic
        var user = new User
        {
            Name = txtName.Text,
            Email = txtEmail.Text
        };

        // Data access
        _repository.Save(user);

        // UI feedback
        MessageBox.Show("Saved!");
        this.Close();
    }
}
```

**Problems:**
- ❌ Cannot unit test (requires Form)
- ❌ Business logic tied to UI
- ❌ Hard to reuse logic
- ❌ Tight coupling to MessageBox
- ❌ Cannot mock data access

</td>
<td width="50%">

**✅ MVP Pattern**
```csharp
// View Interface
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    ViewActionBinder ActionBinder { get; }
}

// Presenter (Testable!)
public class UserEditorPresenter
    : WindowPresenterBase<IUserEditorView>
{
    private readonly IUserRepository _repository;

    public UserEditorPresenter(IUserRepository repo)
    {
        _repository = repo;
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(CommonActions.Save, OnSave);
    }

    private void OnSave()
    {
        // Validation
        if (string.IsNullOrEmpty(View.UserName))
        {
            Messages.ShowError("Name required!");
            return;
        }

        // Business logic
        var user = new User
        {
            Name = View.UserName,
            Email = View.Email
        };

        // Data access
        _repository.Save(user);

        // UI feedback
        Messages.ShowInfo("Saved!");
        RequestClose();
    }
}
```

**Benefits:**
- ✅ Fully unit testable
- ✅ Business logic separate
- ✅ Reusable presenter
- ✅ Mockable services
- ✅ No WinForms dependencies

</td>
</tr>
</table>

### 🎯 Key Benefits of MVP

| Benefit | Description |
|---------|-------------|
| **🧪 Testability** | Write unit tests without creating Forms - mock the View interface |
| **🔧 Maintainability** | Clear separation makes code easier to understand and modify |
| **♻️ Reusability** | Presenter logic can be reused across different Views |
| **🎨 Flexibility** | Change UI without touching business logic |
| **👥 Team Collaboration** | Designers work on Views, developers work on Presenters |
| **📚 Learning Curve** | Once learned, makes large applications much easier to manage |

### 🏗️ MVP in This Framework

This framework implements the **Supervising Controller** variant of MVP:

```
Supervising Controller Pattern:
────────────────────────────────

┌─────────────────────────────────────────────────┐
│  Presenter (Supervising Controller)             │
│  • Handles complex logic                        │
│  • Manages application state                    │
│  • Coordinates between Model and View           │
└─────────────────────────────────────────────────┘
                    │
                    ├────────────────┬────────────────┐
                    ▼                ▼                ▼
            ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
            │   Services   │  │  ViewActions │  │     View     │
            │              │  │  Dispatcher  │  │  (Smart!)    │
            └──────────────┘  └──────────────┘  └──────────────┘
                                                        │
                                    ┌───────────────────┼───────────────────┐
                                    ▼                   ▼                   ▼
                            Data Binding        Event Handling      UI Logic
```

**Key Principle:**
> **Presenter handles use-case logic only, not view logic.**
>
> Modern WinForms Views are smart enough to handle data-binding and event processing.
> Presenter focuses on **business workflow** (validate → save → notify),
> while View handles **UI details** (how to display errors, which controls to use).

---

### Why This Framework?

| Traditional WinForms | 🎯 This Framework |
|---------------------|------------------|
| ❌ Business logic in Form code-behind | ✅ Clean separation via Presenter |
| ❌ `MessageBox.Show()` scattered everywhere | ✅ Abstracted via `IMessageService` |
| ❌ Manual event wiring for every button | ✅ Declarative `ViewAction` system |
| ❌ No CanExecute support | ✅ Auto-enabled/disabled controls |
| ❌ Hard to unit test | ✅ Fully testable with mocks |
| ❌ Form references leak into logic | ✅ Interface-based View contracts |

---

## ✨ Core Features

### 🎮 ViewAction System
WPF's `ICommand` pattern for WinForms with type-safe action keys and automatic CanExecute support.

```csharp
// Presenter - No knowledge of buttons!
_dispatcher.Register(
    UserActions.Delete,
    OnDelete,
    canExecute: () => View.HasSelectedItem);  // Auto enable/disable

View.ActionBinder.Bind(_dispatcher);
```

```csharp
// Form - Internal UI binding
private void InitializeActionBindings()
{
    _binder = new ViewActionBinder();
    _binder.Add(UserActions.Delete, _deleteButton, _deleteMenuItem);
    // Multiple controls → one action!
}
```

### 🏗️ Perfect MVP Separation

**View Interface** - No UI types exposed:
```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }          // Data only
    bool HasSelectedUser { get; }          // State
    ViewActionBinder ActionBinder { get; } // Framework abstraction
    event EventHandler SelectionChanged;   // Events
}
```

**Form Implementation** - Owns UI details:
```csharp
public class UserEditorForm : Form, IUserEditorView
{
    private TextBox _nameTextBox;  // Private - not exposed!
    private Button _saveButton;    // Private - not exposed!

    public string UserName
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }
}
```

**Presenter** - Pure business logic:
```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    private void OnSave()
    {
        _userRepository.Save(View.UserName);  // No Form knowledge!
        Messages.ShowInfo("User saved!");      // No MessageBox.Show()!
    }
}
```

### 🚀 Service Abstraction Layer

Never write `MessageBox.Show()` or `new OpenFileDialog()` again:

```csharp
// ❌ Traditional - Hard to test
MessageBox.Show("Error!", "Error", MessageBoxButtons.OK);

// ✅ Framework - Fully testable
Messages.ShowError("Error!", "Error");

// ❌ Traditional - Leaks WinForms
var dialog = new OpenFileDialog();
if (dialog.ShowDialog() == DialogResult.OK) { ... }

// ✅ Framework - Abstracted
var result = Dialogs.ShowOpenFileDialog();
if (result.IsSuccess) { ... }
```

### 📊 Structured Logging

Built-in logging with a minimal in-house abstraction (`WinformsMVP.Logging.ILogger`) whose API mirrors **Microsoft.Extensions.Logging** — call sites read identically, and a ~30-line application-level adapter forwards to M.E.L. when you want its provider ecosystem. The main package has **zero external dependencies** so it multi-targets `net40;net48`.

```csharp
public class MyPresenter : WindowPresenterBase<IMyView>
{
    // No constructor needed - Logger property automatically available!

    private void OnSave()
    {
        try
        {
            SaveData();

            // Structured logging - parameters are captured for querying
            Logger.LogInformation("User {UserId} saved data successfully", userId);
        }
        catch (Exception ex)
        {
            // Exception logging with context
            Logger.LogError(ex, "Failed to save data for user {UserId}", userId);
            Messages.ShowError("Failed to save data", "Error");
        }
    }
}
```

**Key Benefits:**
- ✅ **Structured data** - Parameterized messages enable powerful querying in log aggregation systems
- ✅ **Extensible** - Bridge to M.E.L. providers (Debug, Console, File, Application Insights, Seq, Elasticsearch) via a small application-level adapter
- ✅ **net40-compatible** - Main package is BCL-only; works on .NET Framework 4.0
- ✅ **Testable** - NullLoggerFactory for zero overhead in tests
- ✅ **Familiar surface** - If you know `ILogger`, you already know how to use it

**Custom Configuration:**

The simplest setup uses the built-in `DebugLoggerFactory` (zero external dependencies, works on net40):

```csharp
// Program.cs
using WinformsMVP.Logging;
using WinformsMVP.Services.Implementations;

PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: null,
    loggerFactory: new DebugLoggerFactory());
```

To plug into the Microsoft.Extensions.Logging ecosystem (net48 only), copy the ~30-line adapter from [`MultiProjectDemo.Shell/Logging/`](src/MultiProjectDemo.Shell/Logging/) into your composition root, then:

```csharp
var msFactory = LoggerFactory.Create(builder =>
{
    builder.AddDebug();
    // .AddApplicationInsights(config);   // Azure
    // .AddSeq("http://localhost:5341");  // Seq
});

PlatformServices.Default = new DefaultPlatformServices(
    viewMappingRegister: null,
    loggerFactory: msFactory.AsFrameworkLoggerFactory());  // your adapter's extension
```

See [`LoggingDemoExample.cs`](src/WinformsMVP.Samples/LoggingDemoExample.cs) and [`MultiProjectDemo.Shell/`](src/MultiProjectDemo.Shell/) for complete working examples.

### 🎨 Flexible Dependency Injection

**Service Locator** (rapid prototyping):
```csharp
protected override void OnSave()
{
    Messages.ShowInfo("Saved!");  // Built-in convenience properties
}
```

**Constructor Injection** (production code):
```csharp
public MyPresenter(IMessageService messages, IUserRepository users)
{
    _messages = messages;
    _users = users;
}
```

**Hybrid** (recommended):
```csharp
public MyPresenter(IUserRepository users)  // Business services injected
{
    _users = users;
    Messages.ShowInfo("Ready!");  // Platform services via properties
}
```

### 🪟 Advanced Window Navigation

```csharp
// Modal dialog with parameters and result
var result = navigator.ShowWindowAsModal<EditUserPresenter, UserId, UserModel>(
    presenter,
    userId: 123);

if (result.IsSuccess)
{
    var user = result.Value;
    // ...
}

// Singleton window (document-style)
var window = navigator.ShowWindow<DocumentPresenter>(
    presenter,
    keySelector: p => documentId);  // Only one per document
```

### 📊 Change Tracking

Built-in edit/cancel support with thread-safe implementation:

```csharp
var tracker = new ChangeTracker<UserModel>(originalUser);

// User edits...
View.Model = tracker.CurrentValue;

// Save or cancel
if (confirmed)
    tracker.AcceptChanges();
else
    tracker.RejectChanges();  // Restore original
```

### 🔄 ActionRequest Pattern

Eliminate event explosion:

```csharp
// ❌ Traditional - Event hell
event EventHandler AddRequested;
event EventHandler EditRequested;
event EventHandler DeleteRequested;
// ... 10 more events

// ✅ Framework - One event
event EventHandler<ActionRequestEventArgs> ActionRequested;
```

---

## 🏛️ Architecture

### Three-Layer Structure

```
┌─────────────────────────────────────────────────────────────┐
│                       User Interaction                      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│    View Layer (Forms/Controls)                              │
│                                                             │
│  • Displays data to user                                    │
│  • Captures user input (button clicks, text entry)          │
│  • Implements IView interface                               │
│  • NO business logic!                                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│    Presenter Layer (Business Logic)                         │
│                                                             │
│  • Handles user actions (Save, Delete, etc.)                │
│  • Validates data                                           │
│  • Coordinates between View and Model                       │
│  • Calls Services (messages, dialogs, logging)              │
│  • Updates View with results                                │
└─────────────────────────────────────────────────────────────┘
                              │
                ┌─────────────┴──────────────┐
                ▼                            ▼
┌─────────────────────────────┐  ┌─────────────────────────────┐
│    Model Layer              │  │    Services Layer           │
│                             │  │                             │
│  • Domain models            │  │  • IMessageService          │
│  • Business entities        │  │  • IDialogProvider          │
│  • DTOs                     │  │  • IFileService             │
│  • Repositories             │  │  • ILogger                  │
└─────────────────────────────┘  └─────────────────────────────┘
```

### Component Responsibilities

```
📋 Complete Structure:

View (Form)
 ├─ Implements IView interface
 ├─ Defines events (button clicks, etc.)
 └─ Methods to update UI

Presenter
 ├─ Subscribes to View events
 ├─ Calls Model (data operations)
 ├─ Calls Services (messages, dialogs, etc.)
 ├─ Validates data
 └─ Updates View via IView interface

Model Layer
 ├─ Repository (data access)
 ├─ Domain Models (User, Order, etc.)
 └─ Business Entities / DTOs

Services Layer
 ├─ IMessageService (message boxes)
 ├─ IDialogProvider (file dialogs)
 ├─ IFileService (file operations)
 └─ ILogger (logging)
```

### Simple Flow Example

```
User clicks "Save" button
        ↓
View raises ActionRequest event
        ↓
Presenter receives event → OnSave()
        ↓
Presenter validates data
        ↓
Presenter calls Repository.Save()
        ↓
Presenter updates View: "Saved successfully!"
        ↓
User sees success message
```

---

## 🚀 Quick Start

> **📖 New to the framework? Check out our [5-Minute Quick Start Guide](QUICKSTART.md)** for a step-by-step tutorial!

### 1. Install

```bash
# Clone the repository
git clone https://github.com/pasysxa/winforms-mvp.git

# Build the solution
dotnet build src/winforms-mvp.sln
```

### 2. Define View Interface

```csharp
public interface IUserEditorView : IWindowView
{
    string UserName { get; set; }
    string Email { get; set; }
    bool HasUnsavedChanges { get; }

    ViewActionBinder ActionBinder { get; }
    event EventHandler DataChanged;
}
```

### 3. Create Presenter

```csharp
public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
{
    protected override void RegisterViewActions()
    {
        _dispatcher.Register(CommonActions.Save, OnSave,
            canExecute: () => View.HasUnsavedChanges);
        _dispatcher.Register(CommonActions.Cancel, OnCancel);

        // Framework automatically calls View.ActionBinder?.Bind(_dispatcher)
    }

    private void OnSave()
    {
        // Save logic
        Messages.ShowInfo("User saved successfully!");
    }
}
```

### 4. Implement Form

```csharp
public class UserEditorForm : Form, IUserEditorView
{
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
        _binder.Add(CommonActions.Cancel, _cancelButton);
    }

    public string UserName
    {
        get => _nameTextBox.Text;
        set => _nameTextBox.Text = value;
    }
}
```

### 5. Show Window

```csharp
var viewMappingRegister = new ViewMappingRegister();
viewMappingRegister.RegisterFromAssembly(Assembly.GetExecutingAssembly());

var navigator = new WindowNavigator(viewMappingRegister);
var presenter = new UserEditorPresenter();

navigator.ShowWindow(presenter);
```

---

## 📚 Examples

The repository includes comprehensive examples:

### 🎮 [ViewAction System](src/WinformsMVP.Samples/ViewActionExample.cs)
- Static ActionKey classes
- CanExecute predicates
- Multi-control binding
- Automatic UI state updates

### ☑️ [CheckBox Demo](src/WinformsMVP.Samples/CheckBoxDemo)
- CheckBox/RadioButton binding
- Theme selection with RadioButtons
- Settings UI pattern

### 📝 [ToDo Demo](src/WinformsMVP.Samples/ToDoDemo)
- Full CRUD operations
- State-driven CanExecute
- Change tracking
- Mock service testing

### 🧭 [Navigator Demo](src/WinformsMVP.Samples/NavigatorDemo)
- Modal/non-modal windows
- Parameterized dialogs
- Singleton windows
- Window lifecycle management

### 📊 [Bulk Binding](src/WinformsMVP.Samples/BulkBindingDemo)
- Survey/questionnaire patterns
- `AddRange()` for efficiency
- Many-to-many control binding

### 🔄 [MVP Comparison](src/WinformsMVP.Samples/MVPComparisonDemo)
- Passive View pattern
- Supervising Controller pattern
- Side-by-side comparison

### 👥 [Master-Detail Pattern](src/WinformsMVP.Samples/MasterDetailDemo) **NEW!**
- Parent-child data relationships (Customers → Orders)
- Coordinated UI updates across master and detail views
- Cascading delete with confirmation
- Real-time total calculation
- State-driven CanExecute for CRUD operations

### ✅ [Complex Validation](src/WinformsMVP.Samples/ValidationDemo) **NEW!**
- Real-time field-level validation
- Cross-field validation (password confirmation)
- Pattern matching (email, phone number)
- Business rule validation (age restrictions)
- Visual error feedback with colored fields
- Validation summary display

### ⚡ [Async Operations](src/WinformsMVP.Samples/AsyncDemo) **NEW!**
- Proper async/await patterns in Presenters
- Progress tracking with progress bar
- Cancellation support (CancellationToken)
- Error handling in async methods
- Long-running operations without UI freezing
- Multiple async operation patterns

### 📊 [Logging Demo](src/WinformsMVP.Samples/LoggingDemoExample.cs) **NEW!**
- In-house `WinformsMVP.Logging.ILogger` abstraction (M.E.L.-compatible API, zero external deps)
- Different log levels (Debug, Information, Warning, Error)
- Structured logging with parameters
- Exception logging with context
- Custom logger factory configuration
- Microsoft.Extensions.Logging bridge via an application-level adapter — see [`MultiProjectDemo.Shell/Logging/`](src/MultiProjectDemo.Shell/Logging/) for a runnable Application Insights / Seq / Serilog setup
- Testing with NullLoggerFactory

---

## 🧪 Testing

The framework is designed for testability:

```csharp
[Fact]
public void OnSave_WithValidData_ShowsSuccessMessage()
{
    // Arrange
    var mockMessages = new MockMessageService();
    var mockView = new MockUserEditorView
    {
        UserName = "John",
        HasUnsavedChanges = true
    };

    var presenter = new UserEditorPresenter(mockMessages);
    presenter.AttachView(mockView);
    presenter.Initialize();

    // Act
    presenter.OnSave();  // Accessible via ActionDispatcher

    // Assert
    Assert.True(mockMessages.InfoMessageShown);
    Assert.Contains("saved", mockMessages.LastMessage);
}
```

**Test Results**: ✅ 131/131 tests passing

---

## 📖 Documentation

- **[QUICKSTART.md](QUICKSTART.md)** - 5-minute beginner tutorial 🚀
  - Hello World example
  - User interaction basics
  - ViewAction system introduction
  - Service layer usage
  - Complete runnable code

- **[MVP-DESIGN-RULES.md](MVP-DESIGN-RULES.md)** - 14 essential design rules ⭐
  - Supervising Controller pattern principles
  - Naming conventions (XxxView, XxxPresenter)
  - Responsibility separation (use-case logic vs UI logic)
  - "Tell, Don't Ask" principle
  - Interface design best practices
  - Domain-driven naming
  - Compliance checklist

- **[CLAUDE.md](CLAUDE.md)** - Comprehensive architecture guide
  - MVP principles and patterns
  - ViewAction system deep dive
  - Service layer design
  - Navigation system
  - Change tracking
  - Best practices and anti-patterns

---

## 🛠️ Technology Stack

- **.NET Framework 4.8** - Target framework
- **C# 7.3+** - Language features
- **xUnit 2.9** - Testing framework
- **SDK-Style Projects** - Modern project format

---

## 📦 Project Structure

```
winforms-mvp/
├── src/
│   ├── WinformsMVP/                    # Core framework
│   │   ├── MVP/
│   │   │   ├── Presenters/             # Presenter base classes
│   │   │   ├── Views/                  # View interfaces
│   │   │   └── ViewActions/            # ViewAction system
│   │   ├── Services/                   # Service abstractions
│   │   └── Common/                     # Utilities (ChangeTracker, etc.)
│   │
│   ├── WinformsMVP.Samples/            # Sample applications
│   │   ├── ViewActionExample.cs        # ViewAction demo
│   │   ├── ToDoDemo/                   # Full CRUD example
│   │   ├── NavigatorDemo/              # Navigation patterns
│   │   ├── CheckBoxDemo/               # CheckBox/RadioButton
│   │   ├── BulkBindingDemo/            # Survey/questionnaire
│   │   └── MVPComparisonDemo/          # Pattern comparison
│   │
│   └── WinformsMVP.Samples.Tests/      # Unit tests
│
├── CLAUDE.md                           # Architecture documentation
└── README.md                           # This file
```

---

## 🎯 Use Cases

This framework is ideal for:

- ✅ **New WinForms Projects** - Start with clean architecture
- ✅ **Legacy Modernization** - Gradually refactor existing code
- ✅ **Enterprise Applications** - Maintainable, testable code
- ✅ **Team Standardization** - Consistent patterns across team
- ✅ **Training & Education** - Learn MVP architecture

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🌟 Show Your Support

If this framework helped you, please give it a ⭐️ on GitHub!

---

## 📬 Contact

For questions and support, please open an issue on GitHub.

---

<div align="center">

**[⬆ Back to Top](#-winforms-mvp-framework)**

Made with ❤️ for the WinForms community

</div>
