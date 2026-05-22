using System;
using WinformsMVP.Common.Events;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Minimal contract exposed by a view's action binder to its presenter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Views expose an action binder so that the presenter can connect view-side UI events
    /// (button clicks, checkbox toggles, ...) to handlers registered on a <see cref="ViewActionDispatcher"/>.
    /// </para>
    /// <para>
    /// This interface intentionally contains only what the presenter (and framework base classes) need.
    /// Configuration APIs such as <c>Add</c>, <c>RegisterStrategy</c>, and bulk-binding extensions
    /// remain on the concrete <see cref="ViewActionBinder"/> class because they are view-implementation details.
    /// </para>
    /// </remarks>
    public interface IViewActionBinder
    {
        /// <summary>
        /// Raised when a bound UI control triggers its associated action.
        /// </summary>
        /// <remarks>
        /// This is the core outbound signal of the binder. It fires regardless of whether
        /// the binder is wired to a dispatcher (implicit pattern) or not (explicit pattern).
        /// Subscribe to this event to observe, log, or forward action requests.
        /// </remarks>
        event EventHandler<ActionRequestEventArgs> ActionTriggered;

        /// <summary>
        /// Binds all configured controls and connects this binder to the given dispatcher.
        /// Enables automatic dispatch on <see cref="ActionTriggered"/> and automatic CanExecute UI refresh.
        /// </summary>
        /// <param name="dispatcher">The dispatcher to forward actions to.</param>
        void Bind(ViewActionDispatcher dispatcher);

        /// <summary>
        /// Binds all configured controls in event-only mode (no dispatcher).
        /// Only <see cref="ActionTriggered"/> will fire; subscribers must handle dispatch themselves.
        /// </summary>
        void Bind();

        /// <summary>
        /// Releases all UI event subscriptions and any dispatcher hook-ups created by Bind.
        /// </summary>
        void Unbind();
    }
}
