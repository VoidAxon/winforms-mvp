# Design: service-resolution foundation — replace `PlatformServices` with `ServiceLocator` over `IServiceProvider`

Date: 2026-06-11
Status: Draft (for review)
Branch: `feature/service-locator`

## Problem

`IPlatformServices` / `PlatformServices` is a **typed service-locator god-object**: every
cross-cutting service is a named property on the interface (`MessageService`,
`DialogProvider`, `FileService`, `WindowNavigator`, `LoggerFactory`, ...). Adding any new
framework service (e.g. an anchored-feedback service) means **growing the interface** — which
forces every implementer (incl. the test `MockPlatformServices`) to change, and produces
awkward debates ("is this a second message service?", "where does this property go?").

This surfaced while adding an anchored-toast capability: there was no clean, testable seam to
register it through — only "add another property to `IPlatformServices`."

## Goal

Replace the typed god-object with a minimal **service-resolution seam** so that:

- Adding a service is **registration**, never an interface change.
- It is **net40-safe** and carries **zero external dependencies** in the core.
- It **connects to a real DI container** (the existing M.E.DI bridge) with no adapter.
- It supports **modular registration without any external DI**.
- Presenter ergonomics (`Messages`, `Dialogs`, ...) and call sites stay intact.

This is the Prism `ContainerLocator` role: a static ambient locator over a container, used by
the framework internals and the convenience accessors; **business services still use
constructor injection** (the documented Hybrid pattern), not the locator.

## Design

### Two faces: resolve (BCL) + register (ours)

```csharp
// Resolution = the BCL interface (net40-safe since .NET 1.1). Real DI containers already implement it.
// System.IServiceProvider: object GetService(Type serviceType);

// Registration = a minimal interface of our own (net40-safe).
public interface IServiceRegistry
{
    void RegisterInstance<TService>(TService instance);
    void RegisterFactory<TService>(Func<IServiceProvider, TService> factory); // lazy/singleton-per-resolve
    bool IsRegistered(Type serviceType);
}

// The built-in implementation is both: register into it, resolve from it.
public sealed class ServiceRegistry : IServiceRegistry, IServiceProvider { /* dictionary-backed */ }
```

A small generic convenience over the BCL non-generic resolve:

```csharp
public static class ServiceProviderExtensions
{
    public static T GetService<T>(this IServiceProvider p) => (T)p.GetService(typeof(T));
    public static T GetRequiredService<T>(this IServiceProvider p)
        => (T)(p.GetService(typeof(T)) ?? throw new InvalidOperationException($"No service for {typeof(T)}."));
}
```

### The static locator (replaces `PlatformServices.Default`)

```csharp
public static class ServiceLocator
{
    private static IServiceProvider _current;

    /// <summary>The ambient provider. Defaults to a registry with the framework's built-ins.
    /// App startup / the M.E.DI bridge / tests assign this.</summary>
    public static IServiceProvider Current
    {
        get => _current ?? (_current = BuildDefault());
        set => _current = value;
    }

    /// <summary>Configure the default built-in registry (no external DI).</summary>
    public static void Configure(Action<IServiceRegistry> register)
    {
        var reg = new ServiceRegistry();
        RegisterBuiltIns(reg);
        register?.Invoke(reg);
        _current = reg;
    }

    private static IServiceProvider BuildDefault()
    {
        var reg = new ServiceRegistry();
        RegisterBuiltIns(reg);
        return reg;
    }

    private static void RegisterBuiltIns(IServiceRegistry reg)
    {
        reg.RegisterInstance<IMessageService>(new MessageService());
        reg.RegisterInstance<IDialogProvider>(new DialogProvider());
        reg.RegisterInstance<IFileService>(new FileService());
        reg.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
        // new framework services register here — no interface to touch
        reg.RegisterInstance<IAnchoredFeedback>(new AnchoredFeedback());
    }
}
```

### `PresenterBase` depends on `IServiceProvider`, not `IPlatformServices`

```csharp
private IServiceProvider _provider;

/// <summary>Override the provider for this presenter (tests / scoped composition).
/// Must be called before AttachView/Initialize.</summary>
internal void SetServiceProvider(IServiceProvider provider) { /* guard, assign */ }

private IServiceProvider Services => _provider ?? ServiceLocator.Current;

// Convenience accessors — call sites unchanged, now resolve through the provider.
protected IMessageService  Messages  => Services.GetService<IMessageService>();
protected IDialogProvider   Dialogs   => Services.GetService<IDialogProvider>();
protected IFileService      Files     => Services.GetService<IFileService>();
protected IWindowNavigator  Navigator => Services.GetService<IWindowNavigator>();
// new services need no new property; resolve directly:
protected T Service<T>() => Services.GetService<T>();
```

