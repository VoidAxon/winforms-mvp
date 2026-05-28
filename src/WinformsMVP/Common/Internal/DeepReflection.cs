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
