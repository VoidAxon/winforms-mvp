using System;

namespace Kibinago.Views
{
    /// <summary>
    /// Where the pages of a <see cref="Kibinago.Presenters.PageStack"/> are shown: a view capability, like
    /// <see cref="IActionBindable"/>. The parent view interface exposes it (<c>IPageHost Pages { get; }</c>) and the
    /// view implements it with Kibinago's <c>PageHost</c> panel; tests use <c>Kibinago.Testing.FakePageHost</c>; an
    /// application may implement it for a layout of its own.
    /// </summary>
    public interface IPageHost
    {
        /// <summary>
        /// Creates a new view for <paramref name="viewInterface"/>, shows it on top with <paramref name="title"/> as its
        /// breadcrumb text (the page below stays alive, hidden) and returns it.
        /// </summary>
        /// <param name="viewInterface">The page's view interface (mapped to a control at startup).</param>
        /// <param name="title">The page's breadcrumb text.</param>
        /// <param name="services">The pushing page presenter's services (its own, else its owner's) — create the view
        /// from these so it shares the presenter's scope and container.</param>
        /// <returns>The new view.</returns>
        /// <remarks>Throw, with nothing added, when no new usable view can be made (no mapping; null; a window; a
        /// disposed control; a control already placed elsewhere or used by a live presenter). A host that fails midway
        /// undoes its own changes before it throws.</remarks>
        IControlView AddPage(Type viewInterface, string title, IServiceProvider services);

        /// <summary>
        /// Removes <paramref name="page"/> — any level, not only the top — and disposes it; the new top is shown.
        /// Tolerates a control that is already disposed.
        /// </summary>
        /// <param name="page">A view this host added.</param>
        void RemovePage(IControlView page);
    }
}
