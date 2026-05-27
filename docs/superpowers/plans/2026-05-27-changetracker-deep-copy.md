# ChangeTracker Deep-Copy (Remove ICloneable Constraint) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 放宽 `ChangeTracker<T>` 的 `ICloneable` 强约束为 `where T : class`,拷贝/比较改为「`ICloneable`/值相等性 → 全局钩子」解析,钩子默认指向内置缓存反射深拷贝/深比较引擎。

**Architecture:** 新增公开静态 `ObjectCloner.DeepCopy` / `ObjectComparer.DeepEquals`(务实档反射引擎,处理循环引用、集合、private 字段),共享内部 `DeepReflection`(类型分类 + 字段缓存 + 引用相等比较器)。新增非泛型静态钩子 `ChangeTrackerDefaults.Cloner` / `.Comparer`,默认指向上述引擎,可一行替换为第三方库。`ChangeTracker<T>` 构造时按优先级解析出内部 `_clone` / `_comparer` 委托。

**Tech Stack:** C# (`LangVersion=latest`),多目标 `net40;net48`,xUnit 测试。全程 BCL(反射、`FormatterServices.GetUninitializedObject`、`ConcurrentDictionary`),零新依赖。

**spec:** [docs/superpowers/specs/2026-05-27-changetracker-deep-copy-design.md](../specs/2026-05-27-changetracker-deep-copy-design.md)

**命名相对 spec 的微调(spec §8 已授权):** 钩子属性 `ChangeTrackerDefaults.Clone`/`.Equals` 改名为 `Cloner`/`Comparer`,因为静态类上的属性名 `Equals` 会与继承自 `object` 的静态 `Equals(object,object)` 冲突。

---

## File Structure

| 文件 | 责任 | 动作 |
|------|------|------|
| `src/WinformsMVP/Common/Internal/DeepReflection.cs` | 内部:类型分类(immutable/unsupported/值相等性)、字段缓存、引用相等比较器 | 创建 |
| `src/WinformsMVP/Common/ObjectCloner.cs` | 公开:反射深拷贝 | 创建 |
| `src/WinformsMVP/Common/ObjectComparer.cs` | 公开:反射深比较 | 创建 |
| `src/WinformsMVP/Common/ChangeTrackerDefaults.cs` | 公开:全局拷贝/比较钩子 | 创建 |
| `src/WinformsMVP/Common/ChangeTracker.cs` | 放宽约束 + 解析 `_clone`/`_comparer` | 修改 |
| `src/WinformsMVP.Samples.Tests/Common/ObjectClonerTests.cs` | 深拷贝单测 | 创建 |
| `src/WinformsMVP.Samples.Tests/Common/ObjectComparerTests.cs` | 深比较单测 | 创建 |
| `src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs` | 钩子 + 优先级 + 修复验证(全局状态,需串行) | 创建 |
| `src/WinformsMVP.Samples.Tests/Common/ChangeTrackerTests.cs` | 现有回归(不改语义) | 不改(仅运行验证) |
| `src/WinformsMVP.Net40SmokeTest/Program.cs` | net40 运行时验证非-ICloneable 反射路径 | 修改 |
| `CLAUDE.md` / `ChangeTracker.cs` docstring | 文档:从「必须 ICloneable」改为「推荐 + 自动回退 + 钩子」 | 修改 |

**测试隔离说明:** `ObjectClonerTests` / `ObjectComparerTests` 直接调用引擎,不碰全局钩子,可并行。`ChangeTrackerHookTests` 既**依赖**默认钩子、又**替换**钩子,必须与「依赖默认钩子的测试」串行——用 xUnit 的 `[Collection]` 把它们归到同一集合(同集合内不并行)。

---

## Task 1: ObjectCloner + DeepReflection(内置深拷贝引擎)

**Files:**
- Create: `src/WinformsMVP/Common/Internal/DeepReflection.cs`
- Create: `src/WinformsMVP/Common/ObjectCloner.cs`
- Test: `src/WinformsMVP.Samples.Tests/Common/ObjectClonerTests.cs`

- [ ] **Step 1: 写失败测试 —— 不可变类型短路 + 扁平 POCO 深拷贝**

创建 `src/WinformsMVP.Samples.Tests/Common/ObjectClonerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ObjectClonerTests
    {
        // 纯 POCO:无 ICloneable、无 Equals 重写。用自动属性,证明按 backing field 拷贝。
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void DeepCopy_Null_ReturnsNull()
        {
            Assert.Null(ObjectCloner.DeepCopy<PlainPoco>(null));
        }

        [Fact]
        public void DeepCopy_Immutables_ReturnedAsIs()
        {
            Assert.Equal(42, ObjectCloner.DeepCopy(42));
            Assert.Equal("hello", ObjectCloner.DeepCopy("hello"));
            var now = DateTime.Now;
            Assert.Equal(now, ObjectCloner.DeepCopy(now));
        }

        [Fact]
        public void DeepCopy_FlatPoco_CopiesAllFields_AndIsIndependent()
        {
            var src = new PlainPoco { Id = 1, Name = "orig" };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src, copy);
            Assert.Equal(1, copy.Id);
            Assert.Equal("orig", copy.Name);

            copy.Name = "changed";
            Assert.Equal("orig", src.Name);   // 改副本不影响原对象
        }
    }
}
```

- [ ] **Step 2: 运行测试,确认失败**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectClonerTests"`
Expected: 编译失败(`ObjectCloner` 不存在)。

- [ ] **Step 3: 创建 `DeepReflection` 内部引擎**

