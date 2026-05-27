# 设计文档:`ChangeTracker<T>` 去除 `ICloneable` 强约束

- 日期:2026-05-27
- 状态:设计已确认,待写实现计划
- 影响范围:`src/WinformsMVP/Common/ChangeTracker.cs`、新增深拷贝/深比较引擎、`EqualityHelper`、CLAUDE.md 文档

## 1. 背景与动机

现在的 `ChangeTracker<T>` 声明为:

```csharp
public class ChangeTracker<T> : IChangeTracking, IRevertibleChangeTracking
    where T : class, ICloneable
```

它隐含**两个**对 model 的要求,而不只是 `ICloneable`:

1. **`ICloneable`(深拷贝)** —— 用于快照:构造、`AcceptChanges`、`RejectChanges`、`GetOriginalValue` 都调用 `Clone()`。
2. **值相等性** —— `IsChanged` 通过 `EqualityHelper.Equals(_originalValue, _currentValue)` 比较。`EqualityHelper` 依次尝试 `IEquatable<T>` → `IComparable<T>` → `object.Equals`。如果 model 不重写 `Equals`/不实现 `IEquatable<T>`,最终退化为**引用比较**;由于构造时 `Clone()` 了两份不同实例,`IsChanged` 会**永远返回 true**(即使未编辑)。

实际项目中**很少有人显式实现 `ICloneable`**,且不希望在每个调用点显式传拷贝/比较委托。因此目标是:

- 放宽泛型约束,不强制 model 实现 `ICloneable`。
- 拷贝与比较都改为「**有实现就用、没实现就自动深处理**」。
- 提供一个**全局钩子**,启动时设一次即可全局生效,且能一行换成第三方深拷贝库。
- 不破坏现有已实现 `ICloneable`/`Equals` 的 model 的行为。

## 2. 设计决策总览

| 决策点 | 结论 |
|--------|------|
| 泛型约束 | `where T : class, ICloneable` → `where T : class` |
| 拷贝来源优先级 | `source is ICloneable` → `Clone()` > 全局钩子 `ChangeTrackerDefaults.Clone` |
| 比较来源优先级 | 单实例 `comparer` > 值相等性(`IEquatable`/`IComparable`/`Equals` 重写) > 全局钩子 `ChangeTrackerDefaults.Equals` |
| 全局钩子 | 非泛型静态类 `ChangeTrackerDefaults`,两个 `Func` 委托,默认指向内置引擎,可一行替换 |
| 内置默认引擎 | **缓存反射**深拷贝/深比较(务实档,处理循环引用) |
| 引擎可见性 | 公开(`ObjectCloner.DeepCopy` / `ObjectComparer.DeepEquals`),既作钩子默认值,也可直接调用 |
| 极致性能 | **不**自建表达式树/IL 引擎;需要时通过钩子外挂编译型库(如 Force.DeepCloner),一行切换 |
| 依赖 | 全程 BCL,零新依赖,`net40;net48` 通用 |

## 3. 详细设计

### 3.1 约束放宽

```csharp
public class ChangeTracker<T> : IChangeTracking, IRevertibleChangeTracking
    where T : class
```

保留 `class` 约束:`CurrentValue` setter 用 `ReferenceEquals`、构造与各方法用 null 检查,均依赖引用类型语义。值类型不在本次范围内。

### 3.2 全局钩子 `ChangeTrackerDefaults`

```csharp
namespace WinformsMVP.Common
{
    /// <summary>
    /// ChangeTracker 在 model 未实现 ICloneable / 值相等性时使用的全局默认深拷贝/深比较钩子。
    /// 默认指向内置缓存反射引擎;可在应用启动时一行替换为第三方库。
    /// 约定:在应用启动时设置一次(非线程安全的写入)。
    /// </summary>
    public static class ChangeTrackerDefaults
    {
        public static Func<object, object> Clone { get; set; }
            = ObjectCloner.DeepCopy;

        public static Func<object, object, bool> Equals { get; set; }
            = ObjectComparer.DeepEquals;
    }
}
```

**为什么是非泛型静态类**:若把钩子放在 `ChangeTracker<T>` 的静态成员上,会因泛型类型的静态成员「每个封闭类型独立」而变成「每个 `T` 各设一次」,违背「一处设置全局生效」。故钩子用 `object` 签名、放非泛型类。

**换第三方库示例(启动时一行)**:

```csharp
// 例:Force.DeepCloner
ChangeTrackerDefaults.Clone  = o => o.DeepClone();
ChangeTrackerDefaults.Equals = (a, b) => MyDeepComparer.Equals(a, b);
```

### 3.3 `ChangeTracker<T>` 解析逻辑

构造时一次性解析出内部使用的 `Func<T,T> _clone` 与 `Func<T,T,bool> _comparer`:

