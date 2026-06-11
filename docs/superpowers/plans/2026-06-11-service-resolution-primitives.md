# Service-Resolution Primitives Implementation Plan (Phase 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the net40-safe service-resolution primitives (`IServiceRegistry`, `DefaultServiceProvider`, `ServiceLocator`, `IServiceModule`, `GetService<T>` extensions) as new, isolated code that breaks nothing — the foundation the later phases migrate onto.

**Architecture:** Resolution rides on the BCL `System.IServiceProvider`; registration is a tiny in-house `IServiceRegistry`. `DefaultServiceProvider` implements both (dictionary-backed, instance + lazy-singleton factory). `ServiceLocator` is a static ambient `Current` provider (Prism `ContainerLocator` role). `IServiceModule` lets a module register into a registry with no external DI. This phase adds only new files + tests; it does **not** touch `PresenterBase`, delete `IPlatformServices`, or register any built-in services (those are Phase 2).

**Tech Stack:** .NET Framework (net40;net48), C#, xUnit 2.9.3. All new types are net40-safe (`IServiceProvider`, `Func<>`, `Dictionary<>`, `Lazy<>` all exist in net40).

**Spec:** `docs/superpowers/specs/2026-06-11-service-locator-foundation-design.md`

---

### Task 1: `GetService<T>` / `GetRequiredService<T>` extensions

**Files:**
- Create: `src/WinformsMVP/Services/ServiceProviderExtensions.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/ServiceProviderExtensionsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class ServiceProviderExtensionsTests
    {
        private sealed class StubProvider : IServiceProvider
        {
            private readonly object _value;
            public StubProvider(object value) { _value = value; }
            public object GetService(Type serviceType) => _value;
        }

        public interface IFoo { }
        private sealed class Foo : IFoo { }

        [Fact]
        public void GetService_Generic_CastsResult()
        {
            IServiceProvider p = new StubProvider(new Foo());
            IFoo foo = p.GetService<IFoo>();
            Assert.NotNull(foo);
        }

        [Fact]
        public void GetService_Generic_ReturnsNullWhenAbsent()
        {
            IServiceProvider p = new StubProvider(null);
            Assert.Null(p.GetService<IFoo>());
        }

        [Fact]
        public void GetRequiredService_ThrowsWhenAbsent()
        {
            IServiceProvider p = new StubProvider(null);
            Assert.Throws<InvalidOperationException>(() => p.GetRequiredService<IFoo>());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceProviderExtensionsTests`
