# Platform → ServiceLocator Migration Implementation Plan (Phase 2 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the `IPlatformServices`/`PlatformServices` typed god-object. `PresenterBase` resolves framework services through the Phase-1 `IServiceProvider`/`ServiceLocator`; the built-in services (message/dialog/file/logger/navigator) are registered into the `ServiceLocator` default; `ConfigureDispatcher` is rehomed as `IDispatcherConfigurer`; the M.E.DI bridge unifies onto `ServiceLocator.Current`; all samples/tests migrate; the old types are deleted.

**Architecture:** Build on Phase 1 (`docs/superpowers/plans/2026-06-11-service-resolution-primitives.md`, already merged on this branch). Order tasks so the build/tests stay green at every commit: (1) add built-ins + `IDispatcherConfigurer` (additive), (2) flip `PresenterBase` + all callers atomically, (3) delete dead types, (4) unify the M.E.DI bridge, (5) docs, (6) gate.

**Tech Stack:** .NET Framework (net40;net48), C#, xUnit 2.9.3. Core stays net40-safe; the M.E.DI bridge package targets net48.

**Spec:** `docs/superpowers/specs/2026-06-11-service-locator-foundation-design.md`

**Pre-read for implementers (current state):**
- `src/WinformsMVP/MVP/Presenters/PresenterBase.cs` — `Platform`, `SetPlatformServices`, `Messages`/`Dialogs`/`Files`/`Navigator`/`Logger`, `EnsureDispatcherConfigured` (uses `Platform.ConfigureDispatcher`).
- `src/WinformsMVP/Services/Implementations/DefaultPlatformServices.cs` — shows how the navigator is built from an `IViewMappingRegister` (+ optional `IServiceProvider` for view-resolution fallback) and how `ILoggerFactory`/`ConfigureDispatcher` default.
- `src/WinformsMVP/Services/ServiceLocator.cs`, `DefaultServiceProvider.cs`, `IServiceRegistry.cs`, `ServiceProviderExtensions.cs` (`Resolve<T>`/`ResolveRequired<T>`) — Phase-1 primitives.

---

### Task 1: `IDispatcherConfigurer` + register built-in services into the `ServiceLocator` default

**Files:**
- Create: `src/WinformsMVP/Services/IDispatcherConfigurer.cs`
- Modify: `src/WinformsMVP/Services/ServiceLocator.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorBuiltInsTests.cs`

This task is additive: nothing consumes the built-ins yet, so the suite stays green. (Phase-1 `ServiceLocatorTests` use a private `IFoo` that is never a built-in, so they keep passing.)

- [ ] **Step 1: Write `IDispatcherConfigurer`**

```csharp
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Optional global hook applied to every presenter's <see cref="ViewActionDispatcher"/> on
    /// first access. Register one in the service provider to install application-wide middleware
    /// (audit, authorization, telemetry) that must run for every dispatch in every presenter.
    /// When no <c>IDispatcherConfigurer</c> is registered, no global configuration is applied.
    /// This replaces the former <c>IPlatformServices.ConfigureDispatcher</c> callback.
    /// </summary>
    public interface IDispatcherConfigurer
    {
        void Configure(ViewActionDispatcher dispatcher);
    }
}
```

- [ ] **Step 2: Add built-in registration to `ServiceLocator`**

Add a `RegisterBuiltIns` step so the default provider (and `Configure`) seed the framework services. Replace the Phase-1 `BuildDefault` / `Configure` bodies:

```csharp
// using directives to add:
using WinformsMVP.Logging;
using WinformsMVP.Services.Implementations;

// inside ServiceLocator:

/// <summary>
/// Registers the framework's built-in services into <paramref name="registry"/> with
/// <c>TryAdd</c>-like "first registration wins" intent: call this first, then let callers
/// override. IWindowNavigator is built lazily from the registered IViewMappingRegister and uses
/// the same provider for view-resolution fallback.
/// </summary>
private static void RegisterBuiltIns(DefaultServiceProvider provider)
{
    provider.RegisterInstance<IViewMappingRegister>(new ViewMappingRegister());
    provider.RegisterInstance<IMessageService>(new MessageService());
    provider.RegisterInstance<IDialogProvider>(new DialogProvider());
    provider.RegisterInstance<IFileService>(new FileService());
    provider.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
    provider.RegisterFactory<IWindowNavigator>(sp =>
        new WindowNavigator(sp.Resolve<IViewMappingRegister>().WithServiceProvider(sp)));
    // IDispatcherConfigurer is intentionally NOT registered by default (null => no global config).
}
```

