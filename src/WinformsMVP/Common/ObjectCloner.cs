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
    /// <remarks>
    /// 显式支持的集合:单维数组、<see cref="IList"/>(如 List&lt;T&gt;)、<see cref="IDictionary"/>
    /// (如 Dictionary&lt;K,V&gt;),均按结构逐元素深拷贝。其它 <see cref="IEnumerable"/> 集合
    /// (如 HashSet&lt;T&gt;、Queue&lt;T&gt;)不在显式支持清单内,会回退到逐字段 POCO 拷贝——
    /// 在 net40/net48 上可用,但若需要保证语义请实现 <see cref="ICloneable"/> 或替换
    /// <see cref="ChangeTrackerDefaults.Cloner"/>。
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
            if (source is Delegate) return null;                       // 跳过委托/事件

            object existing;
            if (visited.TryGetValue(source, out existing)) return existing;

            if (type.IsArray) return CopyArray((Array)source, type, visited);

            // 数组已在上面处理(Array 实现了 ICloneable 但 Clone() 是浅拷贝,必须排除)。
            // ICloneable 节点被信任会自行进行深拷贝(由其 Clone() 负责)。
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
    }
}
