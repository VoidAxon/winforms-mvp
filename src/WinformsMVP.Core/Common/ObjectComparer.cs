using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Pragmatic reflection-based deep comparison. Serves as the default implementation for
    /// <see cref="ChangeTrackerDefaults"/> and can also be called directly.
    /// Leaf nodes respect Equals/IEquatable; collections are compared structurally; cycle-safe.
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

            if (a is Delegate) return true;   // ignore delegates / events

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
                if (!b.Contains(entry.Key)) return false;          // key equality delegated to the dictionary itself (pragmatic: keys are usually immutable)
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
