# Window Closing Model

This page explains the design of the framework's **two-direction window closing model**. For concrete code samples see [HowTo: Handle Window Closing](HowTo-Handle-Window-Closing). For the `WindowNavigator` API reference see [WindowNavigator](Reference-WindowNavigator).

> **Understanding this model is essential for implementing dirty-checks, save-on-close, and cancel behavior correctly.**

---

## Why a dedicated model?

WinForms `Form.FormClosing` aggregates every close trigger — user's X button, system shutdown, task-manager kill, owner-window propagation, and code-driven `Close()` — into one event. That event carries `FormClosingEventArgs`, a WinForms-namespace type, so any Presenter that handles it directly violates two of the [three iron rules of MVP](Concept-MVP-Pattern#3-iron-rules).

This framework resolves those problems by splitting the close responsibility into **two independent directions** and keeping all WinForms details inside one internal component.

---

## Mental model: Push and Pull

| Direction | Initiator | Mechanism | Typical trigger |
|-----------|-----------|-----------|----------------|
| **Pull**  | Framework | `protected virtual bool CanClose(CloseReason reason)` override | X / Alt+F4 / shutdown |
| **Push**  | Presenter | `this.RequestClose(result, status)` extension | user clicked Save / Cancel / OK |

Both paths converge on WinForms `FormClosed`. The `ShowWindowAsModal` caller always receives the same `InteractionResult<TResult>`, regardless of which direction caused the close. The mapping from WinForms `FormCloseReason` to the framework's `CloseReason` happens exactly once, inside `CloseReasonMap`, and never leaks to Presenters.

### Push direction — Presenter actively closes

```
User clicks Save
    ↓
OnSave() in Presenter
    ├─ commits dirty flag (AcceptChanges) — for model correctness, not to skip the gate
    └─ this.RequestClose(result, InteractionStatus.Ok)
          ↓
    WindowCloseController (ICloseSink.Close)
          ├─ sets _suppressGate = true  ← skips the Pull gate once
          └─ form.Close()
                ↓
          FormClosing fires
                └─ _suppressGate is true → gate skipped, no dirty prompt
                      ↓
          FormClosed → Converge → onClosed callback → InteractionResult(Ok, result)
```

### Pull direction — external trigger

```
User clicks X (or Alt+F4, system shutdown …)
    ↓
FormClosing fires
    ↓
WindowCloseController.OnFormClosing
    ├─ _suppressGate is false → calls ICloseParticipant.CanCloseGate(reason, proceed)
    │       ↓
    │   Presenter.CanClose(reason, proceed)   [your override]
    │       ├─ return false → e.Cancel = true  (window stays open)
    │       └─ return true  → e.Cancel = false
    └─ (async path: proceed(true) from continuation → re-close with _suppressGate=true)
          ↓
    FormClosed → Converge → InteractionResult(Cancel) to caller
```

---

## Single-source-of-truth invariant

> **Dirty-state prompts live ONLY in `CanClose` (Pull direction).**

Push handlers finalize the dirty flag (`AcceptChanges` / `RejectChanges`) **before** calling `RequestClose`, so the framework's follow-up `CanClose` call sees clean state and does not re-prompt. The double-prompt prevention is structural — it does not depend on the order of `AcceptChanges` relative to `RequestClose`.

Design gains:
- Dirty-check logic is concentrated in one place
- Push-direction close cannot accidentally trigger a second dirty-check dialog
- Both directions can be tested independently and in isolation
- Callers only see `InteractionResult<TResult>`; they never care which direction caused the close

---

## Pull gate — `CanClose`

Override `CanClose(CloseReason reason)` in your Presenter to veto a close. Return `false` to block, `true` to allow:

```csharp
protected override bool CanClose(CloseReason reason)
{
    // Never block system-level shutdowns — a modal dialog here can freeze application exit.
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
        return true;

    if (!_changeTracker.IsChanged) return true;

    bool discard = Messages.ConfirmYesNo("Discard unsaved changes?", "Confirm");
    return discard;
}
```

### Async Pull gate

When the close decision needs a callback (async save, server round-trip), override the two-argument form. Call `proceed(true)` to allow or `proceed(false)` to block — from inside a continuation if needed. `Action<bool>` is used instead of `Task` to remain net40-safe:

```csharp
protected override void CanClose(CloseReason reason, Action<bool> proceed)
{
    if (reason == CloseReason.SystemShutdown || reason == CloseReason.TaskManager)
    {
        proceed(true);
        return;
    }

    // Default one-argument overload chains through here:
    // proceed(CanClose(reason));

    // Async example — check with a server before closing:
    CheckServerAsync(ok => proceed(ok));
}
```

The framework's internal async handling: if `proceed` is called synchronously (before `CanCloseGate` returns), the answer is applied directly via `e.Cancel`. If it is called from a continuation (after `CanCloseGate` returns), a `proceed(true)` triggers a re-close with the gate suppressed; `proceed(false)` leaves the window open with no action needed.

---

## Push direction — `RequestClose`

Implement the marker interface `IRequestClose<TResult>` (no members — it only declares the result type) and call `this.RequestClose(result, status)`:

