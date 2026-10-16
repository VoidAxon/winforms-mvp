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
    /// Shared reflection utilities for the deep-copy and deep-comparison engines:
    /// type classification, field caching, and a reference-equality comparer.
    /// </summary>
    internal static class DeepReflection
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache
            = new ConcurrentDictionary<Type, FieldInfo[]>();

        private static readonly ConcurrentDictionary<Type, bool> ImmutableCache
            = new ConcurrentDictionary<Type, bool>();

        private static readonly ConcurrentDictionary<Type, bool> CustomEqualsCache
            = new ConcurrentDictionary<Type, bool>();

        /// <summary>Immutable (leaf) types: returned as-is during copy, compared with Equals.</summary>
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

        /// <summary>Types that cannot be safely copied or compared via reflection; throws <see cref="System.NotSupportedException"/>.</summary>
        internal static bool IsUnsupported(Type type)
        {
            return type.IsPointer
                || type.IsCOMObject
                || typeof(Stream).IsAssignableFrom(type)
                || typeof(SafeHandle).IsAssignableFrom(type);
        }

        /// <summary>Returns whether the type has meaningful equality semantics (implements <see cref="System.IEquatable{T}"/> or overrides <c>Equals(object)</c>).</summary>
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

        /// <summary>Collects all instance fields (including private) from a type and its base classes, cached per type.</summary>
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

        /// <summary>An equality comparer that uses reference identity as the key (used for cycle detection). No built-in equivalent exists in net40.</summary>
        internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            private ReferenceEqualityComparer() { }
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