And change the default builder + `Configure` to seed built-ins:

```csharp
public static void Configure(Action<IServiceRegistry> register)
{
    var provider = new DefaultServiceProvider();
    RegisterBuiltIns(provider);
    register?.Invoke(provider);   // caller adds or overrides (last-wins)
    Current = provider;
}

// the lazy default in the Current getter:
if (_current == null)
{
    var provider = new DefaultServiceProvider();
    RegisterBuiltIns(provider);
    _current = provider;
}
```

> Note: `WindowNavigator`, `ViewMappingRegister`, `MessageService`, `DialogProvider`, `FileService` live in `WinformsMVP.Services.Implementations`; `WithServiceProvider` is the existing extension on `IViewMappingRegister`. Confirm the exact `WithServiceProvider` signature in `DefaultPlatformServices.cs`.

- [ ] **Step 3: Write the failing test**

```csharp
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    [Collection("ServiceLocator")]
    public class ServiceLocatorBuiltInsTests : System.IDisposable
    {
        public ServiceLocatorBuiltInsTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void Default_ResolvesBuiltInServices()
        {
            var sp = ServiceLocator.Current;
            Assert.NotNull(sp.Resolve<IMessageService>());
            Assert.NotNull(sp.Resolve<IDialogProvider>());
            Assert.NotNull(sp.Resolve<IFileService>());
            Assert.NotNull(sp.Resolve<ILoggerFactory>());
            Assert.NotNull(sp.Resolve<IWindowNavigator>());
            Assert.NotNull(sp.Resolve<IViewMappingRegister>());
        }

        [Fact]
        public void Default_HasNoDispatcherConfigurer()
        {
            Assert.Null(ServiceLocator.Current.Resolve<IDispatcherConfigurer>());
        }

        [Fact]
        public void Configure_KeepsBuiltIns_AndAllowsOverride()
        {
            var custom = new MockMessageService(); // from tests/.../Mocks
            ServiceLocator.Configure(reg => reg.RegisterInstance<IMessageService>(custom));
            Assert.Same(custom, ServiceLocator.Current.Resolve<IMessageService>());
            Assert.NotNull(ServiceLocator.Current.Resolve<IWindowNavigator>()); // built-in still present
        }
    }
}
```

