# 统一配置入口 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除三个可变静态 `*Defaults` 类，所有框架定制收进一个启动入口 `MvpApplication.Configure(framework:, services:)` / `AddWinformsMVP(viewRegistry, mvp => ...)`，同时拆分消息框与 Toast、让 ChangeTracker 与 EventAggregator 可替换并有默认注册。

**Architecture:** 每个子系统先改成“构造注入 options/接口、不读静态”（Task 1–6），各自默认注册进 `ServiceLocator` 与 `AddWinformsMVP`；最后 Task 7 加 `WinformsMvpBuilder`，把“builder 选择 + 内置默认”合并成一份注册清单，两条注册路径共用；Task 8 迁移示例、模板、文档。

**Tech Stack:** C#（Core: netstandard2.0；WinformsMVP / DI: net472;net10.0-windows），xUnit 2.9.3（测试项目 net48）。

**Spec:** `docs/superpowers/specs/2026-10-19-unified-configuration-design.md`（执行者必须先读）

## Global Constraints

- 在从 `master` 新建的分支 `unified-config` 上工作，完成后开 PR（不直接推 master）。
- Core 不得引用 WinForms / System.Drawing，不新增包依赖（只有 `System.ComponentModel.Annotations`）；不引入 `Microsoft.Extensions.Options`。
- Presenter 不得引用 WinForms 类型（分析器会报错）。
- 不打 tag、不发布；不在任何 `.csproj` 写版本号。
- 库未发布，允许不兼容变更；每个不兼容变更写进 `CHANGELOG.md` 的 `[Unreleased]`（日文，沿用现有格式），迁移表见 spec §10。
- 优先级：services 块直接注册 > framework 块选择 > 内置默认；options 整体替换不合并（spec §3.3）。
- 不新增任何可变静态配置。
- 构建（Linux 可用）：`dotnet build winforms-mvp.sln -warnaserror` 必须 0 警告 0 错误。
- **安全规则（必须遵守）**：开发机受公司安全软件（CrowdStrike）监控。**不得在 Windows 侧执行任何东西**——不调用 `/mnt/c/...` 下的 `dotnet.exe`、`powershell.exe`、`cmd.exe`，不把构建产物复制到 Windows 目录运行，不反射加载程序集，不截屏、不模拟输入，不启动示例程序。本地只做 WSL/Linux 内的事。
- 测试（net48，只能在 Windows 跑）下文记作 **`WINTEST <filter>`**，含义是：在 GitHub Actions 上跑。具体做法：
  1. 本地先 `dotnet build winforms-mvp.sln -warnaserror`（Linux）确认能编译；
  2. 提交并 `git push`（分支 `unified-config`，第一次推送后开 Draft PR：`gh pr create --draft --base master`，之后每次推送 CI 自动运行）；
  3. `gh run watch` 等待结果；失败时 `gh run view --log-failed` 查看。`<filter>` 仅表示该步骤关注的测试，CI 总是跑全量（基线 600 个通过）。
  “先写测试、确认失败”这一步：在 Linux 上确认编译失败（新类型/成员不存在）即可视为“失败”；只有断言型失败需要看 CI 时，才推送一次只含测试的提交。
- 分析器测试：`dotnet test tests/WinformsMVP.Analyzers.Tests/WinformsMVP.Analyzers.Tests.csproj`（Linux 可跑，基线 46 个）。
- 每个任务提交后运行 `codex review --commit <sha>`，处理意见后再进入下一个任务。
- 提交信息结尾：`Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`。
- 用到 `ServiceLocator` 全局状态的测试类必须标 `[Collection("ServiceLocator")]`，构造和 `Dispose` 里调用 `ServiceLocator.Reset()`（沿用 `ServiceLocatorBuiltInsTests` 的写法）。

## Review Focus

1. **后台线程调用 `Toasts.Show`**（`Task.Run` 内、`ConfigureAwait(false)` 之后）：Toast 应出现在 UI 线程并按时关闭，绝不在后台线程建窗口。→ Task 5 测试 `Show_FromWorkerThread_CreatesToastOnUiThread`。
2. **在 `MvpApplication.Configure` 之前就访问过 `ServiceLocator.Current`**（例如静态字段初始化里取了服务）：旧的默认根被释放，之后解析到的是新配置。→ Task 1 / Task 7 测试 `Configure_AfterLazyDefaultWasCreated_DisposesItAndInstallsNew`。
3. **同一项在 `mvp.` 与 `services:` 都配置**：直接注册胜出，且 options 整体替换（`mvp.Toasts.Duration` 被丢弃）。→ Task 7 测试 `DirectOptionsRegistration_ReplacesBuilderOptionsWholesale`。
4. **Model 含数组 / 嵌套 `ICloneable` 字段**：修改元素后 `IsChanged == true`。→ Task 2 测试 `ArrayRoot_ElementEdit_IsDetected`、`NestedCloneable_IsDeepCopiedNotTrusted`。
5. **调用方在 `using (var font = ...)` 中传字体给后台 Toast，`using` 结束后 Toast 才显示**：不应抛 `ObjectDisposedException` 或画错。→ Task 5 测试 `Show_CopiesFontBeforeReturning`。

---

### Task 1: `DefaultServiceProvider` 释放单例，`ServiceLocator` 释放自建默认根

