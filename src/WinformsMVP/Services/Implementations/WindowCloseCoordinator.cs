using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Per-window coordinator that decides whether a close should run the Presenter's
    /// cancellation gate (<see cref="IWindowView.Closing"/>). A close the Presenter itself
    /// initiated (via <c>IRequestClose.CloseRequested</c>) is already authorized, so the gate
    /// — and any dirty-state prompt it raises — is skipped.
    /// </summary>
    /// <remarks>
    /// This replaces the previous reliance on the "finalize the dirty flag before RaiseClose"
    /// ordering convention with an explicit, per-window state. Because there is one coordinator
    /// per <see cref="System.Windows.Forms.Form"/>, it is safe with multiple open non-modal
    /// windows — there is no shared navigator-level flag to corrupt.
    /// </remarks>
    internal sealed class WindowCloseCoordinator
    {
        private readonly IWindowView _view;
        private bool _initiatedByPresenter;

        internal WindowCloseCoordinator(IWindowView view) => _view = view;

        /// <summary>
        /// Marks the in-flight close as Presenter-initiated. Called immediately before
        /// <c>Form.Close()</c> in the <c>CloseRequested</c> handler.
        /// </summary>
        internal void BeginPresenterClose() => _initiatedByPresenter = true;

        /// <summary>
        /// Invoked from the FormClosing bridge. Returns <c>true</c> if the close must be vetoed.
        /// Presenter-initiated closes never run the gate; everything else forwards to the View
        /// so the Presenter's <see cref="IWindowView.Closing"/> handler can decide.
        /// </summary>
        internal bool ShouldCancel(CloseReason reason)
        {
            if (_initiatedByPresenter) return false;

            var args = new WindowClosingEventArgs(reason);
            _view.OnClosing(args);
            return args.Cancel;
        }
    }
}
