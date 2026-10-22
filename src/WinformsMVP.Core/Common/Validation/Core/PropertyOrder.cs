using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WinformsMVP.Common.Validation.Attributes;

namespace WinformsMVP.Common.Validation.Core
{
    /// <summary>
    /// A model type's public properties in the screen's order: by <see cref="ValidationOrderAttribute"/>
    /// (0 when absent), then base class before derived, then declaration order (metadata token, which
    /// follows the source within one type; partial classes split over files have no defined order —
    /// number them).
    /// </summary>
    internal static class PropertyOrder
    {
        private static readonly ConcurrentDictionary<Type, string[]> Cache = new ConcurrentDictionary<Type, string[]>();

        public static IReadOnlyList<string> Of(Type type) => Cache.GetOrAdd(type, Compute);

        private static string[] Compute(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Where(p => p.GetIndexParameters().Length == 0)
                   .OrderBy(p => p.GetCustomAttribute<ValidationOrderAttribute>(inherit: true)?.Order ?? 0)
                   .ThenBy(p => Depth(p.DeclaringType))
                   .ThenBy(p => p.MetadataToken)
                   .Select(p => p.Name)
                   .Distinct(StringComparer.Ordinal)   // a property hidden with `new` appears twice
                   .ToArray();

        private static int Depth(Type type)
        {
            var depth = 0;
            for (var t = type.BaseType; t != null; t = t.BaseType) depth++;
            return depth;
        }
    }
}
