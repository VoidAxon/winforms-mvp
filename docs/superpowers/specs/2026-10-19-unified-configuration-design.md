# 设计文档：统一配置入口（去除静态 `*Defaults`）

- 日期：2026-10-19
- 状态：设计草案，待用户确认
- 影响范围：`WinformsMVP.Core`（ChangeTracker、EventAggregator、服务接口、Presenter 基类）、`WinformsMVP`（消息框、Toast、对话框、启动入口）、`WinformsMVP.DependencyInjection`（`AddWinformsMVP`）、示例、模板、wiki、CLAUDE.md
- 讨论过程：与用户讨论 + Codex 两轮审阅（结论已并入本文）

## 1. 背景与动机

作者希望框架“提供默认实现，使用者可以替换”，并且**替换都在一个统一的地方完成**。

现状是替换点分散在四处，且三处是可变静态类：

| 现在的位置 | 内容 | 问题 |
|---|---|---|
| `ChangeTrackerDefaults`（Core） | 全局 `Cloner` / `Comparer` 委托 | 静态可变 |
| `ToastDefaults`（WinformsMVP） | Toast 位置、大小、字体、时长、堆叠、样式、renderer | 静态可变 |
| `DialogDefaults`（Core） | 消息框默认标题、文件/打印对话框标题、错误文案 | 静态可变 |
| `ServiceLocator.Configure(reg => ...)` / `AddWinformsMVP` | 服务注册 | 唯一“正规”的入口 |

可变静态配置作为长期模式的问题（与 Codex 结论一致）：

- “只在启动时设置”只是注释约定，运行中被改也无人知道；已经造成下面 §8 的 bug 1。
- 测试共享全局状态：`ChangeTrackerHookTests` 必须保存/恢复全局值并禁止并行。
- 同一进程内无法有两套配置（多宿主、并行测试）。

另外，替换消息框外观必须重写整个 `IMessageService`（连 Toast 一起），而 `EventAggregator` 根本没有默认注册，使用者要自己 `new` 并注入。

> 注：2026-05-27 的 `changetracker-deep-copy` 设计当时有意选了“全局钩子 + 有 `ICloneable` 就用”。本设计推翻这两点，理由见 §5。

## 2. 目标 / 非目标 / 反目标

**目标**

1. 一个启动入口完成全部框架定制；`mvp.` 的 IntelliSense 能列出所有可替换项。
2. 框架定制与业务服务注册**分两块写**，但进入同一个容器。
3. 不用 M.E.DI（`ServiceLocator` 路径）和用 M.E.DI 两条路径使用**同一个配置类型**、同样的默认值、同样的优先级。
4. 配置在启动后固定；服务在构造时拿到依赖，运行中不再读全局静态。
5. 删除 `ChangeTrackerDefaults`、`ToastDefaults`、`DialogDefaults`。
6. 修复审阅中发现的 3 个 bug（§8）。

**非目标**

- 不删除 `ServiceLocator.Current`：它仍是整个应用的服务根（Presenter 便捷属性、View 端静态工具通过它取服务）。
- 不引入 `Microsoft.Extensions.Options`；Core 仍只依赖 `System.ComponentModel.Annotations`。
- 不做运行中热切换配置（换主题等）。将来需要时另设专门的服务。
- 不做 View 端声明式绑定（另行讨论，见 memory 记录）。

**反目标（做成这样就算失败）**

- **死板**：使用者只能通过框架预设的 `UseXxx()` 定制、不能直接注册接口。直接注册必须永远可用且优先（§3.3）。
- **不定制的人也要写配置**：不替换任何东西的应用，启动代码不应比现在更长。
- **两套配置来源**：服务既查 builder 又查容器，或 builder 的值和容器里的值不一致。

## 3. 统一启动入口

### 3.1 形状

新入口放在 `WinformsMVP` 包（框架设置里有 `ToastRenderer`、`Font` 等 WinForms 类型，Core 引用不到）：