**拷贝解析(`_clone`)**

```
1. 若 initialValue is ICloneable                → x => (T)((ICloneable)x).Clone()
2. 否则                                          → x => (T)ChangeTrackerDefaults.Clone(x)
```

**比较解析(`_comparer`)**

```
1. 若构造传入 comparer(现有参数)              → 用它
2. 否则若 T 实现 IEquatable<T>/IComparable<T>/重写 Equals → EqualityHelper.Equals
3. 否则                                          → (a, b) => ChangeTrackerDefaults.Equals(a, b)
```

> 第 2 步的「是否重写 `Equals`」检测:`typeof(T)` 实现 `IEquatable<T>`、或实现 `IComparable<T>`、或 `GetMethod("Equals", new[]{ typeof(object) }).DeclaringType != typeof(object)`。检测结果在构造时缓存进每个封闭类型,避免重复反射。

所有原先直接调用 `initialValue.Clone()`、`_currentValue.Clone()`、`EqualityHelper.Equals(...)` 的位置改为调用解析出来的 `_clone` / `_comparer`。

### 3.4 构造函数(保持不变)

两个现有构造原样保留,本次不新增任何构造重载:

```csharp
public ChangeTracker(T initialValue, Func<T, T, bool> comparer = null)
public ChangeTracker(T initialValue, IEqualityComparer<T> comparer)
```

### 3.5 内置缓存反射引擎(务实档)

公开静态类,既作钩子默认值,也可被用户直接调用(满足「想直接用深拷贝」)。

```csharp
public static class ObjectCloner
{
    public static object DeepCopy(object source);
    public static T DeepCopy<T>(T source);     // 泛型便捷重载
}

public static class ObjectComparer
{
    public static bool DeepEquals(object a, object b);
    public static bool DeepEquals<T>(T a, T b);
}
```

**就近递归** —— 每个节点按其运行时类型独立判断:

| 节点类型 | 深拷贝行为 | 深比较行为 |
|----------|-----------|-----------|
| `null` | `null` | 两者皆 null → 相等;其一 null → 不等 |
| 不可变(基础类型、`string`、`enum`、`DateTime`、`decimal`、`Guid`、`TimeSpan` 等) | 原样返回(共享引用安全) | `Equals` |
| 实现 `ICloneable` 的节点 | 调用其 `Clone()` | 见下一行 |
| 重写 `Equals`/实现 `IEquatable` 的节点 | (拷贝按其它规则) | 用其 `Equals`/`IEquatable` |
| 数组 | `Array.CreateInstance` + 逐元素深拷贝 | 维度/长度一致 + 逐元素深比较 |
| `IList`(如 `List<T>`) | 反射建新实例 + 逐项深拷贝 | `Count` 一致 + 按序逐项深比较 |
| `IDictionary` | 反射建新实例 + 逐键值深拷贝 | `Count` 一致 + 逐键值深比较 |
| 其它 POCO(引用类型) | `FormatterServices.GetUninitializedObject` + 逐**字段**深拷贝 | 逐**字段**递归深比较 |
| 委托 / 事件(`Delegate` 派生) | 跳过(拷贝中置 null) | 忽略 |
| 真正不支持(`IntPtr`、`Stream`、指针、句柄类) | 抛 `NotSupportedException`,提示「请实现 `ICloneable`、或替换 `ChangeTrackerDefaults.Clone`(可接第三方深拷贝库)」 | 同左 |

**实现要点**

- **按字段处理**(含自动属性 backing field),与序列化器一致,捕获完整状态(含 private/只读字段)。这是「变更追踪器必须完整还原」的前提。
- **循环引用**:
  - 拷贝:`Dictionary<object, object>`(键用引用相等比较器)记录 `原对象 → 副本`,遇到已访问对象直接返回已建副本。
  - 比较:记录已比较过的引用对,遇到回环判为相等(不再递归)。
  - 需自实现 `ReferenceEqualityComparer : IEqualityComparer<object>`(`ReferenceEquals` + `RuntimeHelpers.GetHashCode`),因为 net40 无内置版本。
- **性能**:每个类型的 `FieldInfo[]`(沿继承链收集 `BindingFlags.Instance | Public | NonPublic`)缓存进 `ConcurrentDictionary<Type, FieldInfo[]>`(net40 可用)。集合/数组的元素类型判断同样可缓存。单次拷贝为缓存后的反射成员访问,适配编辑对话框量级。
- **创建实例**:POCO 用 `FormatterServices.GetUninitializedObject`(`System.Runtime.Serialization`,net40 可用)绕过构造函数;集合类型用 `Activator.CreateInstance`(要求无参构造,`List<>`/`Dictionary<>` 满足)。

### 3.6 `EqualityHelper` 调整

