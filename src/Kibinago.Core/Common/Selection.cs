using System;
using System.Collections.Generic;

namespace Kibinago.Common
{
    /// <summary>
    /// Holds a single "current selection" of type <typeparamref name="T"/> and raises
    /// <see cref="CurrentChanged"/> when it changes (another record, or the same record as a new
    /// object after a reload) and <see cref="IdentityChanged"/> only when another record is selected. One generic selection replaces the N
    /// same-shaped, hand-written selection services a cascading-selection screen would
    /// otherwise need (one per level). See <see cref="Cascade"/>.
    /// </summary>
    public interface ISelection<T> where T : class
    {
        /// <summary>The current selection, or <c>null</c> if nothing is selected.</summary>
        T Current { get; }

        /// <summary>Sets the current selection. <c>Select(null)</c> clears it.</summary>
        void Select(T item);

        /// <summary>
        /// Raised whenever <see cref="Current"/> is set to a different object: another record, a clear,
        /// or the same record as a new instance (a reloaded list). Subscribe here to show the current item.
        /// </summary>
        event EventHandler CurrentChanged;

        /// <summary>
        /// Raised (before <see cref="CurrentChanged"/>) only when another record is selected or the
        /// selection is cleared — not when the same record is replaced by a new instance. Subscribe here
        /// to reload what depends on which record is selected (<see cref="Cascade"/> does).
        /// </summary>
        event EventHandler IdentityChanged;
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
        /// <remarks>
        /// The same object (or null again) does nothing. The same record as a new object (equal by the
        /// comparer) replaces <see cref="Current"/> and raises <see cref="CurrentChanged"/> only. Another
        /// record or a clear raises <see cref="IdentityChanged"/>, then <see cref="CurrentChanged"/>.
        /// <see cref="Current"/> is updated before either event.
        /// </remarks>
        public void Select(T item)
        {
            // The same object: nothing to tell (also stops clearing an already-empty level from
            // fanning out a pointless cascade).
            if (ReferenceEquals(Current, item)) return;

            // Null-safe: never calls the comparer with a null operand.
            bool sameIdentity = Current != null && item != null && _comparer.Equals(Current, item);

            Current = item;
            if (!sameIdentity)
            {
                var identity = IdentityChanged;
                if (identity != null) identity(this, EventArgs.Empty);
            }
            var handler = CurrentChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        public event EventHandler CurrentChanged;

        /// <inheritdoc />
        public event EventHandler IdentityChanged;
    }
}