```csharp
// Program.cs —— 不用 M.E.DI
MvpApplication.Configure(
    framework: mvp =>
    {
        mvp.Messages.UseDialogs(new CompanyStyleDialogs());
        mvp.Toasts.Duration = 4500;
        mvp.Toasts.UseRenderer(new BrandToastRenderer());
        mvp.ChangeTracking.UseComparer(new CompareNetObjectsComparer());
        mvp.Events.Use(sp => new MyEventAggregator(sp.Resolve<ILoggerFactory>()));
    },
    services: reg =>
    {
        BillingModule.Register(reg);
        reg.RegisterInstance<IUserRepository>(repo);
    });
```

```csharp
// 用 M.E.DI —— 同一个 WinformsMvpBuilder 类型
services.AddWinformsMVP(viewRegistry, mvp => { /* 与 framework: 相同 */ });
services.AddBilling();            // 业务照常用 AddXxx
provider.UseWinformsMVP();
```

- `MvpApplication.Configure` 内部：新建 `DefaultServiceProvider` → 注册内置默认 → 应用 `framework:` 的选择 → 执行 `services:` → 设置 `ServiceLocator.Current`。
- 两个参数都可省略；不定制时 `MvpApplication.Configure(services: reg => ...)` 与现在的 `ServiceLocator.Configure` 一样短。
- 现有 `ServiceLocator.Configure(reg => ...)` **保留**，作为底层入口（只有内置默认，无 `framework:` 块）。
- 现有 `AddWinformsMVP(viewRegistry)` 保留，等价于不带定制的新重载。

### 3.2 `mvp.` 下的分组

| 分组 | 可做的事 | 落到容器里的东西 |
|---|---|---|
| `mvp.Messages` | `UseDialogs(backend)` / `UseDialogs(sp => ...)`；`DefaultCaption` | `IMessageDialogBackend`、`MessageDialogOptions` |
| `mvp.Toasts` | 时长、位置、大小、字体、间距、最多显示数、透明度、样式、关闭按钮；`UseRenderer(...)` | `ToastHostOptions`、`ToastRenderer` |
| `mvp.Dialogs` | 文件/文件夹/打印对话框默认标题、默认过滤器、错误文案 | `DialogProviderOptions` |
| `mvp.ChangeTracking` | `UseComparer(...)`、`UseCloner(...)`、`For<T>(policy)` | `IObjectContentComparer`、`IObjectCloner`、`ChangeTrackingPolicy<T>` |
| `mvp.Events` | `Use(instance)` / `Use(sp => ...)` | `IEventAggregator` |
| `mvp.Logging` | `Use(loggerFactory)` | `ILoggerFactory`（现有，纯快捷写法） |
| `mvp.Actions` | `Configure(d => d.Use(...))`、`StrictValidation()` | `IDispatcherConfigurer`（现有，纯快捷写法） |

每个 `UseXxx` 都同时提供“传实例”和“传工厂 `Func<IServiceProvider, T>`”两种重载，让替换实现能取到其他服务（例如主题服务）。

### 3.3 优先级

```
services 块（或 M.E.DI 中）直接注册的接口  >  framework 块的选择  >  框架内置默认
```

- `framework:` 只是**注册的快捷写法**：先在 builder 内部把“builder 的选择”与“内置默认”合并成一份最终的框架注册（builder 选择覆盖内置默认），再把这份注册写进容器；执行完 builder 即失效。运行时只读容器。**不是**先注册内置默认、再让 builder“缺失时补充”——那样 builder 的选择永远盖不过默认。
- 不用 M.E.DI：写入合并后的框架注册，然后执行 `services:` 回调，后注册者胜（`DefaultServiceProvider` 现有语义）。
- M.E.DI：`AddWinformsMVP` 对合并后的框架注册使用 `TryAdd`，调用前用户的注册保留；调用后的注册按 M.E.DI 规则（最后注册者胜）覆盖。
- 两条路径共用同一段“把 builder 转成注册”的逻辑和同一组校验，并用一致性测试覆盖（同一份配置在两条路径上解析出相同的服务）。
- options 对象按**整体替换**处理（我的决定）：如果 `services:` 中直接注册了一个 `ToastHostOptions`，它整体取代 `mvp.Toasts.*` 设置的值，不做逐字段合并。这与“直接注册优先”一致，也避免两处配置逐字段混合后难以判断最终值。文档写明：要么用 `mvp.Toasts.*`，要么自己注册整个 options。

