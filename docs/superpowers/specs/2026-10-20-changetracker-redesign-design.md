# 设计文档：ChangeTracker 重新设计（只做变更检测）

- 日期：2026-10-20
- 状态：设计已与用户确认（API 名称、两种写法、删除事件与取回照片的方法）
- 讨论：与用户讨论 + Codex 设计评审（结论已并入）
- 影响范围：`src/WinformsMVP.Core/Common/ChangeTracker.cs`、`PresenterBase.TrackChanges`、EmailDemo / CustomizationDemo、wiki（Reference-ChangeTracker、HowTo-Handle-Window-Closing、Tutorial）、使用指南、CLAUDE.md、CHANGELOG

## 1. 背景

作者对 ChangeTracker 的本意：

- **只做变更检测**。数据归 Presenter，tracker 从不作为数据源。
- 核心用法是**拍照 + `IsChanged`**：创建时拍一张照片（基准），保存后 `AcceptChanges()` 重新拍照，需要时问 `IsChanged`。
- `IsChangedWith(other)` 是为**另一种常见写法**准备的正式用法：有些人不在同一个 Model 上修改，而是每次取值时都从画面 new 一个 Model（`new Order(View.Name, ...)`），这时拿新建的 Model 和照片比较。

因此支持两种写法，各有固定的一套用法（§3.1）。

现在的实现和本意矛盾：

| 问题 | 现状 |
|---|---|
| 看不到 Presenter 的修改 | 构造函数把传入对象**复制两份**（当前值、基准），Presenter 改自己的对象，tracker 看不到；必须调用 `UpdateCurrentValue` 或改用 `IsChangedWith` |
| 两套所有权语义 | 构造时复制，`UpdateCurrentValue` 却只存引用 |
| tracker 像数据容器 | 公开 `CurrentValue`（虽然在 IntelliSense 里隐藏），文档和示例曾直接读写它 |
| 同名方法结果不同 | `AcceptChanges()` 一定变为无改动；`AcceptChanges(T)` 同时替换基准和当前值 |
| 假装能撤销 | 实现了 `IRevertibleChangeTracking.RejectChanges()`，但只是换掉内部副本，不恢复 Presenter 的对象；EmailDemo 实际只拿它来"清掉改动标记" |
| 锁保护不了被比较的数据 | 内部有锁，但被比较的是外部对象，UI 线程随时在改 |
| 异步保存的竞态要手写 | EmailDemo 手写"保存期间画面没变才接受" |

## 2. 设计原则

1. tracker 的状态只有：**被跟踪对象的引用 + 基准照片 + 固定的比较方式 + 忽略设置**。
2. 始终成立：`IsChanged == !ContentEquals(基准, 当前对象, 忽略设置)`。不存在可以单独设置的"改动标记"。
3. 改回原值就是无改动（比较的是内容，不是"是否编辑过"）。
4. 不负责恢复数据、不负责保存、不负责关窗决策。

## 3. API

```csharp
public sealed class ChangeTracker<T> : IChangeTracking where T : class
{
    // 记住 value 这个对象本身（不复制），并给它拍照作为基准。
    public ChangeTracker(T value);
    public ChangeTracker(T value, ChangeTrackingPolicy<T> policy);
    public ChangeTracker(T value, Func<T, T, bool> comparer);
    public ChangeTracker(T value, IEqualityComparer<T> comparer);

    // 写法 1（同一个对象）
    public bool IsChanged { get; }                 // 跟踪的对象 vs 照片，每次读取时比较
    public void AcceptChanges();                   // 重新给跟踪的对象拍照 → 之后 IsChanged == false

    // 写法 2（每次 new）
    public bool IsChangedWith(T value);            // 传入的对象 vs 照片，无副作用
    public void AcceptChanges(T value);            // 给 value 拍照作为新照片 → 之后 IsChangedWith(value) == false

    // 已有的画面级忽略，不变
    public ChangeTrackingIgnores Ignores { get; }
    public ChangeTracker<T> Ignore(params Expression<Func<T, object>>[] members);
    public ChangeTracker<T> Ignore<TOwner>(params Expression<Func<TOwner, object>>[] members);
    public ChangeTracker<T> IgnoreMember(params string[] names);
    public ChangeTracker<T> IgnoreType<TType>();

}
```

