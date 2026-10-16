using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Compares selections by their <see cref="ISelectable.Key"/> rather than by reference.
    /// <see cref="SelectionStore{T}"/> picks this automatically (no explicit wiring) when its
    /// <typeparamref name="T"/> implements <see cref="ISelectable"/>, so a reloaded list — whose
    /// items are NEW instances with the same keys — still matches "the same" selection.
    /// </summary>
    /// <remarks>
    /// Constrained to <c>where T : class</c> (not <c>ISelectable</c>) so it composes with the
    /// store's own <c>class</c> constraint; non-<see cref="ISelectable"/> operands compare unequal.
    /// Keeps equality scoped to the selection store — the entity's own <c>Equals</c> stays untouched.
    /// </remarks>
    public sealed class SelectableKeyComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly SelectableKeyComparer<T> Instance = new SelectableKeyComparer<T>();

        public bool Equals(T x, T y)
        {
            if (ReferenceEquals(x, y)) return true;
            var sx = x as ISelectable;
            var sy = y as ISelectable;
            return sx != null && sy != null && object.Equals(sx.Key, sy.Key);
        }

        public int GetHashCode(T o)
        {
            var s = o as ISelectable;
            return s != null && s.Key != null ? s.Key.GetHashCode() : 0;
        }
    }
}
