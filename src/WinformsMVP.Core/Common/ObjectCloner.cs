using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Pragmatic reflection-based deep copy. Backs <see cref="BuiltInObjectCloner"/>, the default
    /// <see cref="IObjectCloner"/>, and can also be called directly. <see cref="ICloneable"/> is
    /// not consulted (its Clone() may be shallow).
    /// </summary>
    /// <remarks>
    /// Explicitly supported collections: single-dimensional arrays, <see cref="IList"/>
    /// (e.g. List&lt;T&gt;), and <see cref="IDictionary"/> (e.g. Dictionary&lt;K,V&gt;) —
    /// all deep-copied element by element. Other <see cref="System.Collections.IEnumerable"/>
    /// collections (e.g. HashSet&lt;T&gt;, Queue&lt;T&gt;) are not explicitly supported and
    /// fall back to field-by-field POCO copy — usable, but for guaranteed
    /// semantics register a <see cref="ChangeTrackingPolicy{T}"/> for the model type or replace
    /// the application's <see cref="IObjectCloner"/>.
    /// <para>
    /// Known limitations of the <see cref="IList"/>/<see cref="IDictionary"/> paths:
    /// </para>
    /// <list type="bullet">
    ///   <item>The concrete type must have a public parameterless constructor.
    ///   Read-only or immutable collections (e.g. <c>ReadOnlyCollection&lt;T&gt;</c>) throw —
    ///   register a <see cref="ChangeTrackingPolicy{T}"/> or swap the cloner for those.</item>
    ///   <item>A <see cref="IDictionary"/>'s key comparer is preserved only when the type exposes a
    ///   <c>Comparer</c> property and a matching single-argument constructor (true for
    ///   <c>Dictionary&lt;K,V&gt;</c>); for other dictionaries the default comparer is used.</item>
    /// </list>
    /// </remarks>
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
            if (source is Delegate) return null;                       // skip delegates / events

            object existing;
            if (visited.TryGetValue(source, out existing))
            {
                if (ReferenceEquals(existing, InProgress))
                    throw new NotSupportedException(
                        "ObjectCloner cannot deep-copy a cycle through collection type '" + type.FullName +
                        "' (it can only be rebuilt from its finished elements). Register a ChangeTrackingPolicy " +
                        "for the model type, or replace the cloner (mvp.ChangeTracking.UseCloner).");
                return existing;
            }

            if (type.IsArray) return CopyArray((Array)source, type, visited);

            // ICloneable is deliberately not consulted: .NET allows Clone() to be shallow (Array.Clone
            // is), and a shallow snapshot hides edits. A model with a verified deep Clone() opts in
            // through a ChangeTrackingPolicy.

            var dictionary = source as IDictionary;
            if (dictionary != null) return CopyDictionary(dictionary, type, visited);

            var list = source as IList;
            if (list != null) return CopyList(list, type, visited);

            var elementType = DeepReflection.GetSequenceElementType(type);
            if (elementType != null)
            {
                var sequence = CopySequence((IEnumerable)source, type, elementType, visited);
                if (sequence != null) return sequence;
            }

            if (DeepReflection.IsUnsupported(type))
                throw new NotSupportedException(
                    "ObjectCloner cannot deep-copy type '" + type.FullName +
                    "'. Register a ChangeTrackingPolicy for the model type, or replace the cloner " +
                    "(mvp.ChangeTracking.UseCloner) with a deep-copy library.");

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
            var copy = (IDictionary)CreateDictionary(source, type);
            visited[source] = copy;
            foreach (DictionaryEntry entry in source)
                copy.Add(Copy(entry.Key, visited), Copy(entry.Value, visited));
            return copy;
        }

        // Dictionary<TKey,TValue> carries an IEqualityComparer<TKey> that plain construction would
        // silently drop — e.g. a Dictionary<string,T>(StringComparer.OrdinalIgnoreCase) would come
        // back case-sensitive, a silent correctness change. Reconstruct with the original comparer
        // when the source exposes one and a matching constructor exists; otherwise fall back.
        private static object CreateDictionary(IDictionary source, Type type)
        {
            var comparerProperty = type.GetProperty("Comparer");
            if (comparerProperty != null)
            {
                var comparer = comparerProperty.GetValue(source, null);
                if (comparer != null)
                {
                    var ctor = type.GetConstructor(new[] { comparerProperty.PropertyType });
                    if (ctor != null)
                        return ctor.Invoke(new[] { comparer });
                }
            }
            return Activator.CreateInstance(type);
        }

        private static object CopyList(IList source, Type type, Dictionary<object, object> visited)
        {
            if (type.GetConstructor(Type.EmptyTypes) == null || source.IsReadOnly || source.IsFixedSize)
            {
                // ReadOnlyCollection<T> and similar wrappers: no parameterless constructor (or no Add),
                // but they wrap a list passed to their constructor.
                var elementType = DeepReflection.GetSequenceElementType(type);
                var wrapped = elementType == null ? null : CopySequence(source, type, elementType, visited);
                if (wrapped != null) return wrapped;
                throw new NotSupportedException(
                    "ObjectCloner cannot deep-copy read-only list type '" + type.FullName +
                    "'. Register a ChangeTrackingPolicy for the model type, or replace the cloner " +
                    "(mvp.ChangeTracking.UseCloner).");
            }

            var copy = (IList)Activator.CreateInstance(type);
            visited[source] = copy;
            foreach (var item in source)
                copy.Add(Copy(item, visited));
            return copy;
        }

        // Placeholder in `visited` while a collection that can only be built from its finished
        // elements (constructor-based) is being copied: reaching it again means a cycle.
        private static readonly object InProgress = new object();

        /// <summary>
        /// Copies a collection through its elements. Preferred: create it empty (keeping the
        /// source's comparer, so a case-insensitive HashSet stays case-insensitive), register it in
        /// <paramref name="visited"/>, then add deep-copied elements — cycles back to the collection
        /// resolve to the copy. Otherwise rebuild through a constructor taking the elements, where a
        /// cycle cannot be represented and is rejected. Returns null when neither is possible.
        /// </summary>
        private static object CopySequence(IEnumerable source, Type type, Type elementType,
            Dictionary<object, object> visited)
        {
            var add = FindAdder(type, elementType);
            var empty = add == null ? null : CreateEmpty(source, type);
            if (empty != null)
            {
                visited[source] = empty;
                var copies = new List<object>();
                foreach (var item in source)
                    copies.Add(Copy(item, visited));
                // Stack<T> enumerates top-first; pushing in that order would invert it.
                if (IsStack(type)) copies.Reverse();
                foreach (var item in copies)
                    add.Invoke(empty, new[] { item });
                return empty;
            }

            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var ctor = type.GetConstructor(new[] { enumerableType })
                    ?? type.GetConstructor(new[] { typeof(IList<>).MakeGenericType(elementType) });
            if (ctor == null) return null;

            visited[source] = InProgress;
            var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            foreach (var item in source)
                items.Add(Copy(item, visited));
            if (IsStack(type)) ReverseInPlace(items);
            var copy = ctor.Invoke(new object[] { items });
            visited[source] = copy;
            return copy;
        }

        private static bool IsStack(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Stack<>)) return true;
            return false;
        }

        private static System.Reflection.MethodInfo FindAdder(Type type, Type elementType)
        {
            foreach (var name in new[] { "Add", "Enqueue", "Push", "AddLast" })
            {
                var method = type.GetMethod(name, new[] { elementType });
                if (method != null) return method;
            }
            return null;
        }

        private static object CreateEmpty(IEnumerable source, Type type)
        {
            var comparerProperty = type.GetProperty("Comparer");
            var comparer = comparerProperty?.GetValue(source, null);
            if (comparer != null)
            {
                var withComparer = type.GetConstructor(new[] { comparerProperty.PropertyType });
                if (withComparer != null) return withComparer.Invoke(new[] { comparer });
            }
            var parameterless = type.GetConstructor(Type.EmptyTypes);
            return parameterless?.Invoke(null);
        }

        private static void ReverseInPlace(IList items)
        {
            for (int i = 0, j = items.Count - 1; i < j; i++, j--)
            {
                var tmp = items[i];
                items[i] = items[j];
                items[j] = tmp;
            }
        }
    }
}
