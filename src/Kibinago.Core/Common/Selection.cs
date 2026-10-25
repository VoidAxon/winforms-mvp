using System;
using System.Collections.Generic;

namespace Kibinago.Common
{
    /// <summary>
    /// Holds a single "current selection" of type <typeparamref name="T"/> and raises
    /// <see cref="CurrentChanged"/> when it changes. One generic selection replaces the N
    /// same-shaped, hand-written selection services a cascading-selection screen would
    /// otherwise need (one per level). See <see cref="Cascade"/>.
    /// </summary>
    public interface ISelection<T> where T : class
    {
        /// <summary>The current selection, or <c>null</c> if nothing is selected.</summary>
        T Current { get; }

        /// <summary>Sets the current selection. <c>Select(null)</c> clears it.</summary>
        void Select(T item);

        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        event EventHandler CurrentChanged;
    }

    /// <summary>
    /// Default in-memory <see cref="ISelection{T}"/>. Synchronous; not thread-safe
    /// (selection is a UI-thread concern). Pass an <see cref="IEqualityComparer{T}"/> to
    /// compare by value instead of by reference.
    /// </summary>
    public sealed class Selection<T> : ISelection<T> where T : class
    {
        private readonly IEqualityComparer<T> _comparer;

        // When no comparer is given: if T implements ISelectable, compare by Key (so a reloaded
        // list -- which holds NEW instances -- still matches "the same" row); otherwise fall back
        // to EqualityComparer<T>.Default (reference, or the type's own Equals -- e.g. string).
        // An explicit comparer always wins.
        /// <summary>Creates an empty selection.</summary>
        /// <param name="comparer">
        /// Decides whether a newly selected item is "the same" as the current one; null compares by
        /// <see cref="ISelectable.Key"/> when <typeparamref name="T"/> implements <see cref="ISelectable"/>,
        /// otherwise with <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
        public Selection(IEqualityComparer<T> comparer = null)
        {
            _comparer = comparer
                ?? (typeof(ISelectable).IsAssignableFrom(typeof(T))
                        ? (IEqualityComparer<T>)SelectableKeyComparer<T>.Instance
                        : EqualityComparer<T>.Default);
        }

        /// <inheritdoc />
        public T Current { get; private set; }

        /// <inheritdoc />
        /// <remarks>Selecting an item equal to <see cref="Current"/> does nothing (no event).</remarks>
        public void Select(T item)
        {
            // Short-circuit when unchanged so clearing an already-empty downstream level does not
            // fan out a pointless cascade. Null-safe: never calls the comparer with a null operand.
            bool same = (Current == null && item == null)
                     || (Current != null && item != null && _comparer.Equals(Current, item));
            if (same) return;

            Current = item;
            var handler = CurrentChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public event EventHandler CurrentChanged;
    }
}