### 3.5 服务根被替换时

- `ServiceLocator.Current` 的 setter 不释放旧的服务根（它不知道旧的是谁创建的，可能仍被别处使用）。
- `MvpApplication.Configure` 应该在任何 Presenter 或 View 端工具访问 `ServiceLocator.Current` 之前调用；如果在它之前已经惰性创建过默认服务根，`Configure` 会释放那个由框架自己惰性创建的默认根（我的决定：它只可能是框架建的，不会有别人持有），然后安装新的。
- 测试中的 `ServiceLocator.Reset()` 同样释放框架惰性创建的默认根。

### 3.4 View 映射

`IViewMappingRegister` 仍作为显式参数/现有方式提供，并注册进同一个容器；不在 `mvp.` 下另建一份映射表。`AddWinformsMVP` 现有的“重复注册不同实例即报错”规则保留。

## 4. 配置对象的约定（全框架统一）

| 机制 | 用途 |
|---|---|
| 普通 options 类（`ToastHostOptions`、`MessageDialogOptions`、`DialogProviderOptions`） | 纯数据配置，注册进容器 |
| 接口（`IMessageDialogBackend`、`IObjectCloner`……） | 有行为、可替换的部分 |
| 方法参数 / 构造参数 | 单次调用、单个对象的覆盖（如 `ToastOptions`、显式 `ChangeTrackingPolicy<T>`） |
| 可变静态 `*Defaults` | **删除**；不再新增 |

- 服务在构造时拿到 options，校验后复制成内部只读快照；之后不再读外部对象。
- 内置服务的默认注册改为**惰性工厂**（现在 `WinFormsPlatformServices` 直接 `new MessageService()`，改为构造注入后必须用工厂，否则用户后注册的 options/backend 来不及生效）。
- 资源所有权：传实例 `Use(instance)` 由调用方负责释放；工厂创建的由容器负责（`DefaultServiceProvider` 需补充对 `IDisposable` 单例的释放，见 §9）。

## 5. ChangeTracker

### 5.1 可替换的比较器与复制器

Core 新增：

```csharp
public interface IObjectCloner          { object DeepClone(object value); }
public interface IObjectContentComparer { bool ContentEquals(object left, object right); }

public sealed class ChangeTrackingPolicy<T> where T : class
{
    public ChangeTrackingPolicy(Func<T, T> clone, Func<T, T, bool> contentEquals);
}
```

内置实现分别包装现有 `ObjectCloner.DeepCopy` / `ObjectComparer.ContentEquals`，默认比较语义（不使用实体自己的 `Equals`）**不变**。第三方库（CompareNETObjects、Force.DeepCloner 等）通过使用者自己写几行适配器接入；框架不依赖它们。

### 5.2 使用方式：Presenter 上的方便方法

```csharp
// PresenterBase（protected）
protected ChangeTracker<T> TrackChanges<T>(T value, ChangeTrackingPolicy<T> policy = null) where T : class;
```

- 从该 Presenter 的 `Services` 解析，优先级：显式 `policy` → 容器中注册的 `ChangeTrackingPolicy<T>`（按声明类型 `T` 精确匹配） → 容器中的 `IObjectCloner` / `IObjectContentComparer`。
- 策略在创建时固定，之后不再解析。
- 返回普通对象，不注册进容器。

直接 `new` 仍然可用：

```csharp
new ChangeTracker<Order>(order);          // 只用框架内置引擎，不受启动配置影响
new ChangeTracker<Order>(order, policy);  // 显式策略
```

现有构造函数的去向（我的决定）：

