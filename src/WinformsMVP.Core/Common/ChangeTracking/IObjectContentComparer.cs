namespace WinformsMVP.Common
{
    /// <summary>
    /// Decides whether two object graphs have the same content, for
    /// <see cref="ChangeTracker{T}.IsChanged"/>. Replace the built-in engine
    /// (<see cref="BuiltInObjectContentComparer"/>) to plug in a dedicated library.
    /// </summary>
    /// <remarks>
    /// Either argument may be <c>null</c>. One instance serves the whole application, so it must be
    /// safe to call concurrently.
    /// </remarks>
    public interface IObjectContentComparer
    {
        /// <summary>True when <paramref name="left"/> and <paramref name="right"/> have the same content.</summary>
        bool ContentEquals(object left, object right);
    }
}