`TrackChanges(value[, policy | comparer])` 的解析顺序（显式策略 → 注册的类型策略 → 注册的引擎）不变。

### 3.1 两种写法

**写法 1：一直使用同一个 Model 对象**

```csharp
_tracker = TrackChanges(_order);      // 记住 _order 本身并拍照
_order.Name = View.Name;              // 原地修改
if (_tracker.IsChanged) ...
_repository.Save(_order);
_tracker.AcceptChanges();             // 重新拍照
```

**写法 2：每次从画面 new 一个 Model**

```csharp
_tracker = TrackChanges(initialOrder);          // 只用来拍第一张照片
if (_tracker.IsChangedWith(ReadOrder())) ...
var saved = ReadOrder();
_repository.Save(saved);
_tracker.AcceptChanges(saved);                  // 保存的内容作为新照片
```

| | 判断 | 确认（重新拍照） | 保证 |
|---|---|---|---|
| 写法 1 | `IsChanged` | `AcceptChanges()` | 之后 `IsChanged == false` |
| 写法 2 | `IsChangedWith(x)` | `AcceptChanges(x)` | 之后 `IsChangedWith(x) == false` |

- 写法 2 里**不要用 `AcceptChanges()`**：它会重新拍最初那个已过时的对象，下一次 `IsChangedWith` 会报告改动。文档和 XML 注释写清楚。
- 写法 1 的异步保存：保存前做一份独立的保存数据，成功后 `AcceptChanges(保存的数据)`；之后 `IsChanged` 若仍为 `true`，表示保存期间又有了修改。`AcceptChanges(x)` 确认的是 x，不是跟踪的对象。

### 删除 / 改名

| 旧 | 新 | 理由 |
|---|---|---|
| `CurrentValue` | 删除 | Presenter 已经持有对象，再提供入口会让 tracker 慢慢变回数据容器 |
| `UpdateCurrentValue(T)` | 删除 | 原本为"每次 new"的写法准备，该写法改用 `IsChangedWith`；写法 1 里换成另一份数据时新建 tracker |
| `AcceptChanges(T)` | 保留名称，含义改为"只给 x 拍照作为新照片" | 原来同时替换当前值和照片；现在当前值归 Presenter，只更新照片。与 `IsChangedWith(x)` 成对 |
| `RejectChanges()`、`IRevertibleChangeTracking` | 删除 | 恢复对象图（集合、引用、只读属性、绑定、校验）是业务逻辑，不是检测器的职责 |
| `GetOriginalValue()` | 删除 | 主要用途是撤销修改，而撤销不在 tracker 的职责内；需要原值的画面由 Presenter 自己保留（或重新读取） |
| `IsChangedChanged` 事件 | 删除 | 见 §6 |

## 4. 各种情况的语义

| 情况 | 行为 |
|---|---|
| Presenter 原地修改对象 | `IsChanged` 立即反映 |
| Presenter 把变量换成另一个对象（`_order = Load()`） | tracker 仍跟踪旧对象，应新建 tracker |
| 每次从画面 new 一个 Model 的写法 | 写法 2：`IsChangedWith(新实例)` |
| 保存成功 | 写法 1：`AcceptChanges()`；写法 2：`AcceptChanges(保存的内容)` |
| 异步保存期间用户继续编辑 | 保存前做一份独立的保存数据，成功后 `AcceptChanges(保存的数据)`；保存期间的编辑仍然算改动 |
| 异步保存期间用户改回了原值 | 仍然有改动（磁盘上已是保存的内容），`AcceptChanges(x)` 能正确判断；"保存期间改过就不更新基准"的做法会判断错 |
| 放弃修改并关闭 | 写法 1：`AcceptChanges()`；写法 2：`AcceptChanges(ReadOrder())`；然后 `RequestClose`，满足"Push 方向关窗前先清掉改动"的约定。含义是"结束这段差异的追踪"，不是"已保存" |
| 撤销修改 | 不在 tracker 的职责内。需要撤销的画面由 Presenter 自己保留一份原值（或重新从仓储读取），再显示到画面并新建 tracker |
| 根对象是 `List<T>` | 支持；原列表的增删改能检测到 |
| `record`、不可变类 | 用写法 2 |
| 根是值类型 | 不支持（保持 `where T : class`）；值类型没有"引用"可以跟踪，需要时包进一个编辑状态类 |