- 保留 `ChangeTracker(T, Func<T,T,bool> comparer)` 和 `ChangeTracker(T, IEqualityComparer<T>)`：只换比较、复制用内置引擎。
- 新增 `ChangeTracker(T, ChangeTrackingPolicy<T>)`；原来 `comparer = null` 的可选参数改为不带参数的单独重载，避免 `new ChangeTracker<T>(x, null)` 产生重载歧义。
- `TrackChanges` 同样提供只传比较函数的重载 `TrackChanges(value, Func<T,T,bool> comparer)`（复制用容器中解析出的复制器），现在传比较函数的代码迁移时不必额外写复制函数。

**边界（写进文档）**：直接 `new` 的 tracker 不受启动配置影响；要用应用配置就用 `TrackChanges`。若要让 `new` 也受影响，就得退回全局静态，与本设计冲突。

不公开 `IChangeTrackerFactory`（Codex 曾提议）：它要求使用者先注入工厂才能创建 tracker，与“不要死板”冲突。内部共用创建逻辑即可，将来非 Presenter 场景确有需要再公开。

### 5.3 去掉 `ICloneable` 的自动优先

现在：根对象实现 `ICloneable` 就直接调用其 `Clone()`；内置 cloner 遇到实现 `ICloneable` 的嵌套对象也直接信任。但 .NET 不保证 `Clone()` 是深拷贝。例如 `ChangeTracker<Item[]>` 走 `Array.Clone()`（浅拷贝），快照与当前值共享元素，修改元素检测不到。

改为：一律使用解析出的复制器；`ICloneable` 不再特殊对待。使用者确认自己的 `Clone()` 是深拷贝时，可以显式注册为该类型的策略：

```csharp
mvp.ChangeTracking.For(new ChangeTrackingPolicy<Order>(o => (Order)o.Clone(), (a, b) => ...));
```

内置 `ObjectCloner` 对嵌套 `ICloneable` 的信任也一并去掉；原先只能靠 `ICloneable` 复制的类型（报错信息里提示“Implement ICloneable”的那些），报错信息改为提示注册策略或替换复制器。

## 6. EventAggregator

- 两条注册路径都**默认注册为单例**（每个容器一个），可通过 `mvp.Events.Use(...)` 或直接注册替换。
- 从 `IEventAggregator` 删除 `ClearSubscriptions()`。它只为测试存在，而且只清字典、不释放订阅；测试改为各自新建实例。
- 默认实现实现 `IDisposable`：释放时清除全部订阅、拒绝新订阅、丢弃尚未执行的排队回调。由容器在应用结束时释放。
- **投递线程改为“每次订阅时的 `SynchronizationContext.Current`”**。原因：改成惰性默认注册后，第一次解析发生在哪个线程不可预测，不能再由构造时的线程决定所有订阅者的投递目标。Presenter 都在 UI 线程订阅，行为与现在一致。
- 接口文档写明替换实现必须遵守的语义：强引用、token 退订、过滤器在哪个线程执行、跨线程异步投递、异常隔离、退订后排队消息不再投递。
- MVP017 识别方式不变；补一个“自定义实现也会触发诊断”的测试。

## 7. 消息框与 Toast：拆成两个服务

### 7.1 接口

```csharp
// Core
public interface IMessageService      // 只剩 6 个对话框方法，删除 ShowToast
{
    bool ConfirmYesNo(string text, string caption = "");
    bool ConfirmOkCancel(string text, string caption = "");
    ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
    void ShowInfo(string text, string caption = "");
    void ShowWarning(string text, string caption = "");
    void ShowError(string text, string caption = "");
}

public interface IToastService
{
    void Show(string text, ToastType type, int? duration = null);   // null = 用 ToastHostOptions.Duration
}
```

Presenter 新增便捷属性 `protected IToastService Toasts`。`IToastService.Show` 可以从任意线程调用（与现在的 `ShowToast` 相同），但 Toast 窗口**必须**在有消息循环的 UI 线程上创建：

