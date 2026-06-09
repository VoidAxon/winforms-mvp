# 设计 spec：`ISelectionStore<T>` + `Cascade`（级联选择原语）

- 日期：2026-06-09
- 状态：待评审
- 范围：向框架新增两个公共原语，用于简化「主从联动 / N 层级联选择」的实现，消除手写 N 个选择 Service 的样板与「忘记清空下层」的结构性 bug。

---

## 1. 问题

业务应用常见「上层选择决定下层列表」的级联：分类→子分类→商品、国→省→市、组织→部门→人。当前框架推荐的做法（[HowTo-Communicate-Between-Presenters] 的「共享 Service + 事件」按层串联）在 N 层时有两个痛点：

1. **重复**：每层一个同型的 `IXxxSelectionService`（只是类型参数不同），接口 / 实现 / DI 注册都要手写 N 份——本应泛型化。
2. **易漏**：每层 Presenter 都要手写「上层变了 → 清空自己的选择」。漏一处就留下 stale 选择，下层用旧 key 查询，界面数据对不上，且评审难发现。

## 2. 目标 / 非目标

**目标**
- 用 **1 个泛型** `ISelectionStore<T>` 取代 N 个同型选择 Service。
- 用 **1 行声明** `Cascade.Bind(...)` 取代每层「订阅上层 → 清空自己 → 重载」三步，并让「清空下层」通过通知链**自动级联**，结构上不可能漏。
- 保持框架既有原则：仍是「共享 Model + 事件」，不引入 presenter-holds-presenter，不引入协调者 Presenter。
- **net40 兼容、零外部依赖**。

**非目标（YAGNI）**
- 不做通用响应式 / observable 框架（不引入 Rx）。
- 不在首版做异步重载（同步内存仓库优先；异步作为将来可加的重载，见 §8）。
- 不处理**编译期未知深度**的动态层级（如自递归文件树）；保留为逃生方案，见 §9。
- 不做数据绑定引擎。

## 3. API 设计

放置位置：`src/WinformsMVP/Common/`（与 `ChangeTracker` 同层，属于通用状态原语）。命名空间 `WinformsMVP.Common`。

### 3.1 `ISelectionStore<T>` / `SelectionStore<T>`

```csharp
// One generic selection holder replaces N hand-written selection services.
public interface ISelectionStore<T> where T : class
{
    T Current { get; }
    void Select(T item);              // Select(null) clears the selection
    event EventHandler CurrentChanged;
}

public sealed class SelectionStore<T> : ISelectionStore<T> where T : class
{
    private readonly IEqualityComparer<T> _comparer;

    // Defaults to EqualityComparer<T>.Default. Give entities Id-based Equals (or pass a
    // comparer) so a reloaded list — which holds NEW instances — still matches "the same" row.
    public SelectionStore(IEqualityComparer<T> comparer = null)
    {
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public T Current { get; private set; }

    public void Select(T item)
    {
        // Short-circuit when unchanged so clearing an already-empty downstream level does not
        // fan out a pointless cascade. Null-safe: never calls the comparer with a null operand.
        bool same = (Current == null && item == null)
                 || (Current != null && item != null && _comparer.Equals(Current, item));
        if (same) return;

        Current = item;
        var handler = CurrentChanged;
        if (handler != null) handler(this, EventArgs.Empty);
    }

    public event EventHandler CurrentChanged;
}
```

- 默认比较器 `EqualityComparer<T>.Default`；可注入自定义 `IEqualityComparer<T>`。
- ⚠️ **判等按 Id、不靠引用**：父一变，下层列表会被重新查询、换成全新实例。靠引用判等会让逻辑上「同一条」记录因引用不同而对不上。实体应按 Id 实现 `Equals`/`GetHashCode`，或在构造时传入按 Id 比较的 comparer。（额外好处：值判等下，重载后重选到 Id 相同的新实例会被短路，顺带压住 §5.1 的一部分回灌。）

### 3.2 `Cascade`

