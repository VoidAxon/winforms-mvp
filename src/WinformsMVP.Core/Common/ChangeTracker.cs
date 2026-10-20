using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Detects whether data differs from a snapshot ("photo") taken when it was loaded or last
    /// saved. It only <i>detects</i> changes: the caller owns the data, and the tracker never holds
    /// it for the caller.
    /// </summary>
    /// <typeparam name="T">The tracked type. Must be a reference type.</typeparam>
    /// <remarks>
    /// <para>Two ways to use it, each with its own pair of calls:</para>
    /// <list type="table">
    ///   <listheader><term>Style</term><description>Check / accept</description></listheader>
    ///   <item><term>1. The caller keeps editing one object
    ///   (<c>TrackChanges(_order)</c>, then <c>_order.Name = ...</c>)</term>
    ///   <description><see cref="IsChanged"/> / <see cref="AcceptChanges()"/>.
    ///   After <c>AcceptChanges()</c>, <c>IsChanged</c> is false.</description></item>
    ///   <item><term>2. The caller builds a new object each time
    ///   (<c>new Order(View.Name, ...)</c>)</term>
    ///   <description><see cref="IsChangedWith"/> / <see cref="AcceptChanges(T)"/>.
    ///   After <c>AcceptChanges(x)</c>, <c>IsChangedWith(x)</c> is false.</description></item>
    /// </list>
    /// <para>
    /// In style 2 do not call <see cref="AcceptChanges()"/> without an argument: it photographs the
    /// object the tracker was created with, which is stale there.
    /// </para>
    /// <para>
    /// The tracker keeps a reference to the object it was created with (no copy) and a deep-copied
    /// snapshot. Comparison is by content, so editing a value back to its original makes it
    /// unchanged again. Snapshots and comparison come from a <see cref="ChangeTrackingPolicy{T}"/>
    /// fixed at construction (built-in engines, a comparer you pass, or an explicit policy). T's own
    /// Equals/IEquatable is not consulted (entity equality is usually "same Id", which would hide
    /// edits); <see cref="ICloneable"/> is not consulted either (Clone() may be shallow).
    /// </para>
    /// <para>
    /// Not thread-safe: use a tracker, and the object it tracks, from one thread at a time (in
    /// WinForms, the UI thread). For an async save, snapshot the data to save first and call
    /// <see cref="AcceptChanges(T)"/> with it when the save completes.
    /// </para>
    /// <para>
    /// Inside a presenter, prefer <c>TrackChanges(value)</c>: it uses the application-wide
    /// engines and per-type policies configured at startup (<c>mvp.ChangeTracking</c>). A tracker
    /// created with <c>new</c> only ever uses what its constructor was given.
    /// </para>
    /// </remarks>
    public sealed class ChangeTracker<T> : IChangeTracking where T : class
    {
        private readonly T _tracked;         // the caller's object, by reference
        private T _snapshot;                 // deep copy taken at creation / AcceptChanges
        private readonly Func<T, T> _clone;
        private readonly Func<T, T, bool> _comparer;
        private readonly bool _supportsIgnores;
        private ChangeTrackingIgnores _ignores = ChangeTrackingIgnores.None;

        /// <summary>
        /// Tracks <paramref name="value"/> with the built-in engines (reflection deep copy, content
        /// comparison) and takes the first snapshot.
        /// </summary>
        /// <exception cref="ArgumentNullException">When value is null.</exception>
        public ChangeTracker(T value)
            : this(value, ChangeTrackingPolicy<T>.BuiltIn)
        {
        }

        /// <summary>
        /// Tracks <paramref name="value"/>, comparing with <paramref name="comparer"/>; snapshots use
        /// the built-in deep copy.
        /// </summary>
        /// <exception cref="ArgumentNullException">When value or comparer is null.</exception>
        public ChangeTracker(T value, Func<T, T, bool> comparer)
            : this(value, new ChangeTrackingPolicy<T>(
                ChangeTrackingPolicy<T>.BuiltIn.Clone,
                comparer ?? throw new ArgumentNullException(nameof(comparer))))
        {
        }

        /// <summary>
        /// Tracks <paramref name="value"/>, comparing with <paramref name="comparer"/>; snapshots use
        /// the built-in deep copy.
        /// </summary>
        /// <exception cref="ArgumentNullException">When value or comparer is null.</exception>
        public ChangeTracker(T value, IEqualityComparer<T> comparer)
            : this(value, (comparer ?? throw new ArgumentNullException(nameof(comparer))).Equals)
        {
        }

        /// <summary>
        /// Tracks <paramref name="value"/> with an explicit snapshot/compare
        /// <paramref name="policy"/>, fixed for the tracker's lifetime.
        /// </summary>
        /// <exception cref="ArgumentNullException">When value or policy is null.</exception>
        public ChangeTracker(T value, ChangeTrackingPolicy<T> policy)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            _clone = policy.Clone;
            var ignoring = policy.ContentEqualsIgnoring;
            _supportsIgnores = ignoring != null;
            _comparer = ignoring != null
                ? (a, b) => ignoring(a, b, _ignores)
                : policy.ContentEquals;
            _tracked = value;
            _snapshot = _clone(value);
        }

        // ----------------------------------------------------------------------------------
        // Style 1: one object, edited in place
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// Whether the tracked object (the one the tracker was created with) differs from the
        /// snapshot. Compared on every read, so in-place edits are seen immediately.
        /// </summary>
        public bool IsChanged => !_comparer(_snapshot, _tracked);

        /// <summary>
        /// Takes a new snapshot of the tracked object, typically after a successful save.
        /// Afterwards <see cref="IsChanged"/> is false.
        /// </summary>
        /// <remarks>Style 2 (a new object each time) uses <see cref="AcceptChanges(T)"/> instead.</remarks>
        public void AcceptChanges()
        {
            _snapshot = _clone(_tracked);
        }

        // ----------------------------------------------------------------------------------
        // Style 2: a new object each time
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// Whether <paramref name="value"/> differs from the snapshot. Has no side effects.
        /// </summary>
        /// <exception cref="ArgumentNullException">When value is null.</exception>
        public bool IsChangedWith(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return !_comparer(_snapshot, value);
        }

        /// <summary>
        /// Takes a snapshot of <paramref name="value"/> as the new accepted state; the tracked
        /// object is not touched. Afterwards <see cref="IsChangedWith"/>(value) is false.
        /// </summary>
        /// <remarks>
        /// Style 2: call it with the object that was saved. Style 1 with an async save: call it with
        /// the copy that was sent; <see cref="IsChanged"/> then stays true if the user edited the
        /// tracked object while the save ran.
        /// </remarks>
        /// <exception cref="ArgumentNullException">When value is null.</exception>
        public void AcceptChanges(T value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            _snapshot = _clone(value);
        }

        // ----------------------------------------------------------------------------------
        // Ignoring members (screen level: this tracker only)
        // ----------------------------------------------------------------------------------

        /// <summary>What this tracker leaves out of its comparison.</summary>
        public ChangeTrackingIgnores Ignores
        {
            get { return _ignores; }
        }

        /// <summary>
        /// Leaves members of <typeparamref name="T"/> out of change detection, e.g.
        /// <c>Ignore(x =&gt; x.UpdatedAt)</c>. Each expression names one property or field of
        /// <typeparamref name="T"/> itself; for a member of a nested type use
        /// <see cref="Ignore{TOwner}"/>. Returns this tracker.
        /// </summary>
        /// <exception cref="ArgumentException">An expression is a path, a method call, an indexer, or a computed property.</exception>
        /// <exception cref="InvalidOperationException">This tracker compares with a hand-written comparer.</exception>
        public ChangeTracker<T> Ignore(params Expression<Func<T, object>>[] members) => Ignore<T>(members);

        /// <summary>
        /// Leaves members of <typeparamref name="TOwner"/> out of change detection wherever an object of
        /// that type appears (collection elements included), e.g.
        /// <c>Ignore&lt;OrderLine&gt;(l =&gt; l.CachedTotal)</c>. Returns this tracker.
        /// </summary>
        /// <exception cref="ArgumentException">An expression is a path, a method call, an indexer, or a computed property.</exception>
        /// <exception cref="InvalidOperationException">This tracker compares with a hand-written comparer.</exception>
        public ChangeTracker<T> Ignore<TOwner>(params Expression<Func<TOwner, object>>[] members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            EnsureSupportsIgnores();
            var resolved = new IgnoredMember[members.Length];
            for (int i = 0; i < members.Length; i++) resolved[i] = ChangeTrackingIgnores.Resolve(members[i]);
            foreach (var m in resolved) _ignores = _ignores.WithMember(m);
            return this;
        }

        /// <summary>
        /// Leaves members with these names out of change detection on every type (a hand-written
        /// backing field such as <c>_updatedAt</c> counts as <c>UpdatedAt</c>). Returns this tracker.
        /// </summary>
        /// <exception cref="InvalidOperationException">This tracker compares with a hand-written comparer.</exception>
        public ChangeTracker<T> IgnoreMember(params string[] names)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            foreach (var name in names)
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("Member names must not be empty.", nameof(names));
            EnsureSupportsIgnores();
            foreach (var n in names) _ignores = _ignores.WithMemberName(n);
            return this;
        }

        /// <summary>Leaves values of <typeparamref name="TType"/> (and derived types) out of change detection. Returns this tracker.</summary>
        /// <exception cref="InvalidOperationException">This tracker compares with a hand-written comparer.</exception>
        public ChangeTracker<T> IgnoreType<TType>()
        {
            EnsureSupportsIgnores();
            _ignores = _ignores.WithType(typeof(TType));
            return this;
        }

        private void EnsureSupportsIgnores()
        {
            if (!_supportsIgnores)
                throw new InvalidOperationException(
                    "This tracker compares with a hand-written comparer (a comparer argument or a " +
                    "ChangeTrackingPolicy built from delegates), which cannot be told what to ignore. " +
                    "Leave the members out in that comparer, or create the tracker without one.");
        }
    }
}
