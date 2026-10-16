using System;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Global deep-copy and deep-compare hook used by <see cref="ChangeTracker{T}"/> when the
    /// model does not implement <see cref="ICloneable"/> or provide value equality.
    /// Defaults point to the built-in reflection engines (<see cref="ObjectCloner"/> and
    /// <see cref="ObjectComparer"/>); replace once at application startup to plug in a
    /// third-party deep-copy library, for example:
    /// <code>ChangeTrackerDefaults.Cloner = o => o.DeepClone();   // Force.DeepCloner</code>
    /// </summary>
    /// <remarks>
    /// Writes are NOT thread-safe; set these properties once during application bootstrap.
    /// Setters reject <c>null</c> to surface configuration mistakes immediately.
    /// </remarks>
    public static class ChangeTrackerDefaults
    {
        private static Func<object, object> _cloner = ObjectCloner.DeepCopy;
        private static Func<object, object, bool> _comparer = ObjectComparer.DeepEquals;

        /// <summary>Deep-copy hook. Defaults to <see cref="ObjectCloner.DeepCopy(object)"/>.</summary>
        public static Func<object, object> Cloner
        {
            get { return _cloner; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "ChangeTrackerDefaults.Cloner must not be null.");
                _cloner = value;
            }
        }

        /// <summary>Deep-compare hook. Defaults to <see cref="ObjectComparer.DeepEquals(object, object)"/>.</summary>
        public static Func<object, object, bool> Comparer
        {
            get { return _comparer; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "ChangeTrackerDefaults.Comparer must not be null.");
                _comparer = value;
            }
        }
    }
}