```csharp
// Wires parent->child selection cascades in one declaration.
public static class Cascade
{
    // When `from` changes: clear `target` (whose CurrentChanged cascades further down), then
    // reload `target`'s list from the new parent value. initialSync: also reload once at bind
    // time, so a child bound after its parent already has a value is not left empty.
    public static IDisposable Bind<TParent, TChild>(
        ISelectionStore<TParent> from,
        ISelectionStore<TChild> target,
        Action<TParent> reload,
        bool initialSync = true)
        where TParent : class
        where TChild : class
    {
        if (from == null) throw new ArgumentNullException("from");
        if (target == null) throw new ArgumentNullException("target");
        if (reload == null) throw new ArgumentNullException("reload");

        EventHandler handler = delegate
        {
            target.Select(null);     // clear self -> fires target.CurrentChanged -> downstream clears next
            reload(from.Current);     // from.Current may be null (parent cleared) -> reload empties
        };
        from.CurrentChanged += handler;
        if (initialSync) reload(from.Current);   // each level self-syncs; order-independent
        return new Unsubscriber(delegate { from.CurrentChanged -= handler; });
    }

    // Multi-parent: `target` depends on BOTH `a` and `b`. Either changing clears `target`
    // and reloads with both current values. (Cascade.Bind is the single-parent case.)
    public static IDisposable Combine<TA, TB, TChild>(
        ISelectionStore<TA> a,
        ISelectionStore<TB> b,
        ISelectionStore<TChild> target,
        Action<TA, TB> reload,
        bool initialSync = true)
        where TA : class
        where TB : class
        where TChild : class
    {
        if (a == null) throw new ArgumentNullException("a");
        if (b == null) throw new ArgumentNullException("b");
        if (target == null) throw new ArgumentNullException("target");
        if (reload == null) throw new ArgumentNullException("reload");

        EventHandler handler = delegate
        {
            target.Select(null);
            reload(a.Current, b.Current);
        };
        a.CurrentChanged += handler;
        b.CurrentChanged += handler;
        if (initialSync) reload(a.Current, b.Current);
        return new Unsubscriber(delegate
        {
            a.CurrentChanged -= handler;
            b.CurrentChanged -= handler;
        });
    }

    private sealed class Unsubscriber : IDisposable
    {
        private Action _dispose;
        public Unsubscriber(Action dispose) { _dispose = dispose; }
        public void Dispose()
        {
            var d = _dispose; _dispose = null;
            if (d != null) d();
        }
    }
}
```

- `initialSync`（默认 `true`）：绑定时立即按当前父值同步一次，消除「绑定时父已有值、子列表却停在空」的陷阱；各层各自初始同步，与绑定顺序无关；且初始同步**只重载、不清空**，所以预填 store 再按序绑定可用于状态恢复。
- `reload` 应自我兜底（内部 try/catch，失败置空 + 提示）：`Cascade` 不回滚，同步 reload 抛异常会沿调用栈冒回，留下半截连锁。
- `Combine` 是 `Bind` 的多父版（两个父）。两个以上父可再加重载或退化为手动订阅；首版只做两父 `Combine`。

## 4. 级联语义（同步 / 深度优先 / 一趟）

选择顶层 `C` 时（3 层为例）：

```
categoryStore.Select(C) -> CurrentChanged
  └ SubCategory 的 Bind:
      ① subStore.Select(null) -> CurrentChanged
           └ Product 的 Bind:
               ① prodStore.Select(null)   // 已是 null 则短路，不再 fire
               ② reload(sub = null) -> 商品列表 = 空
      ② reload(category = C) -> 子分类列表 = C 的列表
```

结果一趟到位：子分类列表刷新、子分类选择清空、商品列表清空、商品选择清空——**各 Presenter 一行清空代码都没写**。同步单线程，中间态不会被 UI 重绘看到。

## 5. 三个必须处理的正确性点（评审补充）

### 5.1 列表重载导致控件「自动选中」→ 反向喂回 store（最大坑）

`View.Items = ...` 重设列表时，WinForms 的 ListBox/DataGridView 可能**自动选中首行并触发选择事件**，经 `View.SelectionChanged → store.Select(autoItem)` 形成非用户意图的二次级联（清空后 Current=null，自动项非 null，`ReferenceEquals` 短路救不了）。

**契约 + 守卫（写入 wiki 与示例）**：
- View 契约：**重载 Items 不得触发「用户选择」事件**。理想是 View 内部区分「用户操作」与「程序重设」。
- 兜底守卫：Presenter 在重载期间设抑制标志：

```csharp
private bool _suppressSelection;

private void ReloadItems(/*...*/)
{
    _suppressSelection = true;
    try { View.Items = /* ... */; }
    finally { _suppressSelection = false; }
}

private void OnUserSelected(SubCategory s)
{
    if (_suppressSelection) return;   // ignore selection raised by repopulation
    _subCategoryStore.Select(s);
}
```

> 该守卫是**使用约定**，不放进 `Cascade`——因为回灌路径经过 View，`Cascade` 看不到。原语保持纯净。

### 5.2 「用户选择 → store」必须显式接线

示例里 `OnViewAttached` 除了设列表，**必须订阅 `View.SelectionChanged` 并调用 `store.Select(...)`**（带 5.1 的抑制判断）。否则照抄者会「点了没反应」（与本仓库此前 sample 的 ViewAction 漏接同类问题）。

### 5.3 store 作用域：按画面隔离，勿全局单例

- composition root 手写 `new SelectionStore<Category>()` 是对的——**每个级联画面一套自己的 store**。
- 若用 DI 开放泛型 `AddSingleton(typeof(ISelectionStore<>), typeof(SelectionStore<>))`，则 `ISelectionStore<Category>` 变成**全应用单例**；两个独立级联画面会共享选择状态而串台。**wiki 必须警告**：选择状态应按画面 / 作用域隔离（scoped/transient 或手写组合），不要无脑全局单例。

## 6. 次要点

