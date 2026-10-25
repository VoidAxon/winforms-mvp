using System;

namespace Kibinago.Common
{
    /// <summary>
    /// Decides whether two object graphs have the same content, for
    /// <see cref="ChangeTracker{T}.IsChanged"/>. Replace the built-in engine
    /// (<see cref="BuiltInObjectContentComparer"/>) to plug in a dedicated library.
    /// </summary>
    /// <remarks>
    /// Either argument may be <c>null</c>. One instance serves the whole application, so it must be
    /// safe to call concurrently. An adapter over another library maps <see cref="ChangeTrackingIgnores"/>
    /// onto that library's ignore settings; if the library cannot express one of them, throw
    /// <see cref="NotSupportedException"/> rather than compare it anyway.
    /// </remarks>
    public interface IObjectContentComparer
    {
        /// <summary>
        /// True when <paramref name="left"/> and <paramref name="right"/> have the same content,
        /// leaving out what <paramref name="ignores"/> lists (<see cref="ChangeTrackingIgnores.None"/>
        /// when the tracker ignores nothing).
        /// </summary>
        bool ContentEquals(object left, object right, ChangeTrackingIgnores ignores);
    }
}