(Uses `WinformsMVP.Services` namespace for the service interfaces; add `using WinformsMVP.Logging;` for `ILoggerFactory` and a `using` for `MockMessageService`'s namespace `WinformsMVP.Samples.Tests.Mocks`.)

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ServiceLocator"`
Expected: PASS (Phase-1 `ServiceLocatorTests` + new `ServiceLocatorBuiltInsTests`).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/IDispatcherConfigurer.cs src/WinformsMVP/Services/ServiceLocator.cs tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorBuiltInsTests.cs
git commit -m "feat(di): register framework built-ins in ServiceLocator default; add IDispatcherConfigurer"
```

---

### Task 2: Flip `PresenterBase` to `IServiceProvider` and migrate all callers (atomic)

This is the API-changing task. `SetPlatformServices(IPlatformServices)` → `SetServiceProvider(IServiceProvider)` ripples to the test helper, the mock fixture, presenter tests, and samples — they must change together to keep the build green. Do it all in one commit.

**Files:**
- Modify: `src/WinformsMVP/MVP/Presenters/PresenterBase.cs`
- Modify: `tests/WinformsMVP.Samples.Tests/TestHelpers/PresenterPlatformExtensions.cs`
- Replace: `tests/WinformsMVP.Samples.Tests/Mocks/MockPlatformServices.cs` → a provider-building fixture
- Modify (callers): `tests/WinformsMVP.Samples.Tests/Presenters/ComposeEmailPresenterTests.cs`, `MainEmailPresenterTests.cs`, `ToDoDemoPresenterTests.cs`, `WindowClosingDemoPresenterTests.cs`, `tests/WinformsMVP.Samples.Tests/LoggingIntegrationTests.cs`, `tests/WinformsMVP.Samples.Tests/ViewActions/PresenterMiddlewareIntegrationTests.cs`, and any sample in `samples/` that calls `PlatformServices.Default`/`SetPlatformServices`/`ConfigureDispatcher` (`SampleLauncherForm.cs`, `LoggingDemoExample.cs`, `ViewActionMiddlewareExample.cs`, `MultiProjectDemo.Shell/Program.cs`, `MicrosoftLoggerFactoryAdapter.cs`).

- [ ] **Step 1: Rewrite `PresenterBase` service access**

Replace the platform field/property/setter and the convenience accessors:

```csharp
// field
private IServiceProvider _serviceProvider;

/// <summary>
/// Overrides the service provider for this presenter (tests / scoped composition).
/// MUST be called before AttachView() or Initialize().
/// </summary>
internal void SetServiceProvider(IServiceProvider serviceProvider)
{
    if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));
    if (_initialized)
        throw new InvalidOperationException(
            "Cannot set the service provider after the presenter has been initialized. " +
            "Call SetServiceProvider() before AttachView() or Initialize().");
    _serviceProvider = serviceProvider;
}

/// <summary>The service provider this presenter resolves framework services from.
/// Defaults to <see cref="WinformsMVP.Services.ServiceLocator.Current"/>.</summary>
protected IServiceProvider Services =>
    _serviceProvider ?? (_serviceProvider = WinformsMVP.Services.ServiceLocator.Current);

