using System;
using System.Collections.Generic;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Holds a single "current selection" of type <typeparamref name="T"/> and raises
    /// <see cref="CurrentChanged"/> when it changes. One generic store replaces the N
    /// same-shaped, hand-written selection services a cascading-selection screen would
    /// otherwise need (one per level). See <see cref="Cascade"/>.
    /// </summary>
    public interface ISelectionStore<T> where T : class
    {
        /// <summary>The current selection, or <c>null</c> if nothing is selected.</summary>
        T Current { get; }

        /// <summary>Sets the current selection. <c>Select(null)</c> clears it.</summary>
        void Select(T item);

        /// <summary>Raised when <see cref="Current"/> changes.</summary>
        event EventHandler CurrentChanged;
    }

    /// <summary>
    /// Default in-memory <see cref="ISelectionStore{T}"/>. Synchronous; not thread-safe
    /// (selection is a UI-thread concern). Pass an <see cref="IEqualityComparer{T}"/> to
    /// compare by value instead of by reference.
    /// </summary>
    public sealed class SelectionStore<T> : ISelectionStore<T> where T : class
    {
        private readonly IEqualityComparer<T> _comparer;

        // Defaults to EqualityComparer<T>.Default. Give entities Id-based Equals (or pass a
        // comparer) so a reloaded list -- which holds NEW instances -- still matches "the same" row.
        public SelectionStore(IEqualityComparer<T> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public T Current { get; private set; }

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

        public event EventHandler CurrentChanged;
    }
}
