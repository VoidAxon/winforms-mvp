using System;
using System.Windows.Forms;
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

        internal ControlLifecycleController(IViewBase view, IDisposable presenter)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            if (!(view is Control control))
                throw new ArgumentException(
                    "Control hosting requires a Control-backed view.", nameof(view));

            _control = control;
            _presenter = presenter;
        }

        /// <summary>Wires the teardown bridge. Call AFTER <c>Initialize</c>.</summary>
        internal void WireControlEvents()
        {
            _control.Disposed += OnDisposed;
        }

        private void OnDisposed(object sender, EventArgs e)
        {
            _control.Disposed -= OnDisposed;
            _presenter.Dispose(); // -> Cleanup(): unsubscribe service events, release resources
        }
    }
}
