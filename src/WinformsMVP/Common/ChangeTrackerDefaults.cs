using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Global deep-clone and deep-compare hooks used by <see cref="ChangeTracker{T}"/> when the
    /// model type does not implement <see cref="ICloneable"/> or value equality.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both properties default to the built-in caching reflection engine
    /// (<see cref="ObjectCloner.DeepCopy"/> and <see cref="ObjectComparer.DeepEquals"/>).
    /// </para>
    /// <para>
    /// To plug in a third-party library, assign the hook once at application startup:
    /// <code>ChangeTrackerDefaults.Cloner = o => o.DeepClone();   // e.g. Force.DeepCloner</code>
    /// </para>
    /// <para>
    /// Write access is not thread-safe. Set these properties once during startup before
    /// any <see cref="ChangeTracker{T}"/> instances are created.
    /// </para>
    /// </remarks>
    public static class ChangeTrackerDefaults
    {
        /// <summary>
        /// Factory delegate that produces a deep copy of the supplied object.
        /// The argument and return value are both typed as <see cref="object"/> so the hook
        /// is independent of the generic type parameter on <see cref="ChangeTracker{T}"/>.
        /// </summary>
        public static Func<object, object> Cloner { get; set; } = ObjectCloner.DeepCopy;

        /// <summary>
        /// Predicate delegate that returns <c>true</c> when two object graphs are structurally equal.
        /// The arguments are typed as <see cref="object"/> so the hook is independent of the
        /// generic type parameter on <see cref="ChangeTracker{T}"/>.
        /// </summary>
        public static Func<object, object, bool> Comparer { get; set; } = ObjectComparer.DeepEquals;
    }
}
