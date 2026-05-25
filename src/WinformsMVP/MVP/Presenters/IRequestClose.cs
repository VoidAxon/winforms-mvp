using System;
using WinformsMVP.Common.Events;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Implemented by presenters that need to actively request the window to close
    /// (the Push direction) and return a typed business result to the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Push direction (this interface):</b> the Presenter raises
    /// <see cref="CloseRequested"/> to close the window and pass a
    /// <typeparamref name="TResult"/> back through
    /// <c>InteractionResult&lt;TResult&gt;</c>.
    /// </para>
    /// <para>
    /// <b>Pull direction (NOT this interface):</b> when something outside the Presenter
    /// initiates the close — user clicks X, system shutdown, parent window closing —
    /// the Presenter receives an <see cref="WinformsMVP.Core.Views.IWindowView.Closing"/>
    /// event and can set <see cref="WindowClosingEventArgs.Cancel"/> to block it.
    /// </para>
    /// <para>
    /// Typical implementation is three lines on top of any <c>WindowPresenterBase</c>:
    /// declare the <see cref="CloseRequested"/> event, and add a small private helper
    /// that builds a <see cref="CloseRequestedEventArgs{TResult}"/> and raises the event.
    /// </para>
    /// </remarks>
    /// <typeparam name="TResult">The business result type returned through
    /// <c>InteractionResult&lt;TResult&gt;</c>.</typeparam>
    public interface IRequestClose<TResult>
    {
        /// <summary>
        /// Raised by the Presenter to request the window to close, carrying the
        /// final business result and status.
        /// </summary>
        event EventHandler<CloseRequestedEventArgs<TResult>> CloseRequested;
    }
}