protected WinformsMVP.Services.IMessageService  Messages  => Services.ResolveRequired<WinformsMVP.Services.IMessageService>();
protected WinformsMVP.Services.IDialogProvider   Dialogs   => Services.ResolveRequired<WinformsMVP.Services.IDialogProvider>();
protected WinformsMVP.Services.IFileService      Files     => Services.ResolveRequired<WinformsMVP.Services.IFileService>();
protected WinformsMVP.Services.IWindowNavigator  Navigator => Services.ResolveRequired<WinformsMVP.Services.IWindowNavigator>();
```

Update `Logger` to resolve the factory from the provider:

```csharp
if (_logger == null)
{
    var loggerFactory = Services.ResolveRequired<WinformsMVP.Logging.ILoggerFactory>();
    _logger = loggerFactory.CreateLogger(this.GetType());
}
```

Update `EnsureDispatcherConfigured` — replace `Platform.ConfigureDispatcher?.Invoke(_dispatcher)` with:

```csharp
_dispatcher.Logger = Logger;
Services.Resolve<WinformsMVP.Services.IDispatcherConfigurer>()?.Configure(_dispatcher);
```

Remove the old `_platform` field, `Platform` property, `SetPlatformServices`, and the `using` of `IPlatformServices` if present. Keep everything else (dispatcher init order, `_dispatcherInitialized`, etc.) unchanged.

- [ ] **Step 2: Rewrite the test helper**

`tests/WinformsMVP.Samples.Tests/TestHelpers/PresenterPlatformExtensions.cs` → rename method (keep file or rename to `PresenterServiceProviderExtensions.cs`):

```csharp
using System;
using WinformsMVP.Samples.Tests.TestHelpers;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    public static class PresenterServiceProviderExtensions
    {
        /// <summary>Injects a service provider for testing. Call before AttachView()/Initialize().</summary>
        public static T WithServiceProvider<T>(this T presenter, IServiceProvider provider) where T : class
        {
            dynamic dynamicPresenter = presenter;
            dynamicPresenter.SetServiceProvider(provider);  // internal, reachable via InternalsVisibleTo + dynamic
            return presenter;
        }
    }
}
```

- [ ] **Step 3: Replace `MockPlatformServices` with a provider fixture**

Replace `tests/WinformsMVP.Samples.Tests/Mocks/MockPlatformServices.cs` with a fixture that registers the mocks into a `DefaultServiceProvider` and still exposes the concrete mocks for verification:

```csharp
using WinformsMVP.Logging;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Test fixture that builds a service provider populated with mock framework services and
    /// exposes the concrete mocks for verification. Replaces the former MockPlatformServices.
    /// </summary>
    public class MockServices
    {
        public MockMessageService MessageService { get; } = new MockMessageService();
        public MockDialogProvider DialogProvider { get; } = new MockDialogProvider();
        public MockFileService FileService { get; } = new MockFileService();
        public MockWindowNavigator WindowNavigator { get; } = new MockWindowNavigator();
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>The provider to inject via <c>presenter.WithServiceProvider(mock.Provider)</c>.</summary>
        public DefaultServiceProvider Provider { get; }

        public MockServices()
        {
            Provider = new DefaultServiceProvider();
            Provider.RegisterInstance<IMessageService>(MessageService);
            Provider.RegisterInstance<IDialogProvider>(DialogProvider);
            Provider.RegisterInstance<IFileService>(FileService);
            Provider.RegisterInstance<IWindowNavigator>(WindowNavigator);
            Provider.RegisterInstance<ILoggerFactory>(LoggerFactory);
        }

        public void Reset() { MessageService.Clear(); FileService.Clear(); }
    }
}
```

> If a test sets a custom `ConfigureDispatcher`, register an `IDispatcherConfigurer` in `Provider` instead. Provide a tiny test impl where needed (e.g. a `DelegateDispatcherConfigurer` wrapping an `Action<ViewActionDispatcher>`); add it to the Mocks folder if more than one test needs it.

- [ ] **Step 4: Migrate every caller — transformation rules**

Apply these substitutions across the caller files (read each file, apply the matching rule):

| Old | New |
|-----|-----|
| `new MockPlatformServices()` | `new MockServices()` |
| `presenter.WithPlatformServices(mock)` | `presenter.WithServiceProvider(mock.Provider)` |
| `presenter.SetPlatformServices(x)` (internal calls) | `presenter.SetServiceProvider(provider)` |
| `mock.MessageService.X` (verification) | unchanged — `MockServices` still exposes it |
| `PlatformServices.Default = new DefaultPlatformServices(...)` (samples/startup) | `ServiceLocator.Configure(reg => { /* register overrides */ })` or `ServiceLocator.Current = <provider>` |
| `PlatformServices.Default` (read) | `ServiceLocator.Current` |
| `PlatformServices.Reset()` (tests) | `ServiceLocator.Reset()` |
| `mockServices.ConfigureDispatcher = action` | register `IDispatcherConfigurer` in the provider (see Step 3 note) |
| a `DefaultPlatformServices(null, loggerFactory)` to get logging in a test | `ServiceLocator.Configure(reg => reg.RegisterInstance<ILoggerFactory>(loggerFactory))` then inject `ServiceLocator.Current`, or build a `DefaultServiceProvider` directly with the factory + built-ins |

**Worked example** — `ToDoDemoPresenterTests.cs` (pattern; apply the analogous change to the other presenter tests):

```csharp
// BEFORE
var mock = new MockPlatformServices();
var presenter = new ToDoDemoPresenter().WithPlatformServices(mock);
presenter.AttachView(view);
presenter.Initialize();
// ... presenter.Dispatcher.Dispatch(...) ...
Assert.Contains("Saved", mock.MessageService.InfoMessages);

// AFTER
var mock = new MockServices();
var presenter = new ToDoDemoPresenter().WithServiceProvider(mock.Provider);
presenter.AttachView(view);
presenter.Initialize();
// ... presenter.Dispatcher.Dispatch(...) ...
Assert.Contains("Saved", mock.MessageService.InfoMessages);
```

For `LoggingIntegrationTests.cs` (which constructs `new DefaultPlatformServices(null, loggerFactory)`): build the provider explicitly —

```csharp
var provider = new DefaultServiceProvider();
provider.RegisterInstance<ILoggerFactory>(loggerFactory);          // the factory under test
provider.RegisterInstance<IMessageService>(new MockMessageService()); // plus any services the presenter needs
presenter.WithServiceProvider(provider);
```

- [ ] **Step 5: Build + full test**

Run: `dotnet build winforms-mvp.sln -c Debug` → 0 errors (pre-existing xUnit1031 warnings only).
Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj` → all pass.
Expected: the suite is green again with the new injection path. If a test relied on `IPlatformServices`-specific behavior, adapt it per the rules above.