创建 `src/WinformsMVP/Common/Internal/DeepReflection.cs`:

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WinformsMVP.Common.Internal
{
    /// <summary>
    /// 深拷贝/深比较引擎共享的反射工具:类型分类、字段缓存、引用相等比较器。
    /// </summary>
    internal static class DeepReflection
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache
            = new ConcurrentDictionary<Type, FieldInfo[]>();

        private static readonly ConcurrentDictionary<Type, bool> ImmutableCache
            = new ConcurrentDictionary<Type, bool>();

        private static readonly ConcurrentDictionary<Type, bool> CustomEqualsCache
            = new ConcurrentDictionary<Type, bool>();

        /// <summary>不可变(叶子)类型:拷贝时原样返回,比较时用 Equals。</summary>
        internal static bool IsImmutable(Type type)
        {
            return ImmutableCache.GetOrAdd(type, t =>
                t.IsPrimitive            // bool/byte/short/int/long/char/float/double/IntPtr/UIntPtr...
                || t.IsEnum
                || t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(TimeSpan)
                || t == typeof(Guid));
        }

        /// <summary>无法安全反射拷贝/比较的类型:抛 NotSupportedException。</summary>
        internal static bool IsUnsupported(Type type)
        {
            return type.IsPointer
                || type.IsCOMObject
                || typeof(Stream).IsAssignableFrom(type)
                || typeof(SafeHandle).IsAssignableFrom(type);
        }

        /// <summary>类型是否有「有意义的相等语义」(IEquatable&lt;T&gt; 或重写了 Equals(object))。</summary>
        internal static bool HasCustomEquals(Type type)
        {
            return CustomEqualsCache.GetOrAdd(type, t =>
            {
                var iequatable = typeof(IEquatable<>).MakeGenericType(t);
                if (iequatable.IsAssignableFrom(t)) return true;

                var m = t.GetMethod("Equals", new[] { typeof(object) });
                return m != null
                    && m.DeclaringType != typeof(object)
                    && m.DeclaringType != typeof(ValueType);
            });
        }

        /// <summary>收集类型及其基类的全部实例字段(含 private),按类型缓存。</summary>
        internal static FieldInfo[] GetFields(Type type)
        {
            return FieldCache.GetOrAdd(type, t =>
            {
                var fields = new List<FieldInfo>();
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    fields.AddRange(cur.GetFields(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
                }
                return fields.ToArray();
            });
        }

        /// <summary>按引用相等做 Key 的比较器(用于循环引用检测)。net40 无内置版本。</summary>
        internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            private ReferenceEqualityComparer() { }
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

- [ ] **Step 4: 创建 `ObjectCloner`(最小:不可变 + POCO)**

创建 `src/WinformsMVP/Common/ObjectCloner.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Common
{
    /// <summary>
    /// 务实档反射深拷贝。既是 <see cref="ChangeTrackerDefaults"/> 的默认实现,也可直接调用。
    /// </summary>
    public static class ObjectCloner
    {
        public static T DeepCopy<T>(T source) => (T)DeepCopy((object)source);

        public static object DeepCopy(object source)
        {
            return Copy(source, new Dictionary<object, object>(
                DeepReflection.ReferenceEqualityComparer.Instance));
        }

        private static object Copy(object source, Dictionary<object, object> visited)
        {
            if (source == null) return null;

            var type = source.GetType();

            if (DeepReflection.IsImmutable(type)) return source;
            if (source is Delegate) return null;                       // 跳过委托/事件

            object existing;
            if (visited.TryGetValue(source, out existing)) return existing;

            // 占位:集合 / ICloneable / 不支持类型在 Task1 后续步骤补齐
            return CopyPoco(source, type, visited);
        }

        private static object CopyPoco(object source, Type type, Dictionary<object, object> visited)
        {
            var copy = FormatterServices.GetUninitializedObject(type);
            visited[source] = copy;
            foreach (var field in DeepReflection.GetFields(type))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                var value = field.GetValue(source);
                field.SetValue(copy, Copy(value, visited));
            }
            return copy;
        }
    }
}
```

- [ ] **Step 5: 运行测试,确认通过**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectClonerTests"`
Expected: PASS(3 个测试)。

- [ ] **Step 6: 写失败测试 —— 嵌套 POCO + 集合(数组/List/Dictionary)**

向 `ObjectClonerTests.cs` 追加(在类内补充嵌套/集合 model 与测试):

```csharp
        private class Parent
        {
            public string Title { get; set; }
            public PlainPoco Child { get; set; }
            public List<PlainPoco> Items { get; set; }
            public Dictionary<string, PlainPoco> Map { get; set; }
            public int[] Numbers { get; set; }
        }

        [Fact]
        public void DeepCopy_NestedPoco_IsDeep()
        {
            var src = new Parent { Title = "p", Child = new PlainPoco { Id = 1, Name = "c" } };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src.Child, copy.Child);
            Assert.Equal("c", copy.Child.Name);
            copy.Child.Name = "x";
            Assert.Equal("c", src.Child.Name);
        }

        [Fact]
        public void DeepCopy_Collections_AreDeep()
        {
            var src = new Parent
            {
                Items = new List<PlainPoco> { new PlainPoco { Id = 1, Name = "a" } },
                Map = new Dictionary<string, PlainPoco> { ["k"] = new PlainPoco { Id = 2, Name = "b" } },
                Numbers = new[] { 1, 2, 3 }
            };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.NotSame(src.Items, copy.Items);
            Assert.NotSame(src.Items[0], copy.Items[0]);
            Assert.Equal("a", copy.Items[0].Name);

            Assert.NotSame(src.Map, copy.Map);
            Assert.NotSame(src.Map["k"], copy.Map["k"]);
            Assert.Equal("b", copy.Map["k"].Name);

            Assert.NotSame(src.Numbers, copy.Numbers);
            Assert.Equal(new[] { 1, 2, 3 }, copy.Numbers);

            copy.Items[0].Name = "z";
            Assert.Equal("a", src.Items[0].Name);
        }
```

- [ ] **Step 7: 运行测试,确认失败**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectClonerTests"`
Expected: 新增的 2 个集合测试失败(`List`/`Dictionary`/数组被当作 POCO 走 `CopyPoco`,行为不对或抛异常)。

- [ ] **Step 8: 在 `ObjectCloner.Copy` 中加入数组/集合处理**

把 `Copy` 方法中「占位」注释那一行替换为以下分支(顺序很重要:数组在 ICloneable 之前,因为 `Array.Clone()` 是浅拷贝):

```csharp
            if (type.IsArray) return CopyArray((Array)source, type, visited);

            // 数组已在上面处理(Array 实现了 ICloneable 但 Clone() 是浅拷贝,必须排除)
            var cloneable = source as ICloneable;
            if (cloneable != null)
            {
                var clone = cloneable.Clone();
                visited[source] = clone;
                return clone;
            }

            var dictionary = source as IDictionary;
            if (dictionary != null) return CopyDictionary(dictionary, type, visited);

            var list = source as IList;
            if (list != null) return CopyList(list, type, visited);

            if (DeepReflection.IsUnsupported(type))
                throw new NotSupportedException(
                    "ObjectCloner cannot deep-copy type '" + type.FullName +
                    "'. Implement ICloneable on the model, or set ChangeTrackerDefaults.Cloner " +
                    "to a deep-copy library.");

            return CopyPoco(source, type, visited);
```

并在类内追加这三个方法:

```csharp
        private static object CopyArray(Array source, Type type, Dictionary<object, object> visited)
        {
            if (type.GetArrayRank() != 1)
                throw new NotSupportedException(
                    "ObjectCloner supports single-dimensional arrays only. Type: " + type.FullName);

            var elementType = type.GetElementType();
            var copy = Array.CreateInstance(elementType, source.Length);
            visited[source] = copy;
            for (int i = 0; i < source.Length; i++)
                copy.SetValue(Copy(source.GetValue(i), visited), i);
            return copy;
        }

        private static object CopyDictionary(IDictionary source, Type type, Dictionary<object, object> visited)
        {
            var copy = (IDictionary)Activator.CreateInstance(type);
            visited[source] = copy;
            foreach (DictionaryEntry entry in source)
                copy.Add(Copy(entry.Key, visited), Copy(entry.Value, visited));
            return copy;
        }

        private static object CopyList(IList source, Type type, Dictionary<object, object> visited)
        {
            var copy = (IList)Activator.CreateInstance(type);
            visited[source] = copy;
            foreach (var item in source)
                copy.Add(Copy(item, visited));
            return copy;
        }
```

- [ ] **Step 9: 运行测试,确认通过**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectClonerTests"`
Expected: PASS(5 个测试)。

- [ ] **Step 10: 写失败测试 —— 循环引用 / ICloneable 节点 / 委托跳过 / 不支持类型 / 多维数组**

向 `ObjectClonerTests.cs` 追加 model 与测试:

```csharp
        private class Node
        {
            public string Name { get; set; }
            public Node Next { get; set; }
        }

        private class CloneableLeaf : ICloneable
        {
            public string Tag { get; set; }
            public bool Cloned { get; private set; }
            public object Clone() => new CloneableLeaf { Tag = this.Tag, Cloned = true };
        }

        private class HasDelegate
        {
            public string Name { get; set; }
            public Action Handler { get; set; }
        }

        private class HasStream
        {
            public System.IO.MemoryStream Stream { get; set; }
        }

        [Fact]
        public void DeepCopy_CyclicGraph_DoesNotStackOverflow_AndPreservesTopology()
        {
            var a = new Node { Name = "a" };
            var b = new Node { Name = "b" };
            a.Next = b;
            b.Next = a;   // 环

            var copyA = ObjectCloner.DeepCopy(a);

            Assert.NotSame(a, copyA);
            Assert.Equal("a", copyA.Name);
            Assert.Equal("b", copyA.Next.Name);
            Assert.Same(copyA, copyA.Next.Next);   // 环被保留为副本内部的环
        }

        [Fact]
        public void DeepCopy_NodeImplementingICloneable_UsesCloneMethod()
        {
            var src = new CloneableLeaf { Tag = "t" };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.True(copy.Cloned);   // 走了 ICloneable.Clone() 而非反射
            Assert.Equal("t", copy.Tag);
        }

        [Fact]
        public void DeepCopy_DelegateField_IsSkipped()
        {
            var src = new HasDelegate { Name = "n", Handler = () => { } };

            var copy = ObjectCloner.DeepCopy(src);

            Assert.Equal("n", copy.Name);
            Assert.Null(copy.Handler);   // 委托被跳过
        }

        [Fact]
        public void DeepCopy_UnsupportedType_ThrowsNotSupported()
        {
            var src = new HasStream { Stream = new System.IO.MemoryStream() };

            Assert.Throws<NotSupportedException>(() => ObjectCloner.DeepCopy(src));
        }

        [Fact]
        public void DeepCopy_MultiDimensionalArray_ThrowsNotSupported()
        {
            var src = new int[2, 2];

            Assert.Throws<NotSupportedException>(() => ObjectCloner.DeepCopy(src));
        }
```

- [ ] **Step 11: 运行测试,确认通过(逻辑已在 Step 8 实现)**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectClonerTests"`
Expected: PASS(10 个测试)。循环引用、ICloneable 节点、委托跳过、不支持类型、多维数组分支均已由 Step 3/4/8 的实现覆盖。若某项失败,按报错修对应分支。

- [ ] **Step 12: 提交**

```bash
git add src/WinformsMVP/Common/Internal/DeepReflection.cs src/WinformsMVP/Common/ObjectCloner.cs src/WinformsMVP.Samples.Tests/Common/ObjectClonerTests.cs
git commit -m "feat: Add ObjectCloner reflection deep-copy engine"
```

---

## Task 2: ObjectComparer(内置深比较引擎)

**Files:**
- Create: `src/WinformsMVP/Common/ObjectComparer.cs`
- Test: `src/WinformsMVP.Samples.Tests/Common/ObjectComparerTests.cs`

- [ ] **Step 1: 写失败测试**

创建 `src/WinformsMVP.Samples.Tests/Common/ObjectComparerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ObjectComparerTests
    {
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class Parent
        {
            public string Title { get; set; }
            public PlainPoco Child { get; set; }
            public List<PlainPoco> Items { get; set; }
        }

        private class Node
        {
            public string Name { get; set; }
            public Node Next { get; set; }
        }

        private class HasDelegate
        {
            public string Name { get; set; }
            public Action Handler { get; set; }
        }

        private class HasStream
        {
            public System.IO.MemoryStream Stream { get; set; }
        }

        [Fact]
        public void DeepEquals_NullCases()
        {
            Assert.True(ObjectComparer.DeepEquals<PlainPoco>(null, null));
            Assert.False(ObjectComparer.DeepEquals(new PlainPoco(), null));
            Assert.False(ObjectComparer.DeepEquals(null, new PlainPoco()));
        }

        [Fact]
        public void DeepEquals_SameReference_IsTrue()
        {
            var p = new PlainPoco { Id = 1 };
            Assert.True(ObjectComparer.DeepEquals(p, p));
        }

        [Fact]
        public void DeepEquals_FlatPoco_StructuralEquality()
        {
            var a = new PlainPoco { Id = 1, Name = "x" };
            var b = new PlainPoco { Id = 1, Name = "x" };
            var c = new PlainPoco { Id = 1, Name = "y" };

            Assert.True(ObjectComparer.DeepEquals(a, b));
            Assert.False(ObjectComparer.DeepEquals(a, c));
        }

        [Fact]
        public void DeepEquals_Nested_And_Collections()
        {
            var a = new Parent
            {
                Title = "p",
                Child = new PlainPoco { Id = 1, Name = "c" },
                Items = new List<PlainPoco> { new PlainPoco { Id = 9, Name = "i" } }
            };
            var b = new Parent
            {
                Title = "p",
                Child = new PlainPoco { Id = 1, Name = "c" },
                Items = new List<PlainPoco> { new PlainPoco { Id = 9, Name = "i" } }
            };
            Assert.True(ObjectComparer.DeepEquals(a, b));

            b.Items[0].Name = "different";
            Assert.False(ObjectComparer.DeepEquals(a, b));   // 集合内嵌套差异
        }

        [Fact]
        public void DeepEquals_CyclicGraph_Terminates()
        {
            var a1 = new Node { Name = "a" }; var a2 = new Node { Name = "b" };
            a1.Next = a2; a2.Next = a1;
            var b1 = new Node { Name = "a" }; var b2 = new Node { Name = "b" };
            b1.Next = b2; b2.Next = b1;

            Assert.True(ObjectComparer.DeepEquals(a1, b1));
        }

        [Fact]
        public void DeepEquals_DelegateFields_AreIgnored()
        {
            var a = new HasDelegate { Name = "n", Handler = () => { } };
            var b = new HasDelegate { Name = "n", Handler = () => { } };

            Assert.True(ObjectComparer.DeepEquals(a, b));   // 委托被忽略,只比 Name
        }

        [Fact]
        public void DeepEquals_UnsupportedType_ThrowsNotSupported()
        {
            var a = new HasStream { Stream = new System.IO.MemoryStream() };
            var b = new HasStream { Stream = new System.IO.MemoryStream() };

            Assert.Throws<NotSupportedException>(() => ObjectComparer.DeepEquals(a, b));
        }
    }
}
```

- [ ] **Step 2: 运行测试,确认失败**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectComparerTests"`
Expected: 编译失败(`ObjectComparer` 不存在)。

- [ ] **Step 3: 创建 `ObjectComparer`**

创建 `src/WinformsMVP/Common/ObjectComparer.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Common
{
    /// <summary>
    /// 务实档反射深比较。既是 <see cref="ChangeTrackerDefaults"/> 的默认实现,也可直接调用。
    /// 叶子节点尊重 Equals/IEquatable;集合按结构比较;循环引用安全。
    /// </summary>
    public static class ObjectComparer
    {
        public static bool DeepEquals<T>(T a, T b) => DeepEquals((object)a, (object)b);

        public static bool DeepEquals(object a, object b)
        {
            return AreEqual(a, b, new HashSet<RefPair>());
        }

        private static bool AreEqual(object a, object b, HashSet<RefPair> visited)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            var type = a.GetType();
            if (type != b.GetType()) return false;

            if (DeepReflection.IsImmutable(type) || DeepReflection.HasCustomEquals(type))
                return a.Equals(b);

            if (a is Delegate) return true;   // 忽略委托/事件

            var pair = new RefPair(a, b);
            if (visited.Contains(pair)) return true;
            visited.Add(pair);

            if (type.IsArray) return ArrayEquals((Array)a, (Array)b, visited);

            var dictA = a as IDictionary;
            if (dictA != null) return DictionaryEquals(dictA, (IDictionary)b, visited);

            var listA = a as IList;
            if (listA != null) return ListEquals(listA, (IList)b, visited);

            if (DeepReflection.IsUnsupported(type))
                throw new NotSupportedException(
                    "ObjectComparer cannot deep-compare type '" + type.FullName +
                    "'. Implement Equals/IEquatable on the model, or set ChangeTrackerDefaults.Comparer.");

            return PocoEquals(a, b, type, visited);
        }

        private static bool ArrayEquals(Array a, Array b, HashSet<RefPair> visited)
        {
            if (a.Rank != b.Rank || a.Length != b.Length) return false;
            if (a.Rank != 1)
                throw new NotSupportedException("ObjectComparer supports single-dimensional arrays only.");
            for (int i = 0; i < a.Length; i++)
                if (!AreEqual(a.GetValue(i), b.GetValue(i), visited)) return false;
            return true;
        }

        private static bool DictionaryEquals(IDictionary a, IDictionary b, HashSet<RefPair> visited)
        {
            if (a.Count != b.Count) return false;
            foreach (DictionaryEntry entry in a)
            {
                if (!b.Contains(entry.Key)) return false;          // 键用字典自身相等(务实档:键通常不可变)
                if (!AreEqual(entry.Value, b[entry.Key], visited)) return false;
            }
            return true;
        }

        private static bool ListEquals(IList a, IList b, HashSet<RefPair> visited)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!AreEqual(a[i], b[i], visited)) return false;
            return true;
        }

        private static bool PocoEquals(object a, object b, Type type, HashSet<RefPair> visited)
        {
            foreach (var field in DeepReflection.GetFields(type))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                if (!AreEqual(field.GetValue(a), field.GetValue(b), visited)) return false;
            }
            return true;
        }

        private struct RefPair : IEquatable<RefPair>
        {
            private readonly object _a;
            private readonly object _b;
            public RefPair(object a, object b) { _a = a; _b = b; }
            public bool Equals(RefPair other) => ReferenceEquals(_a, other._a) && ReferenceEquals(_b, other._b);
            public override bool Equals(object obj) => obj is RefPair && Equals((RefPair)obj);
            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(_a) * 397) ^ RuntimeHelpers.GetHashCode(_b);
                }
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试,确认通过**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ObjectComparerTests"`
Expected: PASS(8 个测试)。

- [ ] **Step 5: 提交**

```bash
git add src/WinformsMVP/Common/ObjectComparer.cs src/WinformsMVP.Samples.Tests/Common/ObjectComparerTests.cs
git commit -m "feat: Add ObjectComparer reflection deep-compare engine"
```

---

## Task 3: ChangeTrackerDefaults(全局钩子)

**Files:**
- Create: `src/WinformsMVP/Common/ChangeTrackerDefaults.cs`
- Test: `src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs`(本任务创建,后续 Task 4 继续追加)

- [ ] **Step 1: 写失败测试 —— 默认值非空 + 指向引擎 + 可替换**

创建 `src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs`:

```csharp
using System;
using Xunit;
using WinformsMVP.Common;

namespace WinformsMVP.Samples.Tests.Common
{
    // 该集合内的测试既依赖默认钩子、又替换钩子,必须串行(不与彼此并行)。
    [Collection("ChangeTrackerDefaults")]
    public class ChangeTrackerHookTests
    {
        private class PlainPoco
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        [Fact]
        public void Defaults_AreNotNull_AndDelegateToEngine()
        {
            Assert.NotNull(ChangeTrackerDefaults.Cloner);
            Assert.NotNull(ChangeTrackerDefaults.Comparer);

            var src = new PlainPoco { Id = 1, Name = "x" };
            var copy = (PlainPoco)ChangeTrackerDefaults.Cloner(src);
            Assert.NotSame(src, copy);
            Assert.Equal("x", copy.Name);

            Assert.True(ChangeTrackerDefaults.Comparer(
                new PlainPoco { Id = 1, Name = "x" },
                new PlainPoco { Id = 1, Name = "x" }));
        }

        [Fact]
        public void Cloner_CanBeReplaced()
        {
            var original = ChangeTrackerDefaults.Cloner;
            try
            {
                var called = false;
                ChangeTrackerDefaults.Cloner = o => { called = true; return original(o); };

                var copy = (PlainPoco)ChangeTrackerDefaults.Cloner(new PlainPoco { Name = "y" });

                Assert.True(called);
                Assert.Equal("y", copy.Name);
            }
            finally
            {
                ChangeTrackerDefaults.Cloner = original;   // 还原全局状态
            }
        }
    }

    // 定义集合(空类,仅承载 CollectionDefinition 特性)。
    [CollectionDefinition("ChangeTrackerDefaults", DisableParallelization = true)]
    public class ChangeTrackerDefaultsCollection { }
}
```

- [ ] **Step 2: 运行测试,确认失败**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ChangeTrackerHookTests"`
Expected: 编译失败(`ChangeTrackerDefaults` 不存在)。

- [ ] **Step 3: 创建 `ChangeTrackerDefaults`**

创建 `src/WinformsMVP/Common/ChangeTrackerDefaults.cs`:

```csharp
using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// <see cref="ChangeTracker{T}"/> 在 model 未实现 ICloneable / 值相等性时使用的全局深拷贝/深比较钩子。
    /// 默认指向内置缓存反射引擎(<see cref="ObjectCloner"/> / <see cref="ObjectComparer"/>);
    /// 可在应用启动时一行替换为第三方库,例如:
    /// <code>ChangeTrackerDefaults.Cloner = o => o.DeepClone();   // Force.DeepCloner</code>
    /// 约定:在应用启动时设置一次(写入非线程安全)。
    /// </summary>
    public static class ChangeTrackerDefaults
    {
        public static Func<object, object> Cloner { get; set; } = ObjectCloner.DeepCopy;

        public static Func<object, object, bool> Comparer { get; set; } = ObjectComparer.DeepEquals;
    }
}
```

- [ ] **Step 4: 运行测试,确认通过**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ChangeTrackerHookTests"`
Expected: PASS(2 个测试)。

- [ ] **Step 5: 提交**

```bash
git add src/WinformsMVP/Common/ChangeTrackerDefaults.cs src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs
git commit -m "feat: Add ChangeTrackerDefaults global clone/compare hook"
```

---

## Task 4: ChangeTracker<T> 放宽约束 + 解析逻辑 + bug 修复

**Files:**
- Modify: `src/WinformsMVP/Common/ChangeTracker.cs`
- Test: `src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs`(追加)

- [ ] **Step 1: 写失败测试 —— 非-ICloneable model 可用 + IsChanged 修复 + 优先级 + comparer 覆盖**

向 `ChangeTrackerHookTests.cs` 的 `ChangeTrackerHookTests` 类内追加 model 与测试:

```csharp
        // 无 ICloneable、无 Equals 重写:旧实现会「永远 IsChanged=true」,新实现应正确。
        private class NoEqualsModel
        {
            public string Name { get; set; }
            private int _secret;
            public int Secret { get => _secret; set => _secret = value; }
        }

        // 实现 ICloneable(用计数验证走的是 Clone,不是钩子)。
        private class CloneableModel : ICloneable
        {
            public static int CloneCount;
            public string Name { get; set; }
            public object Clone()
            {
                CloneCount++;
                return new CloneableModel { Name = this.Name };
            }
        }

        [Fact]
        public void Tracker_NonCloneableModel_NotChangedAfterConstruction()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 7 };

            var tracker = new ChangeTracker<NoEqualsModel>(model);

            Assert.False(tracker.IsChanged);   // 修复:旧实现这里是 true
        }

        [Fact]
        public void Tracker_NonCloneableModel_DetectsChange_AndRejectRestores()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 7 };
            var tracker = new ChangeTracker<NoEqualsModel>(model);

            tracker.UpdateCurrentValue(new NoEqualsModel { Name = "b", Secret = 7 });
            Assert.True(tracker.IsChanged);

            tracker.RejectChanges();
            Assert.False(tracker.IsChanged);
            Assert.Equal("a", tracker.CurrentValue.Name);
            Assert.Equal(7, tracker.CurrentValue.Secret);   // private backing field 也被完整还原
        }

        [Fact]
        public void Tracker_CloneableModel_UsesCloneMethod_NotHook()
        {
            CloneableModel.CloneCount = 0;
            var originalCloner = ChangeTrackerDefaults.Cloner;
            try
            {
                ChangeTrackerDefaults.Cloner = o => throw new InvalidOperationException("hook should not be called");

                var tracker = new ChangeTracker<CloneableModel>(new CloneableModel { Name = "a" });

                Assert.True(CloneableModel.CloneCount >= 2);   // 构造克隆两份,走 ICloneable
                Assert.False(tracker.IsChanged);
            }
            finally
            {
                ChangeTrackerDefaults.Cloner = originalCloner;
            }
        }

        [Fact]
        public void Tracker_ExplicitComparer_OverridesEverything()
        {
            var model = new NoEqualsModel { Name = "a", Secret = 1 };
            // 只看 Name,忽略 Secret
            var tracker = new ChangeTracker<NoEqualsModel>(model, (x, y) => x.Name == y.Name);

            tracker.UpdateCurrentValue(new NoEqualsModel { Name = "a", Secret = 999 });

            Assert.False(tracker.IsChanged);   // Secret 变了但被自定义 comparer 忽略
        }
```

- [ ] **Step 2: 运行测试,确认失败**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ChangeTrackerHookTests"`
Expected: 编译失败 —— `new ChangeTracker<NoEqualsModel>(...)` 不满足现有 `where T : class, ICloneable` 约束。

- [ ] **Step 3: 修改 `ChangeTracker.cs` —— 放宽约束、新增 `_clone`、改写构造与快照调用**

3a. 类声明(约第 50 行)去掉 `ICloneable`:

```csharp
    public class ChangeTracker<T> : IChangeTracking, IRevertibleChangeTracking where T : class
```

3b. 字段区(`_comparer` 声明附近,约第 54 行)新增 `_clone`:

```csharp
        private readonly Func<T, T> _clone;
```

3c. 主构造(约第 106-114 行)改为先解析委托、再用委托建快照:

```csharp
        public ChangeTracker(T initialValue, Func<T, T, bool> comparer = null)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));

            _clone = BuildCloneFunc(initialValue);
            _comparer = comparer ?? BuildComparerFunc();
            _currentValue = _clone(initialValue);
            _originalValue = _clone(initialValue);
        }
```

3d. 在「私有辅助方法」区(`InvalidateIsChanged` 附近,约第 302 行)新增解析方法:

```csharp
        // 拷贝解析:initialValue 实现 ICloneable → 用 Clone();否则用全局钩子。
        private static Func<T, T> BuildCloneFunc(T sample)
        {
            if (sample is ICloneable)
                return v => (T)((ICloneable)v).Clone();
            return v => (T)ChangeTrackerDefaults.Cloner(v);
        }

        // 比较解析:T 有值相等语义(IEquatable/IComparable/Equals 重写) → EqualityHelper;否则用全局钩子。
        private static Func<T, T, bool> BuildComparerFunc()
        {
            if (HasValueEquality(typeof(T)))
                return EqualityHelper.Equals;
            return (a, b) => ChangeTrackerDefaults.Comparer(a, b);
        }

        private static bool HasValueEquality(Type t)
        {
            if (typeof(IEquatable<T>).IsAssignableFrom(t)) return true;
            if (typeof(IComparable<T>).IsAssignableFrom(t)) return true;
            var m = t.GetMethod("Equals", new[] { typeof(object) });
            return m != null && m.DeclaringType != typeof(object);
        }
```

3e. 把现有四处 `.Clone()` 调用改为走 `_clone`:

- `AcceptChanges()`(约第 160 行):`_originalValue = (T)_currentValue.Clone();` → `_originalValue = _clone(_currentValue);`
- `RejectChanges()`(约第 183 行):`_currentValue = (T)_originalValue.Clone();` → `_currentValue = _clone(_originalValue);`
- `AcceptChanges(T newValue)`(约第 224-225 行):
  ```csharp
                _originalValue = _clone(newValue);
                _currentValue = _clone(newValue);
  ```
- `GetOriginalValue()`(约第 243 行):`return (T)_originalValue.Clone();` → `return _clone(_originalValue);`

> `IsChanged`、`IsChangedWith`、`UpdateCurrentValue` 用的是 `_comparer` / `CurrentValue`,无需改动。`using` 已含 `WinformsMVP.Common.Helpers`(`EqualityHelper`);`ChangeTrackerDefaults` 同命名空间无需 using。

- [ ] **Step 4: 运行新测试,确认通过**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ChangeTrackerHookTests"`
Expected: PASS(共 6 个:Task3 的 2 + Task4 的 4)。

- [ ] **Step 5: 运行现有 ChangeTracker 回归,确认未破坏**

Run: `dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj --filter "FullyQualifiedName~ChangeTrackerTests"`
Expected: 现有 `ChangeTrackerTests` 全绿(`TestModel`/`NestedModel` 实现了 `ICloneable`+`IEquatable`,走快路径,行为不变)。

- [ ] **Step 6: 提交**

```bash
git add src/WinformsMVP/Common/ChangeTracker.cs src/WinformsMVP.Samples.Tests/Common/ChangeTrackerHookTests.cs
git commit -m "feat: Relax ChangeTracker<T> to where T : class with hook-based clone/compare"
```

---

## Task 5: net40 运行时验证(非-ICloneable 反射路径)

**Files:**
- Modify: `src/WinformsMVP.Net40SmokeTest/Program.cs`

- [ ] **Step 1: 在 smoke test 中新增非-ICloneable model 的反射回退验证**

在 `Program.cs` 的 `ChangeTrackerSmoke()` 方法末尾(`RejectChanges` 验证之后、方法右括号之前)追加:

```csharp
            // 非-ICloneable、非-Equals 的 model:在 net40 上验证反射深拷贝/深比较钩子可用。
            var plain = new PlainTrackedModel { Name = "before" };
            var plainTracker = new ChangeTracker<PlainTrackedModel>(plain);
            if (plainTracker.IsChanged)
                throw new InvalidOperationException("Reflection-fallback tracker reports IsChanged after construction.");

            plainTracker.UpdateCurrentValue(new PlainTrackedModel { Name = "after" });
            if (!plainTracker.IsChanged)
                throw new InvalidOperationException("Reflection-fallback tracker did not detect a change.");

            plainTracker.RejectChanges();
            if (plainTracker.IsChanged || plainTracker.CurrentValue.Name != "before")
                throw new InvalidOperationException("Reflection-fallback RejectChanges did not restore the value.");
```

在 `TrackedModel` 类定义旁(约第 194 行附近)新增:

```csharp
    internal sealed class PlainTrackedModel
    {
        public string Name { get; set; }
    }
```

- [ ] **Step 2: 在 net40 下构建并运行 smoke test**

Run: `dotnet run --project src/WinformsMVP.Net40SmokeTest/WinformsMVP.Net40SmokeTest.csproj -f net40`
Expected: 进程退出码 0,无异常输出(若 csproj 仅单目标则去掉 `-f net40`;先 `dotnet build` 确认 net40 编译通过)。

- [ ] **Step 3: 提交**

```bash
git add src/WinformsMVP.Net40SmokeTest/Program.cs
git commit -m "test: Verify ChangeTracker reflection fallback runs on net40"
```

---

## Task 6: 文档更新 + 全量回归

**Files:**
- Modify: `src/WinformsMVP/Common/ChangeTracker.cs`(XML docstring)
- Modify: `CLAUDE.md`(Change Tracking 章节)

- [ ] **Step 1: 更新 `ChangeTracker.cs` 类级 XML 文档**

把类上方 `<summary>`/`<remarks>`(约第 8-49 行)替换为反映新语义的说明:

```csharp
    /// <summary>
    /// Tracks changes to an object of type T and allows accepting or rejecting changes.
    /// T must be a reference type (class).
    /// </summary>
    /// <typeparam name="T">The type to track. Must be a reference type.</typeparam>
    /// <remarks>
    /// <para>
    /// Snapshotting (clone) resolution order: if T implements <see cref="ICloneable"/> its
    /// Clone() is used; otherwise the global <see cref="ChangeTrackerDefaults.Cloner"/> hook is used
    /// (default = built-in reflection deep copy in <see cref="ObjectCloner"/>).
    /// </para>
    /// <para>
    /// Comparison resolution order: an explicit comparer argument wins; otherwise if T has value
    /// equality (IEquatable&lt;T&gt;/IComparable&lt;T&gt;/Equals override) it is used; otherwise the
    /// global <see cref="ChangeTrackerDefaults.Comparer"/> hook is used (default = reflection deep compare).
    /// </para>
    /// <para>
    /// To plug in a third-party deep-copy library, set the hook once at startup, e.g.
    /// <c>ChangeTrackerDefaults.Cloner = o => o.DeepClone();</c>
    /// </para>
    /// <para>
    /// Implementing <see cref="ICloneable"/> with a deep copy is still recommended as the fast path,
    /// but is no longer required.
    /// </para>
    /// </remarks>
```

- [ ] **Step 2: 更新 `CLAUDE.md` 的 Change Tracking 章节**

定位「### Change Tracking」小节开头(当前写「需要 Model 实现 iclonable 接口」「使用 ChangeTracker<T> 的类型必须实现 ICloneable」)。把开头的「必须实现 ICloneable」表述替换为下述说明,并保留后面关于深拷贝实现质量的指导(它仍适用于 `ICloneable` 快路径):

替换开头段落为:

```markdown
**ChangeTracker<T>** (`WinformsMVP.Common.ChangeTracker`) は編集/キャンセルシナリオのための堅牢な変更追跡を提供します。`IChangeTracking`および`IRevertibleChangeTracking`インターフェースを実装しています。

**型制約:** `where T : class`(参照型のみ)。`ICloneable` の実装は **任意**(必須ではありません)。

**スナップショット(複製)の解決順序:**
1. T が `ICloneable` を実装 → その `Clone()` を使用(高速パス、推奨)
2. 未実装 → グローバルフック `ChangeTrackerDefaults.Cloner`(既定 = 組み込みリフレクション深いコピー `ObjectCloner`)

**比較の解決順序:**
1. コンストラクタに渡した `comparer` → それを使用
2. T が値等価性(`IEquatable<T>`/`IComparable<T>`/`Equals` オーバーライド)を持つ → それを使用
3. いずれも無い → グローバルフック `ChangeTrackerDefaults.Comparer`(既定 = リフレクション深い比較)

**第三者ライブラリの差し込み(起動時に一度だけ):**
```csharp
ChangeTrackerDefaults.Cloner = o => o.DeepClone();   // 例: Force.DeepCloner
```

> **動作変更の注意:** `Equals` を override していないモデルは、旧実装では構築直後に `IsChanged == true`(参照比較によるバグ)でしたが、新実装ではリフレクション深い比較により正しく判定されます。`ICloneable`+`Equals` を実装済みのモデルは挙動不変です。
```

(其后「深拷贝实现质量」「MemberwiseClone 禁止」等指导原样保留——它们对 `ICloneable` 快路径仍然适用。)

- [ ] **Step 3: 全量构建 + 全量测试**

Run:
```bash
dotnet build src/winforms-mvp.sln
dotnet test src/WinformsMVP.Samples.Tests/WindowsMVP.Samples.Tests.csproj
```
Expected: 解决方案在 net40+net48 下均编译通过;测试全绿(新增 ObjectCloner 10 + ObjectComparer 8 + Hook/Tracker 6 + 现有回归全部)。

- [ ] **Step 4: 提交**

```bash
git add src/WinformsMVP/Common/ChangeTracker.cs CLAUDE.md
git commit -m "docs: Update ChangeTracker docs for relaxed ICloneable constraint"
```

---

## Self-Review 记录

**Spec 覆盖核对:**
- §2 约束放宽 → Task 4 Step 3a ✅
- §2/§3.3 拷贝解析(ICloneable > 钩子) → Task 4 Step 3d `BuildCloneFunc` ✅
- §2/§3.3 比较解析(comparer > 值相等性 > 钩子) → Task 4 Step 3d `BuildComparerFunc`/`HasValueEquality` ✅
- §3.3 比较第2步含 IComparable → `HasValueEquality` 含 IComparable ✅
- §3.4 不新增构造重载 → Task 4 仅改主构造,未加重载 ✅
- §3 全局钩子 `ChangeTrackerDefaults` → Task 3 ✅(命名 Clone/Equals→Cloner/Comparer,见顶部说明)
- §3.5 反射引擎务实档(不可变/ICloneable节点/数组/List/Dictionary/POCO字段/委托跳过/不支持抛异常/循环引用) → Task 1+2 全覆盖 ✅
- §3.5 Array.Clone 浅拷贝坑 → Task 1 Step 8 数组分支置于 ICloneable 之前 ✅
- §3.6 EqualityHelper 复用 → `BuildComparerFunc` 用 `EqualityHelper.Equals`;`ObjectComparer` 叶子用 `HasCustomEquals`+`Equals` ✅
- §4 行为变化(无 Equals model 修复) → Task 4 Step 1 `Tracker_NonCloneableModel_NotChangedAfterConstruction` + 文档注明 ✅
- §5 net40/零依赖 → Task 5 net40 smoke ✅;全程 BCL ✅
- §6 测试计划(各项) → Task 1/2/3/4 测试逐条对应 ✅
- §7 不做项 → 计划未引入接口/facade/编译引擎/cloneFunc/值类型/序列化默认 ✅

**Placeholder 扫描:** 无 TBD/TODO;每个改代码的 step 均含完整代码。Task1 Step4 的「占位」注释在 Step8 被明确替换。

**类型/命名一致性:** `ObjectCloner.DeepCopy`、`ObjectComparer.DeepEquals`、`DeepReflection.{IsImmutable,IsUnsupported,HasCustomEquals,GetFields,ReferenceEqualityComparer}`、`ChangeTrackerDefaults.{Cloner,Comparer}`、`ChangeTracker._clone`/`BuildCloneFunc`/`BuildComparerFunc`/`HasValueEquality` —— 跨任务引用一致。
