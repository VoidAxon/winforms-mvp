# Archived Documentation

This directory contains documents that are no longer part of the active documentation set. They are kept for historical reference only — current documentation lives in the GitHub Wiki and the `README.md` at the repository root.

## One-off Audit / Optimization Reports

These were generated once and reflect the state of the project at a specific point in time. They are not maintained going forward.

- `MVP-COMPLIANCE-REPORT.md` / `mvp-compliance-report.json` — initial MVP compliance audit results
- `OPTIMIZATION-SUMMARY.md` — one-time framework optimization summary
- `nul-file` — accidental Windows reserved-name file (kept for completeness, contains no useful content)

## BindActions Pattern (Archived)

The following documents describe the obsolete `BindActions()` method pattern:

- `BindActions-Caller-Analysis.md`
- `BindActions-Final-Analysis.md`
- `BindActions-Location-Analysis.md`
- `BindActions-Reality-Check.md`

### Why Archived?

The `BindActions(ViewActionDispatcher dispatcher)` method pattern has been replaced by the **ActionBinder Property Pattern**.

**Old Pattern (Deprecated):**
```csharp
public interface IMyView : IWindowView
{
    void BindActions(ViewActionDispatcher dispatcher);  // ❌ Obsolete
}

public class MyPresenter : WindowPresenterBase<IMyView>
{
    protected override void OnViewAttached()
    {
        View.BindActions(Dispatcher);  // ❌ Manual binding
    }
}
```

**New Pattern (Current):**
```csharp
public interface IMyView : IWindowView
{
    ViewActionBinder ActionBinder { get; }  // ✅ Property pattern
}

public class MyPresenter : WindowPresenterBase<IMyView>
{
    protected override void RegisterViewActions()
    {
        Dispatcher.Register(MyActions.Save, OnSave);
        // Framework automatically calls View.ActionBinder?.Bind(_dispatcher)
    }
}
```

### Benefits of New Pattern

1. **Automatic Binding** - Framework handles binding lifecycle
2. **Property instead of Method** - Prevents accidental code execution in tests
3. **Cleaner Architecture** - Less boilerplate in presenters
4. **Consistent with WPF** - Similar to ICommand pattern

For current documentation, see:
- `CLAUDE.md` - Main project guide
- `wiki/MVP-Design-Rules.md` - MVP design principles
- Sample code in `src/WinformsMVP.Samples/`
