using System;
using WinformsMVP.MVP.Views;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Hosting entry for control-backed presenters: attach the view, initialize, and wire lifecycle
    /// teardown — the control-path counterpart of <see cref="WindowPresenterConnectExtensions"/>. The
    /// caller creates the presenter (passing only its own dependencies) and the control, then calls
    /// <c>Connect</c> to bind them.
    /// </summary>
    /// <remarks>
    /// Because <c>Connect</c> runs after the presenter's constructor chain has completed,
    /// <c>OnViewAttached</c> and <c>OnInitialize</c> observe fully-assigned derived fields — there is
    /// no constructor-ordering trap. The single <c>view is Control</c> boundary lives in
    /// <c>ControlLifecycleController</c>, keeping the presenter base free of <c>System.Windows.Forms</c>.
    /// </remarks>
    public static class ControlPresenterConnectExtensions
    {
        /// <summary>
        /// Connects a no-param control presenter to its view: attach + initialize + teardown wiring.
        /// </summary>
        public static void Connect<TView>(this ControlPresenterBase<TView> presenter, TView view)
            where TView : IViewBase
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            // Construct first: validates `view is Control` before any side effect (no half-attach).
            var controller = new ControlLifecycleController(view, presenter);

            presenter.AttachView(view);
            presenter.Initialize();
            controller.WireControlEvents();   // teardown bridge wired after Initialize
        }

        /// <summary>
        /// Connects a parameterized control presenter to its view and initialization parameters.
        /// </summary>
        public static void Connect<TView, TParam>(this ControlPresenterBase<TView, TParam> presenter,
            TView view, TParam param)
            where TView : IViewBase
        {
            if (presenter == null) throw new ArgumentNullException(nameof(presenter));

            var controller = new ControlLifecycleController(view, presenter);

            presenter.AttachView(view);
            presenter.Initialize(param);
            controller.WireControlEvents();
        }
    }
}