**Files:**
- Modify: `src/WinformsMVP.Core/Services/DefaultServiceProvider.cs`
- Modify: `src/WinformsMVP.Core/Services/ServiceLocator.cs`
- Test: `tests/WinformsMVP.Samples.Tests/Services/DefaultServiceProviderTests.cs`、`tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorTests.cs`

**Interfaces:**
- Produces: `DefaultServiceProvider : IServiceRegistry, IServiceProvider, IDisposable`；`internal void Register(Type serviceType, object instance)`；`internal void RegisterFactory(Type serviceType, Func<IServiceProvider, object> factory)`（Task 7 用来写入非泛型注册）。解析已释放的 provider 抛 `ObjectDisposedException`。
- Produces: `ServiceLocator` 内部记录“当前根是否由框架惰性创建”（`private static bool _currentIsLazyDefault`）；`internal static void Install(IServiceProvider provider)`：若旧根是惰性默认根则 `Dispose` 它，再安装新根。`Configure`、`Reset` 走同一逻辑；公开 setter 不释放任何东西。

- [ ] **Step 1: 写失败测试**（`DefaultServiceProviderTests`）
  - `Dispose_DisposesFactoryCreatedSingletons`：`RegisterFactory<IDisposableProbe>(_ => probe)`，先 `GetService` 一次，`Dispose()` 后 `probe.Disposed == true`。
  - `Dispose_DoesNotDisposeRegisteredInstances`：`RegisterInstance(probe)`，`Dispose()` 后 `probe.Disposed == false`。
  - `Dispose_SkipsFactoriesNeverResolved`：工厂从未被解析时，工厂不被调用（计数器 0）。
  - `GetService_AfterDispose_Throws`：`Assert.Throws<ObjectDisposedException>`。
  - `NonGenericRegister_ResolvesByType`：`Register(typeof(IFoo), foo)` 后 `GetService(typeof(IFoo))` 为同一实例。
  （`ServiceLocatorTests`，`[Collection("ServiceLocator")]`）
  - `Configure_AfterLazyDefaultWasCreated_DisposesItAndInstallsNew`：先访问 `ServiceLocator.Current` 并通过它解析一个由工厂创建的可释放服务（可用 Task 4 前尚不存在的服务以外的任意内置；若无可释放内置，用 `((DefaultServiceProvider)ServiceLocator.Current).RegisterFactory<IDisposableProbe>(...)` 注入探针并解析），再 `ServiceLocator.Configure(_ => { })`，断言探针已释放、`ServiceLocator.Current` 是新实例。
  - `Setter_DoesNotDisposePreviousRoot`：`ServiceLocator.Current = a; ServiceLocator.Current = b;` 后 `a` 未释放。
  - `Reset_DisposesLazyDefaultRoot`。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~DefaultServiceProviderTests|FullyQualifiedName~ServiceLocatorTests`，预期编译失败或断言失败。
- [ ] **Step 3: 实现** — 记录由工厂创建的实例（按创建顺序），`Dispose` 逆序释放其中实现 `IDisposable` 的；幂等；不持锁调用 `Dispose`。
- [ ] **Step 4: 运行通过** — 同上过滤，全部 PASS；再全量 `WINTEST` 无回归。
- [ ] **Step 5: 提交** — `feat(core): DefaultServiceProvider disposes the singletons it created`；运行 `codex review --commit <sha>`。

---

### Task 2: ChangeTracker 可替换引擎，策略构造时固定，去掉 `ICloneable` 优先，删除 `ChangeTrackerDefaults`

**Files:**
- Create: `src/WinformsMVP.Core/Common/ChangeTracking/IObjectCloner.cs`、`IObjectContentComparer.cs`、`ChangeTrackingPolicy.cs`、`BuiltInObjectCloner.cs`、`BuiltInObjectContentComparer.cs`（命名空间 `WinformsMVP.Common`，与 `ChangeTracker` 相同）
- Modify: `src/WinformsMVP.Core/Common/ChangeTracker.cs`、`src/WinformsMVP.Core/Common/ObjectCloner.cs`、`src/WinformsMVP.Core/Common/ObjectComparer.cs`（只改引用 `ChangeTrackerDefaults` 的注释/报错文字）
- Delete: `src/WinformsMVP.Core/Common/ChangeTrackerDefaults.cs`、`tests/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs`
- Test: Create `tests/WinformsMVP.Samples.Tests/Common/ChangeTrackingPolicyTests.cs`；Modify `ObjectClonerTests.cs`、`ChangeTrackerTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface IObjectCloner          { object DeepClone(object value); }
  public interface IObjectContentComparer { bool ContentEquals(object left, object right); }
  public sealed class BuiltInObjectCloner : IObjectCloner                    { public static readonly BuiltInObjectCloner Instance; }   // 包装 ObjectCloner.DeepCopy
  public sealed class BuiltInObjectContentComparer : IObjectContentComparer  { public static readonly BuiltInObjectContentComparer Instance; } // 包装 ObjectComparer.ContentEquals
  public sealed class ChangeTrackingPolicy<T> where T : class
  {
      public ChangeTrackingPolicy(Func<T, T> clone, Func<T, T, bool> contentEquals);   // 任一为 null 抛 ArgumentNullException
      public Func<T, T> Clone { get; }
      public Func<T, T, bool> ContentEquals { get; }
      internal static ChangeTrackingPolicy<T> From(IObjectCloner cloner, IObjectContentComparer comparer);
  }
  ```
- ChangeTracker 构造函数（替换现有两个）：
  ```csharp
  public ChangeTracker(T initialValue);                                    // 内置引擎
  public ChangeTracker(T initialValue, Func<T, T, bool> comparer);         // 只换比较，复制用内置
  public ChangeTracker(T initialValue, IEqualityComparer<T> comparer);     // 同上
  public ChangeTracker(T initialValue, ChangeTrackingPolicy<T> policy);    // 显式策略
  ```
  构造时把 clone/compare 委托存入字段，之后不再读任何外部状态。`comparer`/`policy` 为 null 抛 `ArgumentNullException`。

- [ ] **Step 1: 写失败测试**（`ChangeTrackingPolicyTests`）
  - `ArrayRoot_ElementEdit_IsDetected`：`var t = new ChangeTracker<Item[]>(new[] { new Item { Name = "a" } }); t.CurrentValue[0].Name = "b"; Assert.True(t.IsChanged);`
  - `CloneableRoot_IsNotTrusted`：根类型实现 `ICloneable` 且 `Clone()` 为浅拷贝（`MemberwiseClone`）、含可变子对象；改子对象字段后 `IsChanged == true`。
  - `NestedCloneable_IsDeepCopiedNotTrusted`（放 `ObjectClonerTests`）：嵌套字段类型实现浅 `ICloneable`，`ObjectCloner.DeepCopy` 后修改副本的孙对象不影响原对象。
  - `Policy_IsUsedForCloneAndCompare`：计数 clone / compare 调用次数（构造时 clone 2 次，读 `IsChanged` 时 compare 1 次）。
  - `ComparerOnlyCtor_UsesBuiltInCloner`：传 `(a,b) => a.Id == b.Id`，改 `Name` 后 `IsChanged == false`。
  - `PolicyCtor_NullThrows` / `ComparerCtor_NullThrows`。
  - 删除 `ChangeTrackerHookTests.cs`；其中仍有意义的断言（例如“实体自身 `Equals` 被忽略”）若 `ChangeTrackerContentCompareTests` 未覆盖，移入本文件。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~ChangeTrack|FullyQualifiedName~ObjectCloner`。
