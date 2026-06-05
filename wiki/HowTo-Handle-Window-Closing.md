# HowTo: Handle Window Closing

This page shows implementation patterns for typical window-closing scenarios.
For the design rationale (why the Push/Pull two-direction model) see [Window Closing Model](Concept-Window-Closing-Model). For the `WindowNavigator` API reference see [WindowNavigator](Reference-WindowNavigator).

> **Key rule**: Forms write **zero** closing code. `IWindowView` has no closing members. All closing policy lives in the Presenter.

---

## Table of contents

- [Scenario 1: Simple OK/Cancel dialog](#scenario-1-simple-okcancel-dialog)
- [Scenario 2: Dirty-check on close](#scenario-2-dirty-check-on-close)
- [Scenario 3: Dialog that returns a business result](#scenario-3-dialog-that-returns-a-business-result)
- [Scenario 4: Parent receives the result](#scenario-4-parent-receives-the-result)
- [Scenario 5: Never block system shutdown](#scenario-5-never-block-system-shutdown)
- [Scenario 6: Async close decision](#scenario-6-async-close-decision)
- [Scenario 7: Adopted hosting (shell / legacy Form)](#scenario-7-adopted-hosting-shell--legacy-form)
- [Test patterns](#test-patterns)

---

## Scenario 1: Simple OK/Cancel dialog

A minimal dialog with no dirty-state: just push a result from the Save/Cancel handler. No `CanClose` override needed when the window should always close on X.

```csharp
public class ConfirmDeletePresenter : WindowPresenterBase<IConfirmDeleteView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Ok,     OnOk);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    private void OnOk()     => RequestClose(true,  InteractionStatus.Ok);
    private void OnCancel() => RequestClose(false, InteractionStatus.Cancel);
}

// Caller
var result = Navigator.For(new ConfirmDeletePresenter()).ShowAsModal<bool>();
if (result.IsOk && result.Value)
    DeleteItem();
```

When the user presses X the window closes with `result.IsCancelled`, requiring no special handling.

---

## Scenario 2: Dirty-check on close

Override `CanClose` to prompt when there are unsaved changes. This is the **only place** that hosts the dirty-check dialog.

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView, EditUserParameters>
{
    private ChangeTracker<UserModel> _changeTracker;

    protected override void OnViewAttached()
        => View.EditChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();

    protected override void OnInitialize(EditUserParameters parameters)
    {
        var user = LoadUser(parameters.UserId);
        _changeTracker = new ChangeTracker<UserModel>(user);
        View.Bind(_changeTracker.CurrentValue);

        _changeTracker.IsChangedChanged += (s, e) => Dispatcher.RaiseCanExecuteChanged();
    }

    protected override void RegisterViewActions()
    {
        Dispatcher.Register(StandardActions.Save, OnSave,
            canExecute: () => _changeTracker.IsChanged);
        Dispatcher.Register(StandardActions.Cancel, OnCancel);
    }

    // ── Pull direction: X / Alt+F4 ───────────────────────────────────────────
    protected override bool CanClose(CloseReason reason)
    {
        // Never block system-level shutdowns.
        if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
            return true;

        if (!_changeTracker.IsChanged) return true;

        return Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    }

    // ── Push direction: Save / Cancel buttons ────────────────────────────────
    private void OnSave()
    {
        SaveUser(_changeTracker.CurrentValue);
        _changeTracker.AcceptChanges();   // commit model state; CanClose will see clean state
        RequestClose(BuildResult(), InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();
        RequestClose(InteractionStatus.Cancel);
    }

    private UserResult BuildResult() => new UserResult { /* ... */ };
}
```

### Why there is no double-prompt

The `WindowCloseController` sets an internal suppress flag when `RequestClose` is called, so the `FormClosing` event raised by the follow-up `form.Close()` skips `CanClose` entirely. That is a structural guarantee — it does not depend on calling `AcceptChanges` before `RequestClose`.

```
OnSave()
    ├─ AcceptChanges()         ← model state only
    └─ RequestClose(...)
          ↓
    WindowCloseController.Close():  _suppressGate = true, form.Close()
          ↓
    FormClosing — _suppressGate is true → CanClose NOT called
          ↓
    FormClosed → converge → InteractionResult(Ok, result)
```

---

## Scenario 3: Dialog that returns a business result

Return any type as the result. Call `RequestClose(result, status)` — `TResult` is inferred from the argument:

```csharp
public class CustomerResult
{
    public int Id   { get; set; }
    public string Name { get; set; }
}

public class EditCustomerPresenter : WindowPresenterBase<IEditCustomerView>
{
    private int _customerId;

    private void OnSave()
    {
        var result = new CustomerResult { Id = _customerId, Name = View.CustomerName };
        RequestClose(result, InteractionStatus.Ok);
    }

    private void OnCancel()
        => RequestClose(InteractionStatus.Cancel);
}
```

---

## Scenario 4: Parent receives the result

The caller uses `Navigator.For(...).ShowAsModal<TResult>()` and reads `InteractionResult<TResult>`. It never needs to know whether Push or Pull caused the close:

```csharp
public class CustomerListPresenter : WindowPresenterBase<ICustomerListView>
{
    private void OnEditCustomer()
    {
        var presenter = new EditCustomerPresenter();
        var param     = new EditCustomerParameters { CustomerId = View.SelectedCustomerId };

        var result = Navigator.For(presenter)
                              .WithParam(param)
                              .ShowAsModal<CustomerResult>();

        if (result.IsOk)
            ReloadCustomer(result.Value.Id);
        else if (result.IsError)
            Messages.ShowError(result.ErrorMessage, "Error");
        // result.IsCancelled: user cancelled or pressed X → no action needed
    }
}
```

---

## Scenario 5: Never block system shutdown

Windows cannot shut down if a modal dialog is open. Always allow `SystemShutdown` and `TaskManager` immediately:

```csharp
protected override bool CanClose(CloseReason reason)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
        return true;   // must not block

    if (reason == CloseReason.ParentClosing)
        return true;   // usually allow owner-propagated close

    // Only Normal (X / Alt+F4) gets the dirty-check prompt.
    if (_changeTracker.IsChanged && !Messages.ConfirmYesNo("Discard changes?", "Confirm"))
        return false;

    return true;
}
```

---

## Scenario 6: Async close decision

When the close decision requires a server call or continuation, override the two-argument form of `CanClose`. Use `Action<bool>` — not `Task` — so it compiles for net40:

```csharp
protected override void CanClose(CloseReason reason, Action<bool> proceed)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
    {
        proceed(true);
        return;
    }

    if (!_changeTracker.IsChanged)
    {
        proceed(true);
        return;
    }

    // Ask the server whether there are pending locks before closing.
    _repository.HasActiveLockAsync(_userId, hasLock =>
    {
        if (hasLock)
        {
            Messages.ShowWarning("Another user has a lock. Cannot close now.", "Warning");
            proceed(false);
        }
        else
        {
            proceed(Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm"));
        }
    });
    // proceed will be called from the continuation — return without calling it now.
}
```

The framework detects that `proceed` was not called synchronously and suppresses the close; when `proceed(true)` arrives from the continuation it triggers a re-close with the gate bypassed.

---

## Scenario 7: Adopted hosting (shell / legacy Form)

For a Form you create and show yourself (application shell, `Application.Run`, legacy migration) call `presenter.Connect(form)`. The Presenter gets attach + initialize + close wiring; you own the `Show`:

```csharp
// No-result adoption (shell window)
public static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        PlatformServices.Default = new DefaultPlatformServices(/* ... */);

        var form      = new MainShellForm();
        var presenter = new MainShellPresenter();
        presenter.Connect(form);      // attach + initialize + wire close controller

        Application.Run(form);        // you own Show
    }
}

// Typed result callback
var form      = new SettingsForm();
var presenter = new SettingsPresenter();
presenter.Connect<ISettingsView, SettingsResult>(form, result =>
{
    if (result.IsOk) ApplySettings(result.Value);
});
form.ShowDialog();

// Parameterized adoption
var form      = new EditItemForm();
var presenter = new EditItemPresenter();
presenter.Connect(form, new EditItemParameters { ItemId = 42 });
form.Show();
```

**Single-owner rule**: a Form connected this way must NOT also be shown through `WindowNavigator`. Choose one hosting mode per Form.

---

## Test patterns

### Pull direction — test `CanClose`

Reach the Pull gate via `ICloseParticipant.CanCloseGate` (internal; accessible from test projects via `InternalsVisibleTo`):

```csharp
[Fact]
public void CanClose_WithUnsavedChanges_UserDeclines_BlocksClose()
{
    _view.SimulateEdit("new value");
    _platform.MessageService.ConfirmYesNoResult = false;   // user keeps editing

    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);

    Assert.False(allow);
    Assert.True(_platform.MessageService.ConfirmDialogShown);
}

[Fact]
public void CanClose_SystemShutdown_DoesNotPrompt()
{
    _view.SimulateEdit("new value");

    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.SystemShutdown, ok => allow = ok);

    Assert.True(allow);
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

### Push direction — test `RequestClose`

Bind a recording `ICloseSink` before the test and dispatch an action:

```csharp
private sealed class RecordingSink : ICloseSink
{
    public readonly List<(object result, InteractionStatus status)> Closed
        = new List<(object, InteractionStatus)>();
    public void Close(object result, InteractionStatus status)
        => Closed.Add((result, status));
}

[Fact]
public void Save_PushesResultWithOkStatus()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _view.SimulateEdit("hello");
    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Save);

    Assert.Single(sink.Closed);
    Assert.Equal("hello", sink.Closed[0].result);
    Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
}

[Fact]
public void Cancel_PushesCancelStatus()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Cancel);

    Assert.Equal(InteractionStatus.Cancel, sink.Closed[0].status);
}
```

### Single-source-of-truth invariant test

Verify that after a Push (Save), the follow-up `CanClose` call does not re-prompt:

```csharp
[Fact]
public void Save_ThenCanClose_DoesNotPrompt()
{
    var sink = new RecordingSink();
    ((ICloseParticipant)_presenter).BindCloseSink(sink);

    _view.SimulateEdit("hello");
    _presenter.Dispatcher.Dispatch(WindowClosingDemoActions.Save);

    // Simulate the framework re-running the Pull gate after the Push close.
    bool? allow = null;
    ((ICloseParticipant)_presenter).CanCloseGate(CloseReason.Normal, ok => allow = ok);

    Assert.True(allow);
    Assert.False(_platform.MessageService.ConfirmDialogShown);
}
```

Complete test examples: `tests/WinformsMVP.Samples.Tests/Presenters/CanCloseTests.cs` and `WindowClosingDemoPresenterTests.cs`.
Full test-driving guide: [HowTo: Test a Presenter](HowTo-Test-A-Presenter).

---

## Related pages

- [Window Closing Model](Concept-Window-Closing-Model) — design rationale and internals
- [WindowNavigator](Reference-WindowNavigator) — Navigator API, Fluent form, `InteractionResult<T>`
- [ChangeTracker](Reference-ChangeTracker) — dirty-state tracking
- [Presenter Base Classes](Reference-Presenter-Base-Classes) — `CanClose`, `RequestClose`, and `Connect` in context
- [HowTo: Test a Presenter](HowTo-Test-A-Presenter) — full testing patterns
