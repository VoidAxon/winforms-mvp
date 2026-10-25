using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kibinago.Common.Internal;

namespace Kibinago.Common
{
    /// <summary>
    /// Pragmatic reflection-based deep comparison; collections are compared structurally; cycle-safe.
    /// <see cref="DeepEquals(object, object)"/> respects every type's Equals/IEquatable.
    /// <see cref="ContentEquals(object, object)"/> — behind <see cref="BuiltInObjectContentComparer"/>, the default —
    /// trusts Equals only where it means "same value", so identity-style entity equality
    /// (e.g. "same Id") cannot hide an edit.
    /// </summary>
    public static class ObjectComparer
    {
        public static bool DeepEquals<T>(T a, T b) => DeepEquals((object)a, (object)b);

        public static bool DeepEquals(object a, object b)
        {
            return AreEqual(a, b, new Visited(contentMode: false, ignores: null));
        }

        public static bool ContentEquals<T>(T a, T b) => ContentEquals((object)a, (object)b);

        /// <summary>
        /// Compares by content: a custom Equals/IEquatable is used only for value types and framework
        /// (<c>System.*</c>) types. Application reference types — typically entities whose Equals
        /// compares an Id — are compared field by field, at every depth, so changing
        /// <c>Order.Customer.Name</c> is detected even when <c>Customer.Equals</c> compares Ids.
        /// </summary>
        public static bool ContentEquals(object a, object b)
        {
            return AreEqual(a, b, new Visited(contentMode: true, ignores: null));
        }

        /// <summary><see cref="ContentEquals(object, object)"/>, leaving out what <paramref name="ignores"/> lists.</summary>
        internal static bool ContentEquals(object a, object b, ChangeTrackingIgnores ignores)
        {
            var effective = ignores == null || ignores.IsEmpty ? null : ignores;
            return AreEqual(a, b, new Visited(contentMode: true, ignores: effective));
        }

        // Cycle-detection set that also carries the comparison mode (and any ignores) down the recursion.
        private sealed class Visited : HashSet<RefPair>
        {
            public readonly bool ContentMode;
            public readonly ChangeTrackingIgnores Ignores;   // null = nothing ignored
            public Visited(bool contentMode, ChangeTrackingIgnores ignores) { ContentMode = contentMode; Ignores = ignores; }
            public Visited(Visited source) : base(source) { ContentMode = source.ContentMode; Ignores = source.Ignores; }
        }

        private static bool TrustsEquals(Type type, Visited visited)
        {
            if (DeepReflection.IsImmutable(type)) return true;
            if (!DeepReflection.HasCustomEquals(type)) return false;
            return !visited.ContentMode || type.IsValueType || IsFrameworkType(type);
        }

        // Member ignores apply to application types only: framework types keep their own semantics
        // (e.g. IgnoreMember("Value") must not reach Nullable<T>.value).
        private static bool HasMemberIgnores(Type type, Visited visited)
        {
            if (visited.Ignores == null || DeepReflection.IsImmutable(type) || IsFrameworkType(type)) return false;
            foreach (var field in DeepReflection.GetFields(type))
                if (visited.Ignores.SkipsField(type, field)) return true;
            return false;
        }

        private static bool IsFrameworkType(Type type)
        {
            var ns = type.Namespace;
            return ns != null && (ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)
                               || ns.StartsWith("Microsoft.", StringComparison.Ordinal));
        }

        private static bool AreEqual(object a, object b, Visited visited)
        {
            if (ReferenceEquals(a, b)) return true;

            // A value of an ignored type is not compared: it equals null or any other ignored value
            // (e.g. a different derived type). It is not a wildcard — replacing it with something
            // else is a change, and set matching stays order-independent. This runs before the null
            // and runtime-type checks, so it also covers object-typed members and collection elements.
            if (visited.Ignores != null)
            {
                bool aIgnored = a != null && visited.Ignores.SkipsType(a.GetType());
                bool bIgnored = b != null && visited.Ignores.SkipsType(b.GetType());
                if (aIgnored || bIgnored)
                    return (aIgnored || a == null) && (bIgnored || b == null);
            }

            if (a == null || b == null) return false;

            var type = a.GetType();
            if (type != b.GetType()) return false;

            // A type's own Equals would still compare its ignored members, so an application type
            // with member ignores is compared field by field instead.
            if (TrustsEquals(type, visited) && !HasMemberIgnores(type, visited))
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

            var elementType = DeepReflection.GetSequenceElementType(type);
            if (elementType != null)
                return DeepReflection.IsSet(type, elementType)
                    ? SetEquals((IEnumerable)a, b, elementType, visited)
                    : SequenceEquals((IEnumerable)a, (IEnumerable)b, visited);

            if (DeepReflection.IsUnsupported(type))
                throw new NotSupportedException(
                    "ObjectComparer cannot deep-compare type '" + type.FullName +
                    "'. Register a ChangeTrackingPolicy for the model type, or replace the comparer " +
                    "(mvp.ChangeTracking.UseComparer).");

            return PocoEquals(a, b, type, visited);
        }