- [ ] **Step 3: 实现** — `ObjectCloner.Copy` 删除 `ICloneable` 分支；`NotSupportedException` 文字改为 “… Register a ChangeTrackingPolicy for the model type, or replace the cloner (mvp.ChangeTracking.UseCloner).”；`ChangeTracker` 删 `BuildCloneFunc`/`BuildComparerFunc`，改为构造时固定；更新类级 XML 注释的解析顺序说明。删除 `ChangeTrackerDefaults.cs`。
- [ ] **Step 4: 运行通过** — 同上过滤 PASS；全量 `WINTEST` 无回归；`dotnet build winforms-mvp.sln -warnaserror`（样例中如有 `new ChangeTracker<T>(x, null)` 或引用 `ChangeTrackerDefaults` 的地方一并修正）。
- [ ] **Step 5: 提交** — `feat(core)!: pluggable ChangeTracker engines fixed at construction; ICloneable no longer trusted`；`codex review --commit <sha>`。

---

### Task 3: `TrackChanges` 与引擎的默认注册

**Files:**
- Modify: `src/WinformsMVP.Core/MVP/Presenters/PresenterBase.cs`、`src/WinformsMVP.Core/Services/ServiceLocator.cs`（`RegisterBuiltIns`）、`src/WinformsMVP.DependencyInjection/ServiceCollectionExtensions.cs`
- Test: Create `tests/WinformsMVP.Samples.Tests/Presenters/TrackChangesTests.cs`；Modify `tests/WinformsMVP.Samples.Tests/Services/ServiceLocatorBuiltInsTests.cs`、`tests/WinformsMVP.Samples.Tests/DependencyInjection/ServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: Task 2 的 `IObjectCloner`、`IObjectContentComparer`、`ChangeTrackingPolicy<T>`。
- Produces（`PresenterBase<TView>`，`protected`）：
  ```csharp
  protected ChangeTracker<T> TrackChanges<T>(T value) where T : class;
  protected ChangeTracker<T> TrackChanges<T>(T value, ChangeTrackingPolicy<T> policy) where T : class;
  protected ChangeTracker<T> TrackChanges<T>(T value, Func<T, T, bool> comparer) where T : class;
  ```
  解析顺序：显式 `policy` → `Services.GetService(typeof(ChangeTrackingPolicy<T>))` → `Services` 中的 `IObjectCloner` / `IObjectContentComparer`（缺失时用 `BuiltIn*.Instance`）。只传 `comparer` 时，复制器按同样顺序取（类型策略的 `Clone` → 注册的 `IObjectCloner` → 内置）。
- 默认注册：`ServiceLocator.RegisterBuiltIns` 与 `AddWinformsMVP`（`TryAddSingleton`）各注册 `IObjectCloner = BuiltInObjectCloner.Instance`、`IObjectContentComparer = BuiltInObjectContentComparer.Instance`。

- [ ] **Step 1: 写失败测试**（`TrackChangesTests`，`[Collection("ServiceLocator")]`，Presenter 用测试内的最小 `WindowPresenterBase<IMockView>` 子类，在 `OnInitialize` 里调用 `TrackChanges` 并把结果存到测试可读的字段；用 `PresenterTestHost.Start` 驱动）
  - `RegisteredTypePolicy_WinsOverEngines`：`ServiceLocator.Configure(r => r.RegisterInstance(new ChangeTrackingPolicy<Order>(clone, (a,b) => true)))`；修改后 `IsChanged == false`。
  - `RegisteredComparerEngine_IsUsed`：注册一个总返回 `true` 的 `IObjectContentComparer`，修改后 `IsChanged == false`。
  - `ExplicitPolicy_WinsOverRegistered`。
  - `ComparerOverload_UsesRegisteredCloner`：注册计数型 `IObjectCloner`，`TrackChanges(order, (a,b)=>...)` 后计数 2。
  - `PolicyFixedAtCreation`：创建 tracker 后再 `ServiceLocator.Configure(...)` 换比较器，旧 tracker 行为不变。
  - `ServiceLocatorBuiltInsTests.Default_RegistersChangeTrackingEngines`、`ServiceCollectionExtensionsTests.AddWinformsMVP_RegistersChangeTrackingEngines`。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~TrackChanges|FullyQualifiedName~BuiltIns|FullyQualifiedName~ServiceCollectionExtensions`。
