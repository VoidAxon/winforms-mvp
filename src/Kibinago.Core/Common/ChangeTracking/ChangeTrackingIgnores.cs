using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Kibinago.Common.Internal;

namespace Kibinago.Common
{
    /// <summary>
    /// A member excluded from change detection: <see cref="Name"/> of <see cref="Owner"/>, wherever an
    /// object of that type appears in the compared graph (collection elements included).
    /// </summary>
    public sealed class IgnoredMember
    {
        internal IgnoredMember(Type owner, MemberInfo member, FieldInfo field)
        {
            Owner = owner;
            Member = member;
            Field = field;
        }

        /// <summary>The type whose member is ignored (also applies to types derived from it).</summary>
        public Type Owner { get; }

        /// <summary>The property or field, as written in the <c>Ignore</c> expression.</summary>
        public MemberInfo Member { get; }

        /// <summary>The member's name.</summary>
        public string Name => Member.Name;

        // The field the built-in comparer skips (a property's backing field).
        internal FieldInfo Field { get; }
    }

    /// <summary>
    /// What a <see cref="ChangeTracker{T}"/> leaves out of its comparison, in the three forms most
    /// object-comparison libraries also understand: a member of a type (<c>"Order.UpdatedAt"</c>),
    /// a member name on any type, and a whole type. Immutable. The built-in comparer applies it
    /// directly; an <see cref="IObjectContentComparer"/> adapter over another library maps each list
    /// onto that library's settings (e.g. CompareNETObjects <c>MembersToIgnore</c> /
    /// <c>TypesToIgnore</c>).
    /// </summary>
    public sealed class ChangeTrackingIgnores
    {
        /// <summary>Nothing ignored.</summary>
        public static readonly ChangeTrackingIgnores None =
            new ChangeTrackingIgnores(new IgnoredMember[0], new string[0], new Type[0]);

        private ChangeTrackingIgnores(IgnoredMember[] members, string[] names, Type[] types)
        {
            Members = members;
            MemberNames = names;
            Types = types;
        }

        /// <summary>Members of specific types (<c>Ignore(x =&gt; x.P)</c>, <c>Ignore&lt;TOwner&gt;(x =&gt; x.P)</c>).</summary>
        public IReadOnlyList<IgnoredMember> Members { get; }

        /// <summary>Member names ignored on every type (<c>IgnoreMember("UpdatedAt")</c>).</summary>
        public IReadOnlyList<string> MemberNames { get; }

        /// <summary>Types whose values are not compared (<c>IgnoreType&lt;Audit&gt;()</c>).</summary>
        public IReadOnlyList<Type> Types { get; }

        /// <summary>True when nothing is ignored.</summary>
        public bool IsEmpty => Members.Count == 0 && MemberNames.Count == 0 && Types.Count == 0;

        internal ChangeTrackingIgnores WithMember(IgnoredMember member)
            => new ChangeTrackingIgnores(Members.Concat(new[] { member }).ToArray(), (string[])MemberNames, (Type[])Types);

        internal ChangeTrackingIgnores WithMemberName(string name)
            => new ChangeTrackingIgnores((IgnoredMember[])Members, MemberNames.Concat(new[] { name }).ToArray(), (Type[])Types);

        internal ChangeTrackingIgnores WithType(Type type)
            => new ChangeTrackingIgnores((IgnoredMember[])Members, (string[])MemberNames, Types.Concat(new[] { type }).ToArray());

        // ---- used by the built-in comparer ----------------------------------------------------

        /// <summary>Values of an ignored type (or a type derived from one) are not compared.</summary>
        internal bool SkipsType(Type type)
        {
            foreach (var ignored in Types)
                if (ignored.IsAssignableFrom(type)) return true;
            return false;
        }

        /// <summary>Whether <paramref name="field"/> of an object of runtime type <paramref name="owner"/> is skipped.</summary>
        internal bool SkipsField(Type owner, FieldInfo field)
        {
            if (SkipsType(field.FieldType)) return true;

            if (MemberNames.Count > 0)
            {
                var logical = LogicalName(field);
                foreach (var name in MemberNames)
                    if (string.Equals(name, logical, StringComparison.OrdinalIgnoreCase)) return true;
            }

            foreach (var member in Members)
                if (member.Field == field && member.Owner.IsAssignableFrom(owner)) return true;
            return false;
        }

        // ---- resolving Ignore(...) arguments ---------------------------------------------------

        internal static IgnoredMember Resolve<TOwner>(Expression<Func<TOwner, object>> expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));

            var body = expression.Body;
            while (body is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
                body = unary.Operand;   // value-type members are boxed to object

            var access = body as MemberExpression;
            if (access == null || !(access.Expression is ParameterExpression))
            {
                if (access?.Expression is MemberExpression parent)
                    throw new ArgumentException(
                        "Ignore takes a member of the type itself, not a path: instead of " + expression +
                        ", write Ignore<" + parent.Type.Name + ">(x => x." + access.Member.Name +
                        "), which ignores that member wherever a " + parent.Type.Name + " appears.",
                        nameof(expression));
                throw new ArgumentException(
                    "Ignore takes a property or field of the type, such as x => x.UpdatedAt; " +
                    expression + " is not one (method calls and indexers are not supported).",
                    nameof(expression));
            }

            return new IgnoredMember(typeof(TOwner), access.Member, FieldFor(typeof(TOwner), access.Member));
        }

        // The field that stores a member's value: the field itself, an auto-property's backing field,
        // or a hand-written backing field named after the property (_name, m_name, name).
        private static FieldInfo FieldFor(Type owner, MemberInfo member)
        {
            var field = member as FieldInfo;
            if (field != null) return field;

            var property = (PropertyInfo)member;
            var backing = property.DeclaringType.GetField(
                "<" + property.Name + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (backing != null) return backing;

            var candidates = DeepReflection.GetFields(owner)
                .Where(f => f.FieldType == property.PropertyType
                            && string.Equals(StripPrefix(f.Name), property.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length == 1) return candidates[0];

            throw new ArgumentException(
                "Cannot ignore " + owner.Name + "." + property.Name + ": no field stores its value " +
                (candidates.Length == 0
                    ? "(a computed property is derived from other members, which are compared)."
                    : "unambiguously (several fields match its name).") +
                " Ignore the field itself, or use IgnoreMember with the field's name.",
                nameof(member));
        }

        // The name a field stands for: an auto-property's name, or a hand-written field without its prefix.
        private static string LogicalName(FieldInfo field)
        {
            var name = field.Name;
            if (name.Length > 2 && name[0] == '<')
            {
                var end = name.IndexOf('>');
                if (end > 1) return name.Substring(1, end - 1);
            }
            return StripPrefix(name);
        }

        private static string StripPrefix(string name)
        {
            if (name.StartsWith("m_", StringComparison.Ordinal)) return name.Substring(2);
            if (name.StartsWith("_", StringComparison.Ordinal)) return name.Substring(1);
            return name;
        }
    }
}