Expected: FAIL — `ServiceProviderExtensions` / `GetService<T>` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Generic convenience over the BCL non-generic <see cref="IServiceProvider"/>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>Resolves <typeparamref name="T"/>, or <c>null</c> if not registered.</summary>
        public static T GetService<T>(this IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            return (T)provider.GetService(typeof(T));
        }

        /// <summary>Resolves <typeparamref name="T"/>, throwing if it is not registered.</summary>
        public static T GetRequiredService<T>(this IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            object service = provider.GetService(typeof(T));
            if (service == null)
                throw new InvalidOperationException("No service registered for type " + typeof(T).FullName + ".");
            return (T)service;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceProviderExtensionsTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/ServiceProviderExtensions.cs tests/WinformsMVP.Samples.Tests/Services/ServiceProviderExtensionsTests.cs
git commit -m "feat(di): add IServiceProvider GetService<T>/GetRequiredService<T> extensions"
```

---

### Task 2: `IServiceRegistry` registration interface

**Files:**
- Create: `src/WinformsMVP/Services/IServiceRegistry.cs`

(No standalone test — exercised through `DefaultServiceProvider` in Task 3.)

- [ ] **Step 1: Write the interface**

```csharp
using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Minimal registration surface for the built-in service provider. Resolution is the BCL
    /// <see cref="IServiceProvider"/>; this is the matching "register" half (Prism's
    /// <c>IContainerRegistry</c> role, kept deliberately small and net40-safe).
    /// </summary>
    public interface IServiceRegistry
    {
        /// <summary>Registers a ready-made singleton instance for <typeparamref name="TService"/>.</summary>
        void RegisterInstance<TService>(TService instance);

        /// <summary>
        /// Registers a factory for <typeparamref name="TService"/>, invoked lazily on first
        /// resolution; the result is cached (singleton). The factory receives the provider so it
        /// can resolve its own dependencies.
        /// </summary>
        void RegisterFactory<TService>(Func<IServiceProvider, TService> factory);

        /// <summary>Whether <paramref name="serviceType"/> has a registration.</summary>
        bool IsRegistered(Type serviceType);
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/WinformsMVP/WinformsMVP.csproj`
Expected: build succeeds (net40 + net48).

- [ ] **Step 3: Commit**

```bash
git add src/WinformsMVP/Services/IServiceRegistry.cs
git commit -m "feat(di): add IServiceRegistry registration interface"
```

---

### Task 3: `DefaultServiceProvider` — instance registration + resolution

**Files:**
- Create: `src/WinformsMVP/Services/DefaultServiceProvider.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/DefaultServiceProviderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class DefaultServiceProviderTests
    {
        public interface IFoo { }
        private sealed class Foo : IFoo { }

        [Fact]
        public void RegisterInstance_ThenResolve_ReturnsSameInstance()
        {
            var sp = new DefaultServiceProvider();
            var foo = new Foo();
            sp.RegisterInstance<IFoo>(foo);

            Assert.Same(foo, sp.GetService<IFoo>());
        }

        [Fact]
        public void Resolve_Unregistered_ReturnsNull()
        {
            var sp = new DefaultServiceProvider();
            Assert.Null(sp.GetService(typeof(IFoo)));
        }

        [Fact]
        public void IsRegistered_ReflectsRegistration()
        {
            var sp = new DefaultServiceProvider();
            Assert.False(sp.IsRegistered(typeof(IFoo)));
            sp.RegisterInstance<IFoo>(new Foo());
            Assert.True(sp.IsRegistered(typeof(IFoo)));
        }

        [Fact]
        public void RegisterInstance_LastWins()
        {
            var sp = new DefaultServiceProvider();
            var first = new Foo();
            var second = new Foo();
            sp.RegisterInstance<IFoo>(first);
            sp.RegisterInstance<IFoo>(second);
            Assert.Same(second, sp.GetService<IFoo>());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter DefaultServiceProviderTests`
Expected: FAIL — `DefaultServiceProvider` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;

namespace WinformsMVP.Services
{
    /// <summary>
    /// The built-in, net40-safe service container: register into it via
    /// <see cref="IServiceRegistry"/>, resolve from it via <see cref="IServiceProvider"/>.
    /// Dictionary-backed; supports ready instances and lazy-singleton factories. No scopes —
    /// scoped/graph resolution is a real DI container's job (the M.E.DI bridge).
    /// </summary>
    public sealed class DefaultServiceProvider : IServiceRegistry, IServiceProvider
    {
        private sealed class Entry
        {
            public object Instance;
            public Func<IServiceProvider, object> Factory;
            public bool HasInstance;
        }

        private readonly Dictionary<Type, Entry> _entries = new Dictionary<Type, Entry>();
        private readonly object _lock = new object();

        public void RegisterInstance<TService>(TService instance)
        {
            lock (_lock)
            {
                _entries[typeof(TService)] = new Entry { Instance = instance, HasInstance = true };
            }
        }

        public void RegisterFactory<TService>(Func<IServiceProvider, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (_lock)
            {
                _entries[typeof(TService)] = new Entry { Factory = sp => factory(sp) };
            }
        }

        public bool IsRegistered(Type serviceType)
        {
            lock (_lock) { return _entries.ContainsKey(serviceType); }
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            lock (_lock)
            {
                Entry entry;
                if (!_entries.TryGetValue(serviceType, out entry))
                    return null;                       // BCL contract: unknown -> null
                if (!entry.HasInstance)
                {
                    entry.Instance = entry.Factory(this);   // lazy, then cache (singleton)
                    entry.HasInstance = true;
                    entry.Factory = null;
                }
                return entry.Instance;
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter DefaultServiceProviderTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/DefaultServiceProvider.cs tests/WinformsMVP.Samples.Tests/Services/DefaultServiceProviderTests.cs
git commit -m "feat(di): add DefaultServiceProvider (instance registration + resolution)"
```

---

### Task 4: `DefaultServiceProvider` — lazy-singleton factory registration

**Files:**
- Modify: `tests/WinformsMVP.Samples.Tests/Services/DefaultServiceProviderTests.cs` (add tests)

(Implementation already written in Task 3; this task proves the factory behaviour.)

- [ ] **Step 1: Write the failing tests (append to the test class)**

```csharp
        [Fact]
        public void RegisterFactory_ResolvesLazily_AndCachesSingleton()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterFactory<IFoo>(_ => { calls++; return new Foo(); });

            Assert.Equal(0, calls);                 // not built until resolved
            var a = sp.GetService<IFoo>();
            var b = sp.GetService<IFoo>();
            Assert.Same(a, b);                      // cached
            Assert.Equal(1, calls);                 // factory ran once
        }

        [Fact]
        public void RegisterFactory_ReceivesProvider_ForDependencyResolution()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterInstance<IFoo>(new Foo());
            sp.RegisterFactory<string>(provider => provider.GetService<IFoo>() != null ? "ok" : "missing");

            Assert.Equal("ok", sp.GetService<string>());
        }

        [Fact]
        public void RegisterFactory_NullFactory_Throws()
        {
            var sp = new DefaultServiceProvider();
            Assert.Throws<ArgumentNullException>(() => sp.RegisterFactory<IFoo>(null));
        }
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter DefaultServiceProviderTests`
Expected: PASS (7 tests total). They pass against the Task 3 implementation.

- [ ] **Step 3: Commit**

```bash
git add tests/WinformsMVP.Samples.Tests/Services/DefaultServiceProviderTests.cs
git commit -m "test(di): cover DefaultServiceProvider lazy-singleton factory behaviour"
```

---

### Task 5: `ServiceLocator` static ambient provider

**Files:**
- Create: `src/WinformsMVP/Services/ServiceLocator.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    [Collection("ServiceLocator")] // serialize: ServiceLocator.Current is process-global
    public class ServiceLocatorTests : IDisposable
    {
        public interface IFoo { }
        private sealed class Foo : IFoo { }

        public ServiceLocatorTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void Current_DefaultsToEmptyProvider_NotNull()
        {
            Assert.NotNull(ServiceLocator.Current);
            Assert.Null(ServiceLocator.Current.GetService<IFoo>()); // empty by default in this phase
        }

        [Fact]
        public void Current_CanBeReplaced()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterInstance<IFoo>(new Foo());
            ServiceLocator.Current = sp;
            Assert.NotNull(ServiceLocator.Current.GetService<IFoo>());
        }

        [Fact]
        public void Configure_RegistersIntoFreshProvider()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IFoo>(new Foo()));
            Assert.NotNull(ServiceLocator.Current.GetService<IFoo>());
        }

        [Fact]
        public void Reset_RestoresEmptyDefault()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IFoo>(new Foo()));
            ServiceLocator.Reset();
            Assert.Null(ServiceLocator.Current.GetService<IFoo>());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceLocatorTests`
Expected: FAIL — `ServiceLocator` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Static ambient access to the current <see cref="IServiceProvider"/> — the framework's
    /// service-resolution root (the Prism <c>ContainerLocator</c> role). The framework internals
    /// and the presenter convenience accessors resolve through <see cref="Current"/>; business
    /// code should prefer constructor injection rather than locating through this.
    /// </summary>
    /// <remarks>
    /// In this phase the default provider is empty. Phase 2 registers the framework built-ins
    /// (message service, dialogs, navigator, logger factory) here.
    /// </remarks>
    public static class ServiceLocator
    {
        private static readonly object _lock = new object();
        private static IServiceProvider _current;

        /// <summary>
        /// The ambient provider. Defaults to an empty <see cref="DefaultServiceProvider"/>.
        /// Assign a configured provider (the built-in registry, or a real DI container's
        /// <see cref="IServiceProvider"/>) at application startup.
        /// </summary>
        public static IServiceProvider Current
        {
            get
            {
                if (_current == null)
                {
                    lock (_lock)
                    {
                        if (_current == null) _current = new DefaultServiceProvider();
                    }
                }
                return _current;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                lock (_lock) { _current = value; }
            }
        }

        /// <summary>
        /// Builds a fresh <see cref="DefaultServiceProvider"/>, lets <paramref name="register"/>
        /// populate it, and installs it as <see cref="Current"/>.
        /// </summary>
        public static void Configure(Action<IServiceRegistry> register)
        {
            var provider = new DefaultServiceProvider();
            register?.Invoke(provider);
            Current = provider;
        }

        /// <summary>Resets to an empty default provider (primarily for tests).</summary>
        public static void Reset()
        {
            lock (_lock) { _current = null; }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceLocatorTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/ServiceLocator.cs tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorTests.cs
git commit -m "feat(di): add ServiceLocator static ambient provider"
```

---

### Task 6: `IServiceModule` modular registration

**Files:**
- Create: `src/WinformsMVP/Services/IServiceModule.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/ServiceModuleTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class ServiceModuleTests
    {
        public interface IBar { }
        private sealed class Bar : IBar { }

        private sealed class BarModule : IServiceModule
        {
            public void RegisterServices(IServiceRegistry registry)
                => registry.RegisterInstance<IBar>(new Bar());
        }

        [Fact]
        public void Module_RegistersIntoRegistry_WithoutExternalDI()
        {
            var sp = new DefaultServiceProvider();
            new BarModule().RegisterServices(sp);
            Assert.NotNull(sp.GetService<IBar>());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceModuleTests`
Expected: FAIL — `IServiceModule` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace WinformsMVP.Services
{
    /// <summary>
    /// A unit of service registration owned by a UI module. Modules register into the core
    /// <see cref="IServiceRegistry"/>, so modular composition works with no external DI container.
    /// When a real container is used, the same module is applied against its registration surface
    /// by the bridge package instead.
    /// </summary>
    public interface IServiceModule
    {
        void RegisterServices(IServiceRegistry registry);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj --filter ServiceModuleTests`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/WinformsMVP/Services/IServiceModule.cs tests/WinformsMVP.Samples.Tests/Services/ServiceModuleTests.cs
git commit -m "feat(di): add IServiceModule for DI-free modular registration"
```

---

### Task 7: Full build + test gate

- [ ] **Step 1: Build the whole solution (both target frameworks)**

Run: `dotnet build winforms-mvp.sln -c Debug`
Expected: build succeeds with 0 errors; `WinformsMVP` builds for net40 and net48; analyzer reports nothing on the new files.

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test tests/WinformsMVP.Samples.Tests/WinformsMVP.Samples.Tests.csproj`
Expected: all tests pass (existing suite + the new Service* tests). Nothing existing is affected — this phase adds only new types.

- [ ] **Step 3: No commit needed** (gate only).

---

## Self-Review

- **Spec coverage (this phase):** `IServiceRegistry` (Task 2), `DefaultServiceProvider` instance + lazy-singleton factory + unknown→null (Tasks 3–4), `ServiceLocator` Current/Configure/Reset (Task 5), `IServiceModule` (Task 6), `GetService<T>`/`GetRequiredService<T>` (Task 1). Built-in registration, `PresenterBase` migration, deletions, `ConfigureDispatcher`/navigator wiring, samples/tests migration, the M.E.DI bridge reconciliation, and the `IAnchoredMessages` feature are **explicitly deferred to Phases 2 and 3** (not in scope here).
- **Placeholder scan:** none — every code/command step is concrete.
- **Type consistency:** `IServiceRegistry` (RegisterInstance/RegisterFactory/IsRegistered), `DefaultServiceProvider` (implements both interfaces), `ServiceLocator` (Current/Configure/Reset), `IServiceModule.RegisterServices(IServiceRegistry)`, `GetService<T>`/`GetRequiredService<T>` — names are used identically across tasks.
- **Net40 safety:** only `IServiceProvider`, `Func<>`, `Dictionary<>`, `lock`, `Type` — all net40. No default interface methods, no value tuples.

## Next phases (separate plans, after this lands)

- **Phase 2 — migration:** register built-ins into the `ServiceLocator` default (incl. `IWindowNavigator` built from a `ViewMappingRegister`, `ILoggerFactory`, and `ConfigureDispatcher` rehomed as `IDispatcherConfigurer`); rework `PresenterBase` from `IPlatformServices`/`Platform` to `IServiceProvider`/`SetServiceProvider` with unchanged `Messages`/`Dialogs`/… accessors; **delete** `IPlatformServices`/`PlatformServices`/`DefaultPlatformServices`/`MockPlatformServices`; migrate all samples + tests; reconcile `WinformsMVP.DependencyInjection` (`IModuleRegistrar` → `IServiceModule`, `ServiceLocator.Current` = M.E.DI provider).
- **Phase 3 — anchored feedback:** `IAnchoredMessages` (cursor-anchored toast + message box) registered as a built-in; carry over the `ToastNotification.ShowAnchored` multi-monitor fix from the `feature/show-toast-for` WIP.