- [ ] **Step 3: 实现**（如上接口）。
- [ ] **Step 4: 运行通过**；全量 `WINTEST`；构建 `-warnaserror`。
- [ ] **Step 5: 提交** — `feat: Presenter.TrackChanges resolves change-tracking policy from services`；`codex review`。

---

### Task 4: EventAggregator 默认单例、可释放、按订阅捕获上下文，删除 `ClearSubscriptions`

**Files:**
- Modify: `src/WinformsMVP.Core/Common/EventAggregator/IEventAggregator.cs`、`EventAggregator.cs`、`src/WinformsMVP.Core/Services/ServiceLocator.cs`、`src/WinformsMVP.DependencyInjection/ServiceCollectionExtensions.cs`、`samples/WinformsMVP.Samples/ComplexInteractionDemo_EventBased/ComplexInteractionDemoEventBasedProgram.cs`
- Test: Modify `tests/WinformsMVP.Samples.Tests/Common/EventAggregatorTests.cs`、`ServiceLocatorBuiltInsTests.cs`、`ServiceCollectionExtensionsTests.cs`；Modify `tests/WinformsMVP.Analyzers.Tests/`（MVP017 测试文件）

**Interfaces:**
- Produces: `IEventAggregator { Subscribe<T>(Action<T>); Subscribe<T>(Action<T>, Func<T,bool>); Publish<T>(T); }`（接口 XML 文档写明 spec §6 的语义清单）；`EventAggregator : IEventAggregator, IDisposable`；构造函数 `EventAggregator()` / `EventAggregator(ILoggerFactory)` 不再捕获上下文。
- 默认注册：`ServiceLocator.RegisterBuiltIns` → `RegisterFactory<IEventAggregator>(sp => new EventAggregator(sp.ResolveRequired<ILoggerFactory>()))`；`AddWinformsMVP` → `TryAddSingleton<IEventAggregator>(sp => new EventAggregator(sp.GetRequiredService<ILoggerFactory>()))`。