- [ ] **Step 6: Commit**

```bash
git add -A
git reset .claude/settings.local.json
git commit -m "refactor(di): PresenterBase resolves via IServiceProvider/ServiceLocator; migrate test helper, mock fixture, tests, samples off IPlatformServices"
```

---

### Task 3: Delete the dead platform types

**Files:**
- Delete: `src/WinformsMVP/Services/IPlatformServices.cs`
- Delete: `src/WinformsMVP/Services/PlatformServices.cs`
- Delete: `src/WinformsMVP/Services/Implementations/DefaultPlatformServices.cs`
- Delete: `tests/WinformsMVP.Samples.Tests/Services/DefaultPlatformServicesTests.cs`
- Delete: `tests/WinformsMVP.Samples.Tests/Mocks/MockPlatformServices.cs` (only if it still exists after Task 2 replaced it with `MockServices.cs`)

- [ ] **Step 1: Confirm no references remain**

Run: `grep -rn "IPlatformServices\|PlatformServices\|DefaultPlatformServices\|MockPlatformServices" --include=*.cs src samples tests`
Expected: only doc-comment `<see cref>` mentions, if any — fix those to point at `ServiceLocator`/`IServiceProvider`. No code references.

- [ ] **Step 2: Delete the files and build**

```bash
git rm src/WinformsMVP/Services/IPlatformServices.cs src/WinformsMVP/Services/PlatformServices.cs src/WinformsMVP/Services/Implementations/DefaultPlatformServices.cs tests/WinformsMVP.Samples.Tests/Services/DefaultPlatformServicesTests.cs
```

Run: `dotnet build winforms-mvp.sln -c Debug` → 0 errors. `dotnet test ...` → all pass.

- [ ] **Step 3: Commit**

```bash
git commit -m "refactor(di): delete IPlatformServices/PlatformServices/DefaultPlatformServices and their tests"
```

---

### Task 4: Unify the M.E.DI bridge onto `ServiceLocator`

**Files:**
- Modify: `src/WinformsMVP.DependencyInjection/ServiceCollectionExtensions.cs`
- Modify: `samples/MultiProjectDemo.Shell/Program.cs`
- Test: `tests/WinformsMVP.Samples.Tests/...` (extend `ServiceCollectionExtensionsTests` if present)

- [ ] **Step 1: `AddWinformsMVP` also registers the framework service defaults**

So a M.E.DI provider can satisfy `PresenterBase`'s `ResolveRequired<IMessageService>()` etc. Add (using `TryAdd*` so hosts can override):

```csharp
services.TryAddSingleton(viewMappingRegister);
services.TryAddSingleton<IPresenterFactory, ServiceProviderPresenterFactory>();

// framework built-ins (host may register its own before calling AddWinformsMVP)
services.TryAddSingleton<IMessageService, MessageService>();
services.TryAddSingleton<IDialogProvider, DialogProvider>();
services.TryAddSingleton<IFileService, FileService>();
services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
services.TryAddSingleton<IWindowNavigator>(sp =>
    new WindowNavigator(sp.GetRequiredService<IViewMappingRegister>())); // M.E.DI is the IServiceProvider for view fallback if desired
```

(Add `using WinformsMVP.Services.Implementations;` and `using WinformsMVP.Logging;`.)

- [ ] **Step 2: Provide the locator hand-off**

Add a small helper so the host wires the framework to resolve through M.E.DI:

```csharp
/// <summary>
/// Points the framework's <see cref="ServiceLocator"/> at this provider, so presenter
/// convenience accessors (Messages, Dialogs, Navigator, ...) resolve through M.E.DI.
/// Call once at startup after BuildServiceProvider().
/// </summary>
public static IServiceProvider UseWinformsMVP(this IServiceProvider provider)
{
    ServiceLocator.Current = provider ?? throw new ArgumentNullException(nameof(provider));
    return provider;
}
```

