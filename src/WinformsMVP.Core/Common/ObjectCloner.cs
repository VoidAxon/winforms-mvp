using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using WinformsMVP.Common.Internal;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Pragmatic reflection-based deep copy. Serves as the default implementation for
    /// <see cref="ChangeTrackerDefaults"/> and can also be called directly.
    /// </summary>
    /// <remarks>
    /// Explicitly supported collections: single-dimensional arrays, <see cref="IList"/>
    /// (e.g. List&lt;T&gt;), and <see cref="IDictionary"/> (e.g. Dictionary&lt;K,V&gt;) —
    /// all deep-copied element by element. Other <see cref="System.Collections.IEnumerable"/>
    /// collections (e.g. HashSet&lt;T&gt;, Queue&lt;T&gt;) are not explicitly supported and
    /// fall back to field-by-field POCO copy — usable on net40/net48, but for guaranteed
    /// semantics implement <see cref="ICloneable"/> or replace
    /// <see cref="ChangeTrackerDefaults.Cloner"/>.
    /// <para>
    /// Known limitations of the <see cref="IList"/>/<see cref="IDictionary"/> paths:
    /// </para>
    /// <list type="bullet">
    ///   <item>The concrete type must have a public parameterless constructor.
    ///   Read-only or immutable collections (e.g. <c>ReadOnlyCollection&lt;T&gt;</c>) throw —
    ///   implement <see cref="ICloneable"/> or swap the cloner for those.</item>
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
            if (visited.TryGetValue(source, out existing)) return existing;

            if (type.IsArray) return CopyArray((Array)source, type, visited);

            // Arrays are handled above (Array implements ICloneable but Clone() is shallow — must be excluded).
            // ICloneable nodes are trusted to perform their own deep copy via Clone().
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

            var elementType = DeepReflection.GetSequenceElementType(type);
            if (elementType != null)
            {
                var sequence = CopySequence((IEnumerable)source, type, elementType, visited);
                if (sequence != null) return sequence;
            }

            if (DeepReflection.IsUnsupported(type))
                throw new NotSupportedException(
                    "ObjectCloner cannot deep-copy type '" + type.FullName +
                    "'. Implement ICloneable on the model, or set ChangeTrackerDefaults.Cloner " +
                    "to a deep-copy library.");

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
                    "'. Implement ICloneable on the model, or set ChangeTrackerDefaults.Cloner.");
            }

            var copy = (IList)Activator.CreateInstance(type);
            visited[source] = copy;
            foreach (var item in source)
                copy.Add(Copy(item, visited));
            return copy;
        }

        /// <summary>
        /// Rebuilds a collection from deep-copied elements through a constructor taking
        /// <c>IEnumerable&lt;T&gt;</c> (plus the source's comparer when it exposes one, so a
        /// case-insensitive HashSet stays case-insensitive). Returns null when no such constructor
        /// exists. Elements are copied before the collection is built, so a cycle back to this
        /// collection is not supported (rare for set/queue/stack element types).
        /// </summary>
        private static object CopySequence(IEnumerable source, Type type, Type elementType,
            Dictionary<object, object> visited)
        {
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            foreach (var item in source)
                items.Add(Copy(item, visited));

            // Stack<T> enumerates top-first but its constructor pushes in order: reverse to keep the top.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Stack<>))
                ReverseInPlace(items);

            object copy = null;
            var comparerProperty = type.GetProperty("Comparer");
            var comparer = comparerProperty?.GetValue(source, null);
            if (comparer != null)
            {
                var withComparer = type.GetConstructor(new[] { enumerableType, comparerProperty.PropertyType });
                if (withComparer != null) copy = withComparer.Invoke(new[] { items, comparer });
            }
            if (copy == null)
            {
                var ctor = type.GetConstructor(new[] { enumerableType })
                        ?? type.GetConstructor(new[] { typeof(IList<>).MakeGenericType(elementType) });
                if (ctor == null) return null;
                copy = ctor.Invoke(new object[] { items });
            }

            visited[source] = copy;
            return copy;
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