        private static bool ArrayEquals(Array a, Array b, Visited visited)
        {
            if (a.Rank != b.Rank || a.Length != b.Length) return false;
            if (a.Rank != 1)
                throw new NotSupportedException("ObjectComparer supports single-dimensional arrays only.");
            for (int i = 0; i < a.Length; i++)
                if (!AreEqual(a.GetValue(i), b.GetValue(i), visited)) return false;
            return true;
        }

        private static bool DictionaryEquals(IDictionary a, IDictionary b, Visited visited)
        {
            if (a.Count != b.Count) return false;
            foreach (DictionaryEntry entry in a)
            {
                if (!b.Contains(entry.Key)) return false;          // key equality delegated to the dictionary itself (pragmatic: keys are usually immutable)
                if (!AreEqual(entry.Value, b[entry.Key], visited)) return false;
            }
            return true;
        }

        private static bool ListEquals(IList a, IList b, Visited visited)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!AreEqual(a[i], b[i], visited)) return false;
            return true;
        }

        // Ordered collections (Queue, Stack, LinkedList, ...): element-by-element in enumeration order.
        private static bool SequenceEquals(IEnumerable a, IEnumerable b, Visited visited)
        {
            var ea = a.GetEnumerator();
            var eb = b.GetEnumerator();
            while (true)
            {
                bool hasA = ea.MoveNext(), hasB = eb.MoveNext();
                if (hasA != hasB) return false;
                if (!hasA) return true;
                if (!AreEqual(ea.Current, eb.Current, visited)) return false;
            }
        }

        // Sets: same count, then membership. Value-like elements (immutable or with their own
        // Equals) use b's own comparer. Reference-identity elements would never match a deep copy
        // that way, so they are paired structurally — each element of a with a distinct, deeply
        // equal element of b (O(n²), fine for model-sized sets).
        private static bool SetEquals(IEnumerable a, object b, Type elementType, Visited visited)
        {
            var collectionType = typeof(ICollection<>).MakeGenericType(elementType);
            var count = collectionType.GetProperty("Count");
            if ((int)count.GetValue(a, null) != (int)count.GetValue(b, null)) return false;

            if (!UsesReferenceIdentity(b, elementType, visited))
            {
                var contains = collectionType.GetMethod("Contains");
                foreach (var item in a)
                    if (!(bool)contains.Invoke(b, new[] { item })) return false;
                return true;
            }

            var unmatched = new List<object>();
            foreach (var item in (IEnumerable)b) unmatched.Add(item);
            foreach (var item in a)
            {
                int match = -1;
                for (int i = 0; i < unmatched.Count && match < 0; i++)
                {
                    // A failed trial must not leave "assumed equal" pairs behind in `visited`.
                    var trial = new Visited(visited);
                    if (AreEqual(item, unmatched[i], trial))
                    {
                        match = i;
                        visited.UnionWith(trial);
                    }
                }
                if (match < 0) return false;
                unmatched.RemoveAt(match);
            }
            return true;
        }

        // Membership is reference identity only when the set uses the default equality comparer
        // and the element type has no value equality of its own. An explicit comparer (e.g. a
        // case-insensitive name comparer) or a SortedSet's IComparer defines membership itself.
        private static bool UsesReferenceIdentity(object set, Type elementType, Visited visited)
        {
            if (TrustsEquals(elementType, visited))
                return false;
            // Content mode: an element whose content changed is an edit, whatever the set's own
            // membership comparer (often "same Id") says.
            if (visited.ContentMode)
                return true;
            var comparer = set.GetType().GetProperty("Comparer")?.GetValue(set, null);
            if (comparer == null) return true;
            var defaultComparer = typeof(EqualityComparer<>).MakeGenericType(elementType)
                .GetProperty("Default").GetValue(null, null);
            return Equals(comparer, defaultComparer);
        }

        private static bool PocoEquals(object a, object b, Type type, Visited visited)
        {
            foreach (var field in DeepReflection.GetFields(type))
            {
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                if (visited.Ignores != null && !IsFrameworkType(type) && visited.Ignores.SkipsField(type, field)) continue;
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