保留现有 `EqualityHelper.Equals<T>` 的叶子语义(`IEquatable` → `IComparable` → `object.Equals`),供 3.3 比较解析第 2 步复用。`ObjectComparer.DeepEquals` 的叶子节点同样复用这套逻辑。不改变 `EqualityHelper` 已有行为。

## 4. 兼容性与行为变化

- ✅ **零变化**:已实现 `ICloneable` + (`Equals`/`IEquatable`/传 comparer) 的 model —— 走优先级前两级,与现状完全一致。
- ⚠️ **唯一行为变化**:**没重写 `Equals`** 的 model。
  - 旧:默认退化引用比较 → 构造后 `IsChanged` **永远 true**(已存在的 bug)。
  - 新:走全局钩子(默认反射深比较)→ `IsChanged` **正确反映是否真的变化**。
  - 这是修复,但属可观察的行为变化,需在 CLAUDE.md / 迁移说明中写明。
- 旧文档(`ChangeTracker.cs` docstring、CLAUDE.md「Change Tracking」「必须实现 `ICloneable` 深拷贝」)更新为:「**推荐**实现 `ICloneable`(快路径);否则自动深拷贝/深比较;或在启动时设置 `ChangeTrackerDefaults` 钩子(可换第三方库)」。

## 5. net40 / 依赖约束确认

| 用到的 API | net40 可用 | 命名空间 |
|------------|-----------|----------|
| 反射 `FieldInfo`/`Type` | ✅ | `System.Reflection` |
| `FormatterServices.GetUninitializedObject` | ✅ | `System.Runtime.Serialization` |
| `ConcurrentDictionary<,>` | ✅ | `System.Collections.Concurrent` |
| `RuntimeHelpers.GetHashCode` | ✅ | `System.Runtime.CompilerServices` |

**零新外部依赖**,主包继续保持 `net40;net48` 多目标。表达式树/IL 不在本次实现内(YAGNI,由钩子外挂解决极致性能)。

## 6. 测试计划

**`ObjectCloner.DeepCopy`**
- 扁平 POCO、嵌套 POCO、`List<T>`/数组/`Dictionary<K,V>`、含 private/只读字段。
- 循环引用(A↔B、自引用)不死循环且副本独立。
- 委托/事件字段被跳过。
- 不支持类型(如含 `Stream` 字段)抛 `NotSupportedException`。
- 副本与原对象互不影响(改副本不动原值)。

**`ObjectComparer.DeepEquals`**
- 同结构相等/某字段不同 → 不等;嵌套差异、集合元素差异、Dictionary 差异。
- 循环引用结构可正常比较。
- 叶子节点尊重 `Equals`/`IEquatable` 重写。

**`ChangeTracker<T>` 优先级**
- 实现 `ICloneable` 时不走全局钩子(可用计数 spy 钩子验证)。
- 替换 `ChangeTrackerDefaults.Clone/.Equals` 后生效。
- 传 `comparer` 覆盖值相等性与钩子。

**回归 & 修复验证**
- 现有 `ChangeTrackerTests` 全绿(已实现 `ICloneable`+`Equals` 的用例不受影响)。
- 新增:**未重写 `Equals`** 的 model,构造后 `IsChanged == false`;改动后 `true`;`RejectChanges` 完整还原(含 private 字段)。

**线程安全**
- 沿用现有 `_lock`;`ChangeTrackerDefaults` 静态属性约定「启动时设置」,测试中按需在 setup 设置/还原。

## 7. 明确不做(YAGNI)

- 不引入 `IDeepCloneProvider`/`IDeepEqualityProvider` 接口 + facade + provider 替换那层抽象(`Func` 钩子已覆盖可扩展性)。
- 不新增 per-instance `cloneFunc` 构造参数(零使用;`ICloneable`(per-type) + 全局钩子(per-app)已覆盖拷贝定制。per-instance 自定义**比较**有真实需求故保留 `comparer`,自定义**拷贝**无,二者不对称是 YAGNI 的合理结果)。
- 不自建表达式树/IL 编译引擎(钩子外挂第三方库即可)。
- 不支持值类型 `T`(维持 `class` 约束)。
- 不支持 `BinaryFormatter`/`DataContractSerializer` 作为默认(前者需 `[Serializable]` + 安全告警;后者只拷公共属性、丢 private/只读状态,对变更追踪不安全)。如需要,用户可自行把序列化实现接到钩子上。

## 8. 待实现阶段微调项

- `ObjectCloner` / `ObjectComparer` / `ChangeTrackerDefaults` 命名可调整。
- 不可变类型白名单的最终清单。
- 「重写 `Equals` 检测」与值相等性优先级的具体内联实现位置(`ChangeTracker` 内 vs `EqualityHelper` 扩展)。
