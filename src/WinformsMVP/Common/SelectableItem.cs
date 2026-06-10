using System;
using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Exposes a key that represents the identity of a selection. When <typeparamref name="T"/>
    /// of a <see cref="SelectionStore{T}"/> implements this interface, the store compares
    /// selections by this <see cref="Key"/> (via <see cref="SelectableKeyComparer{T}"/>) instead
    /// of by reference — so a reloaded list, which holds NEW instances, still matches "the same"
    /// row. Return a value-comparable key (int / string / Guid / enum, ...).
    /// </summary>
    public interface ISelectable
    {
        /// <summary>The identity of this item. Two items with equal keys are "the same" selection.</summary>
        object Key { get; }
    }

    /// <summary>
    /// A ready-made <see cref="ISelectable"/> wrapper around a value that is its own identity.
    /// Intended for value types (primitives / enums / <see cref="Guid"/> / <see cref="DateTime"/> /
    /// <c>bool</c>), which cannot be used as <c>SelectionStore&lt;T&gt;</c>'s <c>T</c> directly
    /// (that <c>where T : class</c> constraint models "no selection = null"). Also usable as a
    /// list/combo data-source item thanks to <see cref="Text"/> / <see cref="ToString"/>.
    /// For domain entities, implement <see cref="ISelectable"/> on the entity instead of wrapping it.
    /// </summary>
    public sealed class SelectableItem<T> : ISelectable
    {
        /// <summary>The wrapped value; also serves as this item's identity (<see cref="Key"/>).</summary>
        public T Value { get; private set; }

        /// <summary>Display text. Defaults to <c>Value.ToString()</c> when not supplied.</summary>
        public string Text { get; private set; }

        public SelectableItem(T value, string text = null)
        {
            Value = value;
            Text = text != null ? text : (value != null ? value.ToString() : null);
        }

        /// <summary>Identity is the value itself.</summary>
        public object Key { get { return Value; } }

        /// <summary>Shows the display text when bound to a list/combo.</summary>
        public override string ToString() { return Text; }

        public override bool Equals(object obj)
        {
            var other = obj as SelectableItem<T>;
            return other != null && object.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }
    }

    /// <summary>
    /// Factory helpers for <see cref="SelectableItem{T}"/>. The generic methods give type
    /// inference, so callers avoid writing <c>new SelectableItem&lt;int&gt;(...)</c>
    /// (same reason as <c>Tuple.Create</c>).
    /// </summary>
    public static class SelectableItem
    {
        /// <summary>Wrap a single value. Type is inferred: <c>SelectableItem.Of(2025)</c>.</summary>
        public static SelectableItem<T> Of<T>(T value, string text = null)
        {
            return new SelectableItem<T>(value, text);
        }

        /// <summary>Wrap a sequence of values (handy for list/combo data sources).</summary>
        public static IList<SelectableItem<T>> From<T>(
            IEnumerable<T> values, Func<T, string> text = null)
        {
            var list = new List<SelectableItem<T>>();
            if (values != null)
            {
                foreach (var v in values)
                    list.Add(new SelectableItem<T>(v, text != null ? text(v) : null));
            }
            return list;
        }

        /// <summary>Wrap every value of an enum type.</summary>
        public static IList<SelectableItem<TEnum>> FromEnum<TEnum>(Func<TEnum, string> text = null)
            where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum)
                throw new ArgumentException(typeof(TEnum).Name + " is not an enum type.");

            return From((TEnum[])Enum.GetValues(typeof(TEnum)), text);
        }
    }
}