- 现有 `UiThread.Run` 在没有窗口、句柄已销毁等情况下会退回到调用线程执行（`UiThread.cs:18,35`），后台线程上创建的 Toast 窗口和 WinForms Timer 无法正常关闭。新实现不得退回调用线程。
- `WinFormsToastService` 绑定一个明确的 UI 调度目标（在 UI 线程上首次使用时捕获的 `SynchronizationContext`，或启动入口提供的主窗口）。
- 消息循环开始前、或应用关闭后调用 `Show`：记录日志并忽略，不抛异常（我的决定：Toast 是非关键反馈，不应让后台任务失败）。

### 7.2 只换对话框外观：`IMessageDialogBackend`

```csharp
// WinformsMVP
public interface IMessageDialogBackend
{
    MessageDialogResult Show(MessageDialogRequest request);   // 请求：文本、标题、按钮组、图标；结果：按下的按钮
}
```

- 默认 `MessageService` 负责 6 个语义方法、默认标题（`MessageDialogOptions.DefaultCaption`）和结果转换，通过构造注入拿到 backend。
- 默认 backend 用原生 `MessageBox`；使用者实现一个方法即可换成自己的 Form 或第三方 UI 库。
- 仍可整体替换 `IMessageService`。
- `AnchoredMessageBox`（View 端静态工具，现在直接调用 `MessageBox.Show`）也改为走同一个 backend（我的决定：目标是“一处换掉对话框外观”，锚点对话框不应例外）。为此 `MessageDialogRequest` 带上可选的所属窗口和显示位置；backend 不支持定位时可以忽略位置。空标题时也使用 `MessageDialogOptions.DefaultCaption`。
- 不采用“把 `MessageService` 方法改成 `virtual`”：继承重写不能清楚地区分“换外观”和“换整个服务”这两种定制范围。

### 7.3 Toast 两层定制

- **只换外观**：继续派生现有的 `ToastRenderer`（`mvp.Toasts.UseRenderer(...)`）。`ToastStyle` 枚举保留为内置样式的快捷选择。
- **换掉整个通知方式**（系统通知、第三方组件）：实现 `IToastService`。
- WinForms 包新增 `IWinFormsToastService : IToastService`，带 `Show(string, ToastType, ToastOptions)` 和 `ShowAnchored(string, ToastType, Point anchor, ToastOptions)`（`ToastOptions` 含 `Font` 等 GDI+ 类型，只在 View 端使用）。锚点 Toast 保留现有“独立的一个锚点槽位”语义（新的锚点 Toast 替换旧的，不参与角落堆叠）。默认实现 `WinFormsToastService` 同时注册为两个接口。
- 删除 `MessageServiceToastExtensions`（它检查具体类型，换掉服务后会悄悄降级）。
- 替换范围（我的决定）：`mvp.Toasts.Use(...)` 接收 `IWinFormsToastService`，同时注册为两个接口，Presenter 端和 View 端用同一套通知。只直接注册 `IToastService` 的话，只有 Presenter 的 `Toasts` 被替换，View 端（含锚点 Toast）仍用内置实现；文档写明这一点，想全部替换就注册 `IWinFormsToastService`。
- `ToastManager` 从静态类改为 `WinFormsToastService` 持有的实例（配置和存活 Toast 的管理归同一个对象）。
- `AnchoredToast`（View 端静态工具）保留静态调用方式，内部转发到 `ServiceLocator.Current` 解析出的 `IWinFormsToastService`，不再有自己的全局状态。
- 调用级 `ToastOptions`（含 `Font`）在 `Show` **返回之前**同步复制（字体也复制一份），之后由 Toast 负责释放。不能等到派发到 UI 线程后再复制：后台调用返回、调用方 `using` 结束时，UI 回调可能还没开始。派发失败（如 §7.1 的忽略情况）时由服务立即释放这份复制。

### 7.4 `DialogDefaults` 的去向

拆为 `MessageDialogOptions`（默认标题）与 `DialogProviderOptions`（文件/文件夹/打印对话框标题、默认过滤器、各类错误文案），由 `MessageService` / `DialogProvider` 构造注入。Core 中 `OpenFileDialogOptions` / `SaveFileDialogOptions` 目前在构造时读 `DialogDefaults`，`FolderBrowserDialogOptions.Description` 则写死为 `"Select Folder"`（`FolderBrowserDialogOptions.cs:5`），都会盖住全局配置。统一改为：未设置的字段为 `null`，由 `DialogProvider` 用自己的 options 补齐。

