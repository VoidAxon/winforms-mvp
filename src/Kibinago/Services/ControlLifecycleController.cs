using System;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Views;

namespace Kibinago.Services
{
    /// <summary>
    /// Per-control controller that owns the WinForms lifecycle bridge for one control-hosted view:
    /// it rides the control's <see cref="System.ComponentModel.Component.Disposed"/> event to tear the presenter down. This is
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

        internal ControlLifecycleController(IView view, IDisposable presenter)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));
            if (!(view is Control control))
                throw new ArgumentException(
                    "Control hosting requires a Control-backed view.", nameof(view));
            if (view is Form)
                throw new ArgumentException(
                    view.GetType().Name + " is a Form: a window is connected to a window presenter or opened with " +
                    "Navigator. A control presenter hosts a UserControl.", nameof(view));
            // A presenter that is already attached (or owned by a parent) is rejected before anything is
            // wired, so the failure path cannot dispose it and break its current owner.
            if (presenter is IViewAttachable attachable && (attachable.IsViewAttached || attachable.IsInitialized))
                throw new InvalidOperationException(
                    presenter.GetType().Name + " is already attached to a view; a presenter is attached once.");
            if (presenter is IComposablePresenter composable && composable.IsDisposed)
                throw new ObjectDisposedException(presenter.GetType().Name);
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
            var subscription = ControlDisposalObserver.Instance.ObserveDisposal((IView)_control, OnDisposed);
            // Released with the presenter, so a presenter disposed first leaves no handler on the control.
            ((IPresenterResourceOwner)_presenter).OwnUntilDisposed(subscription);
            ((IPresenterResourceOwner)_presenter).OwnUntilDisposed(ViewOwnership.Claim((IView)_control, _presenter));
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
