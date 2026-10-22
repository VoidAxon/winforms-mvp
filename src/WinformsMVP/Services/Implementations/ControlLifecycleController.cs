using System;
using System.Windows.Forms;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Per-control controller that owns the WinForms lifecycle bridge for one control-hosted view:
    /// it rides the control's <see cref="Control.Disposed"/> event to tear the presenter down. This is
    /// the only place in the control path that knows <see cref="Control"/> — the runtime view-to-Control
    /// boundary lives in this constructor, mirroring how <c>WindowCloseController</c> is the only place
    /// that knows <see cref="Form"/>.
    /// </summary>
    /// <remarks>
    /// Far simpler than the window close machinery on purpose: a control has no Pull gate (it cannot
    /// veto its own removal), no Push sink (it does not request its own close), and no result to
    /// converge. The parent container owns the control's lifetime, so this controller never disposes
    /// the control itself — it only triggers presenter teardown (which unsubscribes long-lived service
    /// events in <c>Cleanup</c>, preventing leaks). Subscribing to <c>Disposed</c> keeps this controller
    /// alive for as long as the control lives; once the event fires it unsubscribes and becomes
    /// collectible.
    /// </remarks>
    internal sealed class ControlLifecycleController
    {
        private readonly Control _control;
        private readonly IDisposable _presenter;
        private bool _controlDisposed;

        internal ControlLifecycleController(IViewBase view, IDisposable presenter)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!(view is Control control))
                throw new ArgumentException(
                    "Control hosting requires a Control-backed view.", nameof(view));
            if (ViewOwnership.IsClaimed(view))
                throw new InvalidOperationException(
                    "This control is already used by another live presenter; each presenter needs its own view.");
            if (ControlDisposalObserver.Instance.IsDisposed(view))
                throw new ObjectDisposedException(control.GetType().Name, "The control is already disposed.");
            _control = control;
            _presenter = presenter;
        }

        internal void WireControlEvents()
        {
            var subscription = ControlDisposalObserver.Instance.ObserveDisposal((IViewBase)_control, OnDisposed);
            // Released with the presenter, so a presenter disposed first leaves no handler on the control.
            ((IPresenterResourceOwner)_presenter).OwnUntilDisposed(subscription);
            ((IPresenterResourceOwner)_presenter).OwnUntilDisposed(ViewOwnership.Claim((IViewBase)_control, _presenter));
        }

        /// <summary>Throws when the control was disposed while the presenter was being connected (the
        /// presenter has already been disposed by then).</summary>
        internal void ThrowIfControlDisposed()
        {
            if (_controlDisposed)
                throw new ObjectDisposedException(_control.GetType().Name,
                    "The control was disposed while its presenter was being connected.");
        }

        private void OnDisposed()
        {
            _controlDisposed = true;
            _presenter.Dispose(); // -> Cleanup(): unsubscribe service events, release resources
        }
    }
}