### Modules — work without external DI

```csharp
/// <summary>A unit of registration owned by a UI module. Registers into the core registry,
/// so modular composition works with no external DI.</summary>
public interface IServiceModule
{
    void RegisterServices(IServiceRegistry registry);
}
```

App startup collects modules and registers them into the built-in registry:

```csharp
ServiceLocator.Configure(reg =>
{
    new BillingModule().RegisterServices(reg);
    new ReportingModule().RegisterServices(reg);
});
```

When M.E.DI **is** used, the same `IServiceModule` registrations are routed into M.E.DI's
`IServiceCollection` by the bridge; the module concept is identical, only the backing differs.

### External DI — zero-adapter connection

A real DI container is already an `IServiceProvider`:

```csharp
// In Program.cs / app startup, when using Microsoft.Extensions.DependencyInjection:
var provider = services.BuildServiceProvider();
ServiceLocator.Current = provider;     // framework now resolves everything through M.E.DI
```

The `WinformsMVP.DependencyInjection` package keeps `AddWinformsMVP(...)` (registers the
framework's services into the M.E.DI `IServiceCollection`) and `IPresenterFactory`
(constructor injection of presenters). Its `IModuleRegistrar` collapses onto the core
`IServiceModule`; the package only bridges to M.E.DI.

### Discipline (Prism-aligned)

- The locator backs **framework internals + the convenience accessors** only.
- **Business services use constructor injection** (Hybrid pattern), not `ServiceLocator` /
  `Service<T>()`. Service-locating business dependencies everywhere is the anti-pattern we are
  explicitly *not* endorsing.

## Deletions (hard, no `[Obsolete]` transition — preview stage)

- `IPlatformServices` (interface)
- `PlatformServices` (static) / `DefaultPlatformServices` (concrete)
- `MockPlatformServices` (tests) — replaced by `new ServiceRegistry()` + `RegisterInstance` of mocks
- `PresenterBase.Platform` property, `SetPlatformServices(IPlatformServices)`

## Open items to settle during implementation

1. **`ConfigureDispatcher`** — today an `Action<ViewActionDispatcher>` on `IPlatformServices`. It
   is configuration, not a resolvable service. Options: register an `IDispatcherConfigurer`
   service resolved by the framework, or keep a dedicated config hook on `ServiceLocator`.
   Decision: register `IDispatcherConfigurer` (uniform), resolved in `EnsureDispatcherConfigured`.
2. **`LoggerFactory`** — a service; resolve `ILoggerFactory` via the provider (no special case).
3. **Lifetimes** — the built-in registry supports instance + factory only (no scopes). Scopes /
   per-resolve graphs are the real DI container's job (M.E.DI), not the net40 fallback's.
4. **Thread-safety** — `ServiceLocator.Current` and `ServiceRegistry` reads happen on the UI
   thread in practice; registration happens at startup. Guard registration-after-resolve.

## Impact / migration

- **Core**: new `ServiceRegistry`, `IServiceRegistry`, `IServiceModule`, `ServiceLocator`,
  `ServiceProviderExtensions`; `PresenterBase` reworked to `IServiceProvider`.
- **Samples**: any `PlatformServices` / `SetPlatformServices` usage migrates to `ServiceLocator`
  / `SetServiceProvider`.
- **Tests**: `MockPlatformServices` removed; tests build a `ServiceRegistry` with mocks and call
  `SetServiceProvider`. `MockMessageService` etc. stay (they implement the service interfaces).
- **`WinformsMVP.DependencyInjection`**: `AddWinformsMVP` registers into M.E.DI as before;
  `ServiceLocator.Current` set to the M.E.DI provider; `IModuleRegistrar` reconciled with the
  core `IServiceModule`.
- **Docs**: the DI section of `CLAUDE.md` + the DI wiki page updated (Service Locator pattern now
  = `ServiceLocator`/`IServiceProvider`).

## Follow-on: the anchored-toast feature (paused, lands on this foundation)

After the foundation, the anchored-feedback feature is simply:

- `IAnchoredFeedback` (toast + later anchored message box), registered as a built-in.
- Cursor-anchored (`Cursor.Position` read in the real impl at call time); decoupled from
  ViewAction/ActionBinder.
- The `ViewActionBinder` trigger-capture, `IActionTriggerSource`, and
  `PresenterBase.ShowToastFor(action, ...)` from the earlier exploration are **dropped**.
- The `ToastNotification.ShowAnchored` multi-monitor fix (use `Screen.FromPoint(anchor)`) is an
  independent bug fix to carry over from the `feature/show-toast-for` WIP commit.
