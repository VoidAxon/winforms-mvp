using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// How a <see cref="ChangeTracker{T}"/> snapshots and compares one model type: pass it to the
    /// tracker directly, or register it in the service provider so <c>TrackChanges</c> picks it up
    /// for that exact <typeparamref name="T"/>. Immutable.
    /// </summary>
    /// <example>
    /// <code>
    /// // Order.Clone() is a verified deep copy; ignore the audit timestamp when comparing.
    /// new ChangeTrackingPolicy&lt;Order&gt;(o => (Order)o.Clone(), (a, b) => a.SameEditableContent(b));
    /// </code>
    /// </example>
    public sealed class ChangeTrackingPolicy<T> where T : class
    {
        /// <param name="clone">Deep copy used for snapshots.</param>
        /// <param name="contentEquals">Content comparison used by <c>IsChanged</c>.</param>
        public ChangeTrackingPolicy(Func<T, T> clone, Func<T, T, bool> contentEquals)
        {
            Clone = clone ?? throw new ArgumentNullException(nameof(clone));
            ContentEquals = contentEquals ?? throw new ArgumentNullException(nameof(contentEquals));
        }

        /// <summary>Deep copy used for snapshots.</summary>
        public Func<T, T> Clone { get; }

        /// <summary>Content comparison used by <c>IsChanged</c>.</summary>
        public Func<T, T, bool> ContentEquals { get; }

        /// <summary>
        /// The comparison with ignores applied, when the policy is backed by an
        /// <see cref="IObjectContentComparer"/>; <c>null</c> for a hand-written <see cref="ContentEquals"/>,
        /// which cannot be told what to ignore.
        /// </summary>
        internal Func<T, T, ChangeTrackingIgnores, bool> ContentEqualsIgnoring { get; private set; }

        /// <summary>A policy backed by application-wide engines.</summary>
        internal static ChangeTrackingPolicy<T> From(IObjectCloner cloner, IObjectContentComparer comparer)
        {
            if (cloner == null) throw new ArgumentNullException(nameof(cloner));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            return new ChangeTrackingPolicy<T>(
                v => (T)cloner.DeepClone(v),
                (a, b) => comparer.ContentEquals(a, b, ChangeTrackingIgnores.None))
            {
                ContentEqualsIgnoring = (a, b, ignores) => comparer.ContentEquals(a, b, ignores),
            };
        }

        /// <summary>The built-in engines (reflection deep copy, content comparison).</summary>
        internal static readonly ChangeTrackingPolicy<T> BuiltIn =
            From(BuiltInObjectCloner.Instance, BuiltInObjectContentComparer.Instance);
    }
}
