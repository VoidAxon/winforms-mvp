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
            var copy = (IList)Activator.CreateInstance(type);
            visited[source] = copy;
            foreach (var item in source)
                copy.Add(Copy(item, visited));
            return copy;
        }
    }
}