```csharp
public class EditUserPresenter : WindowPresenterBase<IEditUserView>,
                                  IRequestClose<UserResult>
{
    private void OnSave()
    {
        var result = new UserResult { Name = View.UserName };
        _changeTracker.AcceptChanges();   // commit model state
        this.RequestClose(result, InteractionStatus.Ok);
    }

    private void OnCancel()
    {
        _changeTracker.RejectChanges();
        this.RequestClose(null, InteractionStatus.Cancel);
    }
}
```

For a no-result close, the base provides a protected helper that does not require `IRequestClose<TResult>`:

```csharp
protected void RequestClose(InteractionStatus status = InteractionStatus.Ok)
```

---

## `CloseReason` enum

The framework uses its own `CloseReason` instead of `System.Windows.Forms.CloseReason` to keep WinForms types out of View interfaces and Presenters entirely.

```csharp
public enum CloseReason
{
    Normal,          // X / Alt+F4 — inspect dirty state here
    SystemShutdown,  // Windows shutting down — never block
    TaskManager,     // Force-kill — never block
    ParentClosing,   // Owner window closing — usually allow
    Unknown,
}
```

The mapping from `FormCloseReason` to this enum happens once in `CloseReasonMap` (internal). That is the only place in the framework that knows WinForms close reasons.

---

## Hosting — Managed vs Adopted

**Single-owner rule**: use exactly one of the two modes for any given Form. Never mix them.

### Managed hosting (default for MVP-native windows)

`WindowNavigator` creates the Form, wires the close controller, and shows it. This is the correct path for all new windows:

```csharp
// Modal — returns InteractionResult<TResult>
var result = Navigator.For(presenter).WithParam(parameters).ShowAsModal<UserResult>();

// Non-modal — returns immediately
Navigator.For(presenter).ShowWindow();
```

### Adopted hosting (shell window, legacy migration)

For a Form you create and `Show` / `Application.Run` yourself, call `presenter.Connect(form)`. This does attach + initialize + close wiring idempotently, without showing the form:

```csharp
// No-result adoption
presenter.Connect(form);

// Typed callback on close
presenter.Connect<IMyView, bool>(form, result =>
{
    if (result.IsOk) DoSomethingWith(result.Value);
});

// Parameterized presenter adoption
presenter.Connect(form, parameters);
```

After `Connect`, you own the `Show` / `Application.Run`. The Presenter owns the close wiring.

---

## `IRequestClose<TResult>` as a marker

`IRequestClose<TResult>` has **no members**. It only declares the result type so that:

1. `this.RequestClose(result, status)` is compile-time typed to `TResult`.
2. `WindowNavigator.ShowWindowAsModal<TPresenter, TResult>` can infer and enforce the result type at the call site.

There is no `CloseRequested` event. There is no `RaiseClose` helper to write. The framework injects the close sink automatically.

---

## Internal bridge — `WindowCloseController`

One `WindowCloseController` instance is created per window. It:

- Implements `ICloseSink` (the Push sink): records the pending result and status, sets the suppress flag, calls `form.Close()`.
- Bridges `FormClosing` (the Pull gate): calls `ICloseParticipant.CanCloseGate`, applies the `e.Cancel` decision.
- Handles the close-before-show edge case via `CloseRequestedBeforeShow` + `ConvergeWithoutShow`.
- Converges on `FormClosed`: invokes the `onClosed` callback and disposes the Presenter (and, for Managed modal / Adopted, the Form).

This is the only component in the framework that references `Form` directly.

---

## Caller's view

The caller (parent Presenter) sees only `InteractionResult<TResult>` and never needs to know which direction caused the close:

```csharp
private void OnEditUser()
{
    var result = Navigator.For(new EditUserPresenter())
                          .WithParam(new EditUserParameters { UserId = _selectedId })
                          .ShowAsModal<UserResult>();

    if (result.IsOk)
        ReloadUser(result.Value.UserId);
    // result.IsCancelled: user pressed X and discarded, or clicked Cancel — no action needed
}
```

---

## Summary

| Concern | Guarantee |
|---------|-----------|
| Presenter never sees WinForms types | `CloseReason` is a framework enum; `FormClosingEventArgs` never leaks |
| Dirty-check centralized in one place | Only `CanClose` contains the prompt |
| Push close cannot re-trigger the dirty prompt | `_suppressGate` flag is structural, not a calling-order convention |
| Forms write zero closing code | `IWindowView` has no closing members |
| Both directions testable independently | Pull via `ICloseParticipant.CanCloseGate`; Push via a bound `ICloseSink` recording fake |

---

## Next steps

| Goal | Page |
|------|------|
| Concrete code samples for all scenarios | [HowTo: Handle Window Closing](HowTo-Handle-Window-Closing) |
| Full `WindowNavigator` API | [WindowNavigator](Reference-WindowNavigator) |
| Dirty-state tracking | [ChangeTracker](Reference-ChangeTracker) |
| Testing patterns | [HowTo: Test a Presenter](HowTo-Test-A-Presenter) |