- [ ] **Step 1: 写失败测试**
  - `Subscribe_CapturesSubscriberContext_NotConstructorContext`：在无上下文线程构造 aggregator，在装有自定义 `SynchronizationContext` 的线程订阅，从第三个线程 `Publish`，断言 handler 经该上下文的 `Post` 执行。
  - `Dispose_ClearsSubscriptions_AndRejectsNew`：`Dispose()` 后 `Publish` 不调用 handler；`Subscribe` 抛 `ObjectDisposedException`。
  - `Dispose_DropsQueuedDeliveries`：用不立即执行的手动上下文排队一条消息，`Dispose()` 后再泵队列，handler 未执行。
  - `Default_ResolvesSingletonEventAggregator`（ServiceLocator 与 M.E.DI 各一）：两次解析为同一实例。
  - 分析器：`MVP017_FiresForCustomIEventAggregatorImplementation`。
  - 把现有测试中的 `ClearSubscriptions()` 改为新建实例或 `Dispose()`。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~EventAggregator|FullyQualifiedName~BuiltIns|FullyQualifiedName~ServiceCollectionExtensions`；分析器测试。
- [ ] **Step 3: 实现** — 示例 Program 改为从 `ServiceLocator.Current` 解析 `IEventAggregator`，不再 `new`。
- [ ] **Step 4: 运行通过**；全量；构建。
- [ ] **Step 5: 提交** — `feat(core)!: EventAggregator registered by default, disposable, per-subscription context`；`codex review`。

---

### Task 5: Toast 拆为独立服务，实例化 ToastManager，可靠 UI 调度，删除 `ToastDefaults`

**Files:**
- Create: `src/WinformsMVP.Core/Services/IToastService.cs`；`src/WinformsMVP/Services/IWinFormsToastService.cs`；`src/WinformsMVP/Services/ToastHostOptions.cs`；`src/WinformsMVP/Services/Implementations/WinFormsToastService.cs`
- Modify: `IMessageService.cs`（删 `ShowToast`）、`MessageService.cs`（删 toast 部分）、`ToastNotification.cs`、`ToastManager.cs`（静态 → 实例）、`ToastRendererResolver.cs`、`UiThread.cs`（删除，或仅保留给其他调用者并去掉“回退到调用线程”分支——先 grep 调用者）、`AnchoredToast.cs`、`PresenterBase.cs`（`Toasts` 属性）、`WinFormsPlatformServices.cs`、`ServiceCollectionExtensions.cs`、`samples/.../AnchoredMessageDemo/*`、`samples/.../ToastDemo/ToastDemoForm.cs`、`tests/.../Mocks/MockMessageService.cs`
- Create: `tests/WinformsMVP.Samples.Tests/Mocks/MockToastService.cs`
- Delete: `src/WinformsMVP/Services/ToastDefaults.cs`、`src/WinformsMVP/Services/MessageServiceToastExtensions.cs`
- Test: Create `tests/WinformsMVP.Samples.Tests/Services/WinFormsToastServiceTests.cs`；Modify `ToastRendererResolverTests.cs`、`ToastLayoutTests.cs`、`AnchoredMessageDemoPresenterTests.cs`

**Interfaces:**
- Produces（Core）：`public interface IToastService { void Show(string text, ToastType type, int? duration = null); }`；`PresenterBase.Toasts`（`protected IToastService Toasts => Services.ResolveRequired<IToastService>();`）。
- Produces（WinformsMVP）：
  ```csharp
  public interface IWinFormsToastService : IToastService
  {
      void Show(string text, ToastType type, ToastOptions options);
      void ShowAnchored(string text, ToastType type, Point anchor, ToastOptions options = null);
  }
  public sealed class ToastHostOptions   // 属性名与默认值逐一沿用 ToastDefaults
  {
      public ToastPosition Position { get; set; } = ToastPosition.BottomRight;
      public Size Size { get; set; } = new Size(350, 80);
      public bool AutoHeight { get; set; }            // false
      public int MinHeight { get; set; } = 40;
      public int MaxHeight { get; set; } = 220;
      public Font Font { get; set; }                  // null => 服务内部创建 Segoe UI 10pt 并负责释放
      public int Duration { get; set; } = 3000;
      public int Margin { get; set; } = 20;
      public int Gap { get; set; } = 10;
      public int MaxVisibleToasts { get; set; } = 5;
      public double Opacity { get; set; } = 0.95;
      public ToastStyle Style { get; set; } = ToastStyle.Soft;
      public bool ShowCloseButton { get; set; } = true;
  }
  public sealed class WinFormsToastService : IWinFormsToastService, IDisposable
  {
      public WinFormsToastService(ToastHostOptions options, ToastRenderer renderer = null, ILoggerFactory loggerFactory = null);
  }
  ```
  构造时校验 options（`Size` 宽高 > 0、`Duration` > 0、`MinHeight` ≤ `MaxHeight`、`MaxVisibleToasts` ≥ 1、`0 < Opacity ≤ 1`，否则 `ArgumentException`）并复制成内部快照。`Renderer` 优先级保持：调用级 renderer → 调用级 style → 服务的 renderer → 服务的 style。
- UI 调度：服务在 UI 线程首次被调用时捕获 `SynchronizationContext.Current`（须为 `WindowsFormsSynchronizationContext`），之后所有 `Show` 经它 `Post`；若在非 UI 线程调用且尚未捕获、或已 `Dispose`：记录 `LogWarning` 并忽略（释放已复制的字体）。不得在调用线程创建窗口。
- 调用级 `ToastOptions` 在 `Show` 返回前同步复制（`Font` 用 `new Font(font, font.Style)` 复制），复制品归 Toast 所有，Toast 关闭时释放。
- 注册：`WinFormsPlatformServices` / `AddWinformsMVP` 注册 `ToastHostOptions`（默认实例）、`WinFormsToastService`（工厂，取 `ToastHostOptions`、可选 `ToastRenderer`、`ILoggerFactory`），并把 `IWinFormsToastService`、`IToastService` 都指向同一实例。
- `AnchoredToast.Show(...)` 签名不变，内部 `ServiceLocator.Current.ResolveRequired<IWinFormsToastService>().ShowAnchored(...)`。

- [ ] **Step 1: 写失败测试**（`WinFormsToastServiceTests`；需要 UI 线程的测试在专用 STA 线程上跑 `Application.Run` 小循环，参照现有 `WindowLifecycleControllerTests` 中的做法，完成后 `Application.ExitThread()`）
  - `Show_WithoutDuration_UsesHostOptionsDuration`：`ToastHostOptions { Duration = 4500 }`，断言创建的 Toast 时长 4500（通过 internal 观察点，例如 `internal IReadOnlyList<ToastNotification> LiveToasts` + `ToastNotification.Duration` internal 属性）。
  - `Show_FromWorkerThread_CreatesToastOnUiThread`：UI 线程先调用一次 `Show` 完成捕获，再从 `Task.Run` 调用，断言 Toast 的创建线程 id == UI 线程 id。
  - `Show_FromWorkerBeforeUiCapture_IsIgnoredAndLogged`：用 `CapturingLogger`，断言无 Toast 且有一条 Warning。
  - `Show_CopiesFontBeforeReturning`：worker 线程 `using (var f = new Font("Arial", 12)) svc.Show("x", ToastType.Info, new ToastOptions { Font = f });`，之后泵 UI 队列，Toast 的字体未释放、`Name == "Arial"`。
  - `MaxVisibleToasts_EvictsOldest`（迁移自原静态行为）。
  - `ShowAnchored_ReplacesPreviousAnchored_AndDoesNotStack`。
  - `TwoServices_HaveIndependentStacks`。
  - `Ctor_InvalidOptions_Throws`（每条校验各一个 `[Theory]` 数据行）。
  - `ServiceLocatorBuiltInsTests.Default_ToastServices_AreSameInstance`：`IToastService` 与 `IWinFormsToastService` 为同一对象（M.E.DI 同样一条）。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~Toast|FullyQualifiedName~BuiltIns|FullyQualifiedName~AnchoredMessageDemo`。
- [ ] **Step 3: 实现** — 示例中 Presenter 的 `Messages.ShowToast(...)` 改 `Toasts.Show(...)`；View 端带 `ToastOptions` 的调用改为解析 `IWinFormsToastService`（`ToastDemoForm` 通过 `ServiceLocator.Current.ResolveRequired<IWinFormsToastService>()`）。`MockMessageService` 删 `ShowToast`；新增 `MockToastService : IToastService`（记录 `Calls`），相关 Presenter 测试改断言它（测试通过 `ServiceLocator.Configure` 注册 mock）。
- [ ] **Step 4: 运行通过**；全量；构建 `-warnaserror`。
- [ ] **Step 5: 提交** — `feat!: IToastService split from IMessageService; instance toast host replaces ToastDefaults`；`codex review`。

---

### Task 6: 消息框 backend、对话框 options，删除 `DialogDefaults`

**Files:**
- Create: `src/WinformsMVP/Services/IMessageDialogBackend.cs`、`MessageDialogRequest.cs`、`MessageDialogResult.cs`、`MessageDialogOptions.cs`、`DialogProviderOptions.cs`；`src/WinformsMVP/Services/Implementations/NativeMessageDialogBackend.cs`
- Modify: `MessageService.cs`、`DialogProvider.cs`、`AnchoredMessageBox.cs`、`src/WinformsMVP.Core/Services/Implementations/DialogOptions/FolderBrowserDialogOptions.cs`（`Description` 默认 `null`）、`OpenFileDialogOptions.cs` / `SaveFileDialogOptions.cs`（只改注释）、`WinFormsPlatformServices.cs`、`ServiceCollectionExtensions.cs`
- Delete: `src/WinformsMVP.Core/Services/DialogDefaults.cs`
- Test: Create `tests/WinformsMVP.Samples.Tests/Services/MessageServiceTests.cs`、`DialogProviderOptionsTests.cs`

**Interfaces:**
- Produces（WinformsMVP，命名空间 `WinformsMVP.Services`）：
  ```csharp
  public enum MessageDialogButtons { Ok, OkCancel, YesNo, YesNoCancel }
  public enum MessageDialogIcon    { None, Information, Warning, Error, Question }
  public enum MessageDialogResult  { None, Ok, Cancel, Yes, No }
  public sealed class MessageDialogRequest
  {
      public string Text { get; set; }
      public string Caption { get; set; }            // MessageService 已填好默认标题
      public MessageDialogButtons Buttons { get; set; }
      public MessageDialogIcon Icon { get; set; }
      public IWin32Window Owner { get; set; }        // 可选
      public Point? Location { get; set; }           // 可选；backend 不支持可忽略
  }
  public interface IMessageDialogBackend { MessageDialogResult Show(MessageDialogRequest request); }
  public sealed class MessageDialogOptions  { public string DefaultCaption { get; set; } = "System"; }
  public sealed class DialogProviderOptions // 属性名与默认值逐一沿用 DialogDefaults 除 DefaultMessageCaption 外的全部属性
  public MessageService(IMessageDialogBackend backend, MessageDialogOptions options);
  public DialogProvider(DialogProviderOptions options);
  public sealed class NativeMessageDialogBackend : IMessageDialogBackend   // MessageBox.Show；Location 有值时用从 AnchoredMessageBox 移来的 CBT hook 定位
  ```
- `AnchoredMessageBox` 公开方法签名不变；内部构造 `MessageDialogRequest { Location = location, Owner = owner }` 交给 `ServiceLocator.Current.ResolveRequired<IMessageDialogBackend>()`，空标题用 `MessageDialogOptions.DefaultCaption`，把 `MessageDialogResult` 映射回 `DialogResult`。
- 注册：`MessageDialogOptions`、`DialogProviderOptions` 默认实例；`IMessageDialogBackend = NativeMessageDialogBackend`；`MessageService`、`DialogProvider` 改为工厂（构造注入）；`DialogProvider` 仍同一实例注册为 `IDialogProvider` 与 `IWinFormsDialogProvider`。

- [ ] **Step 1: 写失败测试**
  - `ShowInfo_EmptyCaption_UsesDefaultCaption`：`new MessageService(fakeBackend, new MessageDialogOptions { DefaultCaption = "MyApp" })`，断言请求的 `Caption == "MyApp"`、`Buttons == Ok`、`Icon == Information`。
  - `ConfirmYesNoCancel_MapsResults`（`[Theory]`：Yes→Yes，No→No，Cancel→Cancel）。
  - `ConfirmOkCancel_ReturnsTrueOnlyForOk`。
  - `AnchoredMessageBox_RoutesThroughRegisteredBackend`（`[Collection("ServiceLocator")]`）：注册假 backend，调用 `AnchoredMessageBox.ConfirmYesNo("q", new Point(10,20))`，断言请求 `Location == (10,20)`、标题为默认标题。
  - `DialogProvider_FolderDescription_UsesOptionsWhenUnset`：断言 `new FolderBrowserDialogOptions().Description == null`，以及 provider 解析出的描述来自 `DialogProviderOptions.FolderBrowserDescription`（把“选项合并”抽成 internal 纯函数 `internal static string ResolveFolderDescription(FolderBrowserDialogOptions, DialogProviderOptions)` 以便不弹窗测试；Open/Save 的标题与过滤器同样各抽一个）。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~MessageService|FullyQualifiedName~DialogProviderOptions`。
- [ ] **Step 3: 实现** — 删除 `DialogDefaults.cs`，修正所有引用。
- [ ] **Step 4: 运行通过**；全量；构建。
- [ ] **Step 5: 提交** — `feat!: replaceable message dialog backend; DialogDefaults replaced by options`；`codex review`。

---

### Task 7: `WinformsMvpBuilder`、`MvpApplication.Configure`、`AddWinformsMVP(viewRegistry, configure)`

**Files:**
- Create: `src/WinformsMVP/Configuration/WinformsMvpBuilder.cs`（含各分组类：`MessagesBuilder`、`ToastsBuilder`、`DialogsBuilder`、`ChangeTrackingBuilder`、`EventsBuilder`、`LoggingBuilder`、`ActionsBuilder`，可各自一个文件）、`src/WinformsMVP/Configuration/FrameworkRegistration.cs`（internal）、`src/WinformsMVP/MvpApplication.cs`
- Modify: `src/WinformsMVP/WinformsMVP.csproj`（加 `<InternalsVisibleTo Include="WinformsMVP.DependencyInjection" />`）、`WinFormsPlatformServices.cs`（改为 `new WinformsMvpBuilder().Apply(registry)`）、`ServiceLocator.cs`（Core 的 `RegisterBuiltIns` 只保留无 WinForms 环境下的默认：FileService、NullLoggerFactory、两个 ChangeTracking 引擎、EventAggregator）、`ServiceCollectionExtensions.cs`
- Test: Create `tests/WinformsMVP.Samples.Tests/Services/WinformsMvpBuilderTests.cs`、`RegistrationParityTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `DefaultServiceProvider.Register(Type, object)` / `RegisterFactory(Type, Func<IServiceProvider, object>)`；Task 2–6 的全部服务与 options 类型。
- Produces:
  ```csharp
  public sealed class WinformsMvpBuilder
  {
      public MessagesBuilder Messages { get; }        // UseDialogs(IMessageDialogBackend) / UseDialogs(Func<IServiceProvider, IMessageDialogBackend>); string DefaultCaption { get; set; }
      public ToastsBuilder Toasts { get; }            // ToastHostOptions 的全部属性同名直通；UseRenderer(ToastRenderer) / UseRenderer(Func<...>); Use(IWinFormsToastService) / Use(Func<...>)
      public DialogsBuilder Dialogs { get; }          // DialogProviderOptions 的全部属性同名直通
      public ChangeTrackingBuilder ChangeTracking { get; } // UseCloner(...)×2, UseComparer(...)×2, For<T>(ChangeTrackingPolicy<T>)
      public EventsBuilder Events { get; }            // Use(IEventAggregator) / Use(Func<...>)
      public LoggingBuilder Logging { get; }          // Use(ILoggerFactory)
      public ActionsBuilder Actions { get; }          // Configure(Action<ViewActionDispatcher>) 可多次（按顺序组合）; StrictValidation()
      internal IReadOnlyList<FrameworkRegistration> BuildRegistrations();   // builder 选择覆盖内置默认后的完整清单
      internal void Apply(DefaultServiceProvider provider);                 // 写入清单（后写胜）
  }
  internal sealed class FrameworkRegistration { public Type ServiceType; public object Instance; public Func<IServiceProvider, object> Factory; }

  public static class MvpApplication
  {
      public static IServiceProvider Configure(
          Action<WinformsMvpBuilder> framework = null,
          Action<IServiceRegistry> services = null);
  }

  // WinformsMVP.DependencyInjection
  public static IServiceCollection AddWinformsMVP(this IServiceCollection services, IViewMappingRegister viewMappingRegister, Action<WinformsMvpBuilder> configure);
  // 现有 AddWinformsMVP(services, viewMappingRegister) 转调 configure: null
  ```
  - `MvpApplication.Configure`：新建 `DefaultServiceProvider` → Core 默认 → `builder.Apply` → `services` 回调 → `ServiceLocator.Install(provider)`（Task 1，会释放惰性默认根）→ 返回 provider。View 映射仍是容器里的 `IViewMappingRegister`（`ViewMappingRegister` 默认实例，使用者照旧 `Resolve<IViewMappingRegister>().RegisterFromAssembly(...)`）。
  - `AddWinformsMVP`：对 `BuildRegistrations()` 的每一项 `TryAdd`（实例 → `ServiceDescriptor.Singleton(type, instance)`，工厂 → `ServiceDescriptor.Singleton(type, factory)`），`IViewMappingRegister` 与 `IPresenterFactory` 仍按现有逻辑。
  - 同一个 builder 实例只能 `BuildRegistrations` 一次之后再修改则抛 `InvalidOperationException`（防止“builder 失效后还在改”）。

- [ ] **Step 1: 写失败测试**（`[Collection("ServiceLocator")]`）
  - `NoFramework_IsEquivalentToServiceLocatorConfigure`：两种方式各建一次，逐个内置服务类型断言实现类型相同。
  - `BuilderChoice_OverridesBuiltInDefault`：`mvp.Messages.UseDialogs(fake)` → 解析 `IMessageDialogBackend` 为 `fake`；`mvp.ChangeTracking.UseComparer(c)` → 解析为 `c`；`mvp.Events.Use(e)` → `e`。
  - `DirectRegistration_WinsOverBuilder`：`framework` 设 `UseDialogs(a)`，`services` 注册 `IMessageDialogBackend` 为 `b` → 解析为 `b`。
  - `DirectOptionsRegistration_ReplacesBuilderOptionsWholesale`：`mvp.Toasts.Duration = 4500`，`services` 注册 `new ToastHostOptions { Gap = 3 }` → 解析出的 options `Duration == 3000` 且 `Gap == 3`。
  - `FactoryOverload_ReceivesProvider`：`UseDialogs(sp => new Fake(sp.Resolve<IFoo>()))`，`IFoo` 在 `services` 中注册 → 构造成功且拿到 `IFoo`。
  - `ChangeTrackingFor_RegistersTypePolicy`：`mvp.ChangeTracking.For(policy)` 后 `TrackChanges` 使用它（复用 Task 3 的测试 Presenter）。
  - `Actions_Configure_RegistersDispatcherConfigurer`、`Actions_StrictValidation_SetsStrictMode`。
  - `Configure_AfterLazyDefaultWasCreated_DisposesItAndInstallsNew`（MvpApplication 版本）。
  - `Builder_ModifiedAfterBuild_Throws`。
  - `RegistrationParityTests.SameBuilder_ResolvesSameImplementationTypes_OnBothPaths`：同一份 `framework` 委托分别喂给 `MvpApplication.Configure` 与 `AddWinformsMVP(...).BuildServiceProvider()`，对 `BuildRegistrations()` 中每个服务类型断言两边解析出的实现类型相同。
  - `AddWinformsMVP_PreRegisteredService_Wins`、`AddWinformsMVP_LaterRegistration_Wins`。
- [ ] **Step 2: 运行确认失败** — `WINTEST FullyQualifiedName~WinformsMvpBuilder|FullyQualifiedName~RegistrationParity|FullyQualifiedName~ServiceCollectionExtensions|FullyQualifiedName~BuiltIns`。
- [ ] **Step 3: 实现**。
- [ ] **Step 4: 运行通过**；全量；构建。
- [ ] **Step 5: 提交** — `feat: unified framework configuration (MvpApplication.Configure / AddWinformsMVP builder)`；`codex review`。

---

### Task 8: 示例、模板、文档、CHANGELOG

**Files:**
- Create: `samples/WinformsMVP.Samples/CustomizationDemo/`（`CompanyStyleDialogBackend.cs`：用一个简单自定义 Form 实现 `IMessageDialogBackend`；`BrandToastRenderer.cs`；`CustomizationDemoProgram.cs`：用 `MvpApplication.Configure(framework: ...)` 演示换对话框、renderer、`Toasts.Duration`、`ChangeTracking.UseComparer` 的适配器写法（适配器只写注释示例，不引入第三方包））；在示例启动器加入口
- Create: `wiki/HowTo-Customize-Framework.md`（“想换什么 → 在 `mvp.` 哪里换 / 直接注册哪个接口”一览表，优先级规则，`new ChangeTracker` 与 `TrackChanges` 的边界，两条注册路径对照）；`wiki/_Sidebar.md`、`wiki/Home.md` 加链接
- Modify: `samples/WinformsMVP.Samples/Program.cs`（如使用 `ServiceLocator.Configure`，改为 `MvpApplication.Configure`）、`templates/content/app/**/Program.cs`（改用 `MvpApplication.Configure(framework: mvp => { /* 注释示例 */ }, services: reg => { ... })`）、`templates/content/tests/**`（若有 `IMessageService` mock 的 `ShowToast`）
- Modify 文档：`wiki/Reference-Platform-Services.md`、`Reference-Toast-Notifications.md`、`Reference-ChangeTracker.md`、`Reference-EventAggregator.md`、`Reference-DependencyInjection.md`、`HowTo-Handle-Errors.md`、`Troubleshooting.md`、`Home.md`、`CLAUDE.md`（Services、DI、ChangeTracker、EventAggregator、Toast 各节；删除所有 `*Defaults` 字样）、`CHANGELOG.md` `[Unreleased]`（Added / Changed / Removed + spec §10 迁移表，日文）

- [ ] **Step 1:** `grep -rn "ChangeTrackerDefaults\|ToastDefaults\|DialogDefaults\|ShowToast\|ClearSubscriptions\|MessageServiceToastExtensions" --include=*.cs --include=*.md . | grep -v "/obj/\|/bin/\|docs/superpowers/"` 列出残留，逐一更新（CHANGELOG 迁移表除外）。预期最终输出为空。
- [ ] **Step 2:** 更新示例与模板；`dotnet build winforms-mvp.sln -warnaserror` 通过。
- [ ] **Step 3:** `tests/templates/verify-templates.sh` 通过（Linux）。
- [ ] **Step 4:** 更新 wiki、CLAUDE.md、CHANGELOG。
- [ ] **Step 5:** 全量 `WINTEST`、分析器测试全部通过。
- [ ] **Step 6: 提交** — `docs/samples/templates: unified configuration entry`；`codex review`；最后对整个分支 `codex review --base master`，处理意见。