- [ ] **Step 3: Update `MultiProjectDemo.Shell/Program.cs`**

After building the M.E.DI provider, call `provider.UseWinformsMVP();`. Remove any `PlatformServices.Default = ...` line. Keep `IModuleRegistrar` usage (the M.E.DI module path) and `IPresenterFactory` as-is. Read the file and apply minimally.

- [ ] **Step 4: Build + test**

Run: `dotnet build winforms-mvp.sln -c Debug` → 0 errors. `dotnet test ...` → all pass. If `ServiceCollectionExtensionsTests` exists, add a test asserting a built M.E.DI provider resolves `IMessageService`/`IWindowNavigator` and that `UseWinformsMVP` sets `ServiceLocator.Current` (reset after).

- [ ] **Step 5: Commit**

```bash
git add -A
git reset .claude/settings.local.json
git commit -m "feat(di): AddWinformsMVP registers framework defaults; add UseWinformsMVP locator hand-off"
```

---

### Task 5: Documentation

**Files:**
- Modify: `CLAUDE.md` (the "Dependency injection — three patterns" + "Services & platform access" sections)
- Modify: the DI wiki page `wiki/Reference-DependencyInjection.md` and `wiki/Reference-Platform-Services.md`

- [ ] **Step 1: Update the docs**

Reflect the new model: framework services resolve through `IServiceProvider`/`ServiceLocator`; `Platform`/`IPlatformServices` are gone; the convenience accessors (`Messages`, `Dialogs`, ...) remain on `PresenterBase`; modules without external DI use `IServiceModule`; M.E.DI users call `AddWinformsMVP(...)` then `provider.UseWinformsMVP()`. Keep the "service locator for framework, constructor injection for business services" discipline explicit. Do not invent APIs — describe exactly what Tasks 1–4 built.

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md wiki/
git commit -m "docs(di): document ServiceLocator/IServiceProvider model; remove Platform references"
```

---

### Task 6: Full gate

- [ ] **Step 1:** `dotnet build winforms-mvp.sln -c Debug` → 0 errors; only pre-existing xUnit1031 warnings.
- [ ] **Step 2:** `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj` → all pass.
- [ ] **Step 3:** `grep -rn "IPlatformServices\|PlatformServices" --include=*.cs src samples tests` → no code references (doc crefs already fixed). No commit (gate only).

---

## Self-Review

- **Spec coverage:** `PresenterBase` → `IServiceProvider`/`SetServiceProvider` with unchanged accessors (Task 2); built-ins incl. navigator-from-`IViewMappingRegister` registered in `ServiceLocator` (Task 1); `ConfigureDispatcher` → `IDispatcherConfigurer` (Tasks 1–2); deletions (Task 3); M.E.DI unification via `ServiceLocator.Current` + `IServiceModule` already exists from Phase 1, `IModuleRegistrar` retained for the M.E.DI path (Task 4); samples/tests migrated (Task 2/4); docs (Task 5). All spec sections covered.
- **Placeholder scan:** the mechanical caller migrations are rule-based with a worked example (a migration's call sites are derivable from the rule); all NEW/CORE code is given in full. Implementers read each caller file before applying the matching rule.
- **Type consistency:** `SetServiceProvider`/`Services`/`Resolve`/`ResolveRequired`/`IDispatcherConfigurer`/`MockServices`/`WithServiceProvider`/`UseWinformsMVP` are used identically across tasks.
- **Green-keeping order:** Task 1 additive → Task 2 atomic flip (PresenterBase + all callers together) → Task 3 delete dead code → Task 4 bridge → Task 5 docs. Each task ends green.

## Next phase

- **Phase 3 — anchored feedback:** `IAnchoredMessages` (cursor-anchored toast + message box) registered as a built-in in `ServiceLocator.RegisterBuiltIns`; presenter convenience accessor `AnchoredMessages`; carry over the `ToastNotification.ShowAnchored` multi-monitor fix from the `feature/show-toast-for` WIP commit `9d83b8b`.