- **reload 抛异常**：`target.Select(null)` 先于 `reload(...)`，若 reload 抛异常则下层已清、自己未重载，留半截状态。同步内存场景概率低；在 XML 注释中说明，调用方需保证 reload 不抛或自行兜底。
- `reload(from.Current)`：在 `target.Select(null)` 之后读 `from.Current`，但 `from` 不受清空影响，值正确（即新父值或 null）。注释说明以打消疑虑。

## 7. net40 约束

- 没有 `Array.Empty<T>()`（4.6+）：空列表用 `new T[0]`。
- 没有 `IReadOnlyList<T>`（4.5+）：View 列表属性用 `T[]` / `IList<T>`。
- 匿名委托、泛型、`IDisposable`、`IEqualityComparer<T>` 均 net40 可用。

## 8. 异步的将来扩展（不在首版）

将来遇到慢仓库需异步重载时，**新增**重载而非改签名：

```csharp
public static IDisposable Bind<TParent, TChild>(
    ISelectionStore<TParent> from, ISelectionStore<TChild> target, Func<TParent, Task> reloadAsync)
```

异步版需处理「快速切上层导致旧请求后到」的乱序（记录最新 token、过期结果丢弃，类似 switchMap）。与同步版并存，不破坏既有 API。

## 9. 动态深度（不在首版，保留逃生方案）

编译期未知深度（自递归层级）不适合「每类型一个 `ISelectionStore<T>`」。届时切换为单一 `DrillDownPath`：按层号持有选择，`SelectAt(level, item)` 截断该层以下。更重的抽象，仅当深度动态时使用。本设计的方式是**深度固定、各层类型不同**这一常见场景的默认解。

## 10. 测试策略

`tests/WinformsMVP.Samples.Tests/Common/`：

**`SelectionStoreTests`**
- `Select` 改变值时触发一次 `CurrentChanged`。
- `Select` 同值（`ReferenceEquals` 或注入的 comparer）不触发。
- `Select(null)` 清空 `Current`。
- 注入 `IEqualityComparer<T>` 时按值同等判定。

**`CascadeTests`**
- 父变化 → `target` 被清空（`target.Current == null`）且 `reload` 以 `from.Current` 被调用。
- `initialSync = true`：绑定时立即按当前父值 reload 一次；`initialSync = false`：绑定时不 reload。
- `Dispose()` 后父再变化不再触发。
- 3 层链：选顶层 → 中、末层各被清空并重载（计数，排除 initialSync 的初次）。
- 清空已为 null 的下层时短路（下层 `reload` 不被多余调用）。
- `from.Current` 为 null（父被清）时 `reload(null)` 被调用。
- `Combine`：a 或 b 任一变化 → `target` 清空 + `reload(a.Current, b.Current)`；`Dispose()` 后 a、b 两父都解除订阅。

**集成（Presenter 级，可选）**
- 用 mock View + `SelectionStore` 串 3 层，验证选顶层后三层状态一次到位；验证 5.1 抑制守卫下「重载不回灌」。

## 11. 影响 / 迁移

- **纯增量、无破坏**：新增 `Common/SelectionStore.cs`、`Common/Cascade.cs`，不改既有类型。
- 随 `WinformsMVP` 包发布（核心库，net40;net48）。
- 文档：新增 wiki 页 `HowTo-Handle-Cascading-Selection.md`（用户草稿 + 本 spec 的 5.1/5.2/5.3 修正，日文撰写），并在 `HowTo-Implement-Master-Detail`（N 段版指引）、`HowTo-Communicate-Between-Presenters`、`_Sidebar` 增加链接。
- `samples/WinformsMVP.Samples/CascadeDemo/`（3 层示例）+ SampleLauncher 入口注册 — 端到端验证含 §5.1 抑制守卫。
- CHANGELOG `[Unreleased]` 增「Added：`ISelectionStore<T>` / `SelectionStore<T>` / `Cascade`」。

## 12. 决策记录 / 待确认

1. ✅ **命名**：采用 `ISelectionStore<T>` / `SelectionStore<T>` / `Cascade.Bind`（与 `ChangeTracker` 的角色名词风、`ActionBinder.Bind` 的动词对齐）。
2. ✅ **首版附带 `samples/WinformsMVP.Samples/CascadeDemo/`（3 层示例）** — 端到端验证原语 + §5.1 的「重载不回灌」抑制守卫（该坑只有真实控件能验证）。同时在 SampleLauncher 注册入口。
3. ✅ **命名空间**：`WinformsMVP.Common`（与 `ChangeTracker` 同层）。
4. ✅ **wiki 落地语言**：日文（与现有页面一致；中文草稿为来源，落地译为日文）。
5. ✅ **`Cascade.Combine`（两父多父）纳入 v1**：原语 + 单测 + wiki 一节。CascadeDemo 仍保持线性 3 层（Combine 无新的真实控件风险，由单测 + wiki 覆盖即可）。
6. ✅ **`Cascade.Bind` 加 `initialSync`**（默认 true）；`SelectionStore` 判等改 `EqualityComparer<T>.Default` + 按 Id 指引；presenter 示例含 reload 兜底、双绑守卫、`OnInitialize`/`OnViewAttached` 分工。
