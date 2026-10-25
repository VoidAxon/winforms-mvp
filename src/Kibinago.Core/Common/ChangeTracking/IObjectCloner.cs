namespace Kibinago.Common
{
    /// <summary>
    /// Deep-copies an object graph for <see cref="ChangeTracker{T}"/> snapshots. Replace the
    /// built-in engine (<see cref="BuiltInObjectCloner"/>) to plug in a dedicated library.
    /// </summary>
    /// <remarks>
    /// The copy must share no mutable state with the source that affects change tracking, must
    /// compare content-equal to it, and unsupported graphs must fail with an exception rather than
    /// produce a partial copy. One instance serves the whole application, so it must be safe to
    /// call concurrently.
    /// </remarks>
    public interface IObjectCloner
    {
        /// <summary>Returns a deep copy of <paramref name="value"/> (<c>null</c> for <c>null</c>).</summary>
        object DeepClone(object value);
    }
}