## 5. 线程

删除内部锁。约定：**一个 tracker 和它跟踪的对象要串行使用**；WinForms 里就是全部在 UI 线程上操作。

- 锁只能保护 tracker 自己的字段，保护不了 Presenter 正在修改的对象，保留它只会让人误以为"线程安全"。
- 后台线程只拿独立的保存数据；完成后回到 UI 线程再 `AcceptChanges(x)`。
- 全局共用的复制 / 比较引擎仍需支持多个 tracker 并发使用（现状已满足）。

## 6. 改动通知：删除 `IsChangedChanged`

- 原地修改对象时 tracker 无法得知，事件不可能自动触发；写法 2 里 tracker 根本不知道当前值。
- 画面变化时 Presenter 本来就要调用 `RaiseCanExecuteChanged()`（按钮条件不只看有没有改动），在同一处直接读 `IsChanged` / `IsChangedWith` 即可：
  ```csharp
  private void OnDataChanged(object s, EventArgs e)
  {
      ApplyViewTo(_order);                 // 写法 1；写法 2 则 var dirty = _tracker.IsChangedWith(ReadOrder());
      View.IsDirty = _tracker.IsChanged;
      Dispatcher.RaiseCanExecuteChanged();
  }
  ```
- `AcceptChanges` 之后要自己刷新一次 `View.IsDirty`。
- 讨论过但不采用：保留事件并加 `DetectChanges()`（Codex 的建议；多一个概念，能省的代码很少）；"确认时"事件携带旧 / 新确认内容（目前没有实际用途）。需要时可以再加，不影响现有 API。

## 7. 示例与文档

- **EmailDemo**（写法 2）：每次从画面读出只含 To / Subject / Body 的 `EmailEditState`（发送用的 `EmailMessage` 还会补上日期、发件人等字段，直接拿它当基准会把这些差异也算进去），用 `IsChangedWith` 判断；异步保存草稿后 `AcceptChanges(保存的内容)`，删除手写的竞态判断；"放弃"改为 `AcceptChanges(当前画面的内容)`。
- **CustomizationDemo**（写法 2，`IsChangedWith`）；**wiki / 使用指南**：两种写法并列介绍，各自配一个完整例子。
- 修正 wiki `Reference-ChangeTracker.md` 中"Push 关闭会跳过 `CanClose`"的错误描述（与 CLAUDE.md 的约定矛盾）。
- CHANGELOG 记为不兼容变更（未发布，没有使用者受影响）。

## 8. 测试

- `Constructor_CreatesIndependentCopy` 改为：基准独立于原对象；修改原对象后 `IsChanged == true`。
- 新增（写法 1）：原地修改后 `IsChanged`；`AcceptChanges` 后无改动；`AcceptChanges(x)` 后保存期间的编辑仍是改动；保存期间改回原值仍是改动；根为 `List<T>` 的增删改。
- 新增（写法 2）：`IsChangedWith(新实例)`；`AcceptChanges(x)` 后同内容的新实例无改动；`IsChangedWith` 不改变任何状态。
- 照片独立于跟踪的对象（修改对象不影响照片；`AcceptChanges(x)` 后再修改 x 不影响照片）。
- 删除 `RejectChanges`、`CurrentValue`、`UpdateCurrentValue`、`GetOriginalValue`、`IsChangedChanged` 相关测试。
- 都在 Core，可以在 Linux 上运行。