## 8. 一并修复的 bug

1. **ChangeTracker 策略未在构造时固定**：注释说“构造时确定一次”，但 `BuildComparerFunc` / `BuildCloneFunc` 返回的 lambda 每次调用都重新读 `ChangeTrackerDefaults`（`ChangeTracker.cs:263-274`）。由 §5.2 的“创建时固定”解决。
2. **`ICloneable` 浅拷贝导致漏检修改**（如根数组）。由 §5.3 解决。
3. **Toast 默认时长不生效**：`IMessageService.ShowToast(..., int duration = 3000)` 的默认值写死，`ToastDefaults.Duration` 永远用不上（`MessageService.cs:55`）。由 §7.1 的 `int? duration = null` 解决。

## 9. 待实现时处理的细节（实现者自行决定）

- `DefaultServiceProvider` 目前不释放单例；需要增加 `IDisposable`，释放它自己用工厂创建的单例（不释放 `RegisterInstance` 传入的实例）。`MvpApplication` 在 `Application.Run` 结束后由使用者 `Dispose`，或提供 `MvpApplication.Run(mainForm)` 包装——二选一，实现者决定。
- builder 内部如何同时写入 `IServiceRegistry` 与 `IServiceCollection`（例如收集成一组注册描述再分别应用）。
- options 的校验规则与报错文字（大小、时长、透明度范围等）。
- 各 options 类的具体属性名（沿用现有 `ToastDefaults` / `DialogDefaults` 的属性名为宜）。

## 10. 不兼容变更与迁移（写入 CHANGELOG `[Unreleased]`）

| 旧 | 新 |
|---|---|
| `ChangeTrackerDefaults.Cloner/Comparer = ...` | `mvp.ChangeTracking.UseCloner/UseComparer(...)`，Presenter 中用 `TrackChanges(model)` |
| model 实现 `ICloneable` 自动被使用 | 不再自动使用；需要时 `mvp.ChangeTracking.For(policy)` |
| `ToastDefaults.Xxx = ...` | `mvp.Toasts.Xxx = ...` |
| `DialogDefaults.Xxx = ...` | `mvp.Messages.DefaultCaption` / `mvp.Dialogs.Xxx` |
| `Messages.ShowToast(...)` | `Toasts.Show(...)`（View 端带样式：`IWinFormsToastService`） |
| `messages.ShowToast(text, type, ToastOptions)` 扩展方法 | `IWinFormsToastService.Show(text, type, options)` |
| `new EventAggregator()` 自己注入 | 默认已注册；构造函数注入 `IEventAggregator` 即可 |
| `IEventAggregator.ClearSubscriptions()` | 删除（测试中新建实例；应用结束时容器释放） |

## 11. 配套工作

- 测试：重写 `ChangeTrackerHookTests`（不再需要串行与全局恢复）；两条注册路径一致性测试；优先级测试；`TrackChanges` 策略解析测试；`Item[]` 等浅拷贝回归测试；Toast 默认时长回归测试；EventAggregator 释放与投递线程测试；MVP017 自定义实现测试。
- 示例：更新使用 `ShowToast`（7 个文件）、`ClearSubscriptions`、`new EventAggregator()` 的示例；新增一个“统一配置”示例，演示换对话框外观、Toast renderer、第三方比较器适配。
- 模板：`winformsmvp` 应用模板的 `Program.cs` 改用 `MvpApplication.Configure`，`framework:` 块留注释示例。
- 文档：新增 wiki 页 `HowTo-Customize-Framework`（“想换什么，在哪里换”一览）；更新 Reference-Platform-Services、Reference-ChangeTracker、Reference-EventAggregator、Reference-DependencyInjection、CLAUDE.md、CHANGELOG。
- 每个提交后按惯例运行 `codex review --commit <sha>`。
