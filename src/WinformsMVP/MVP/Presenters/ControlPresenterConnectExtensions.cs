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

            // Construct first: validates `view is Control` (and not disposed) before any side effect.
            var controller = new ControlLifecycleController(view, presenter);
            Run(presenter, controller, () =>
            {
                ((IViewAttacher<TView>)presenter).AttachView(view);
                ((IInitializable)presenter).Initialize();
            });
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
            Run(presenter, controller, () =>
            {
                ((IViewAttacher<TView>)presenter).AttachView(view);
                ((IInitializable<TParam>)presenter).Initialize(param);
            });
        }

        // The teardown bridge is wired BEFORE attach/initialize, so a control disposed while the
        // presenter initializes still tears it down. A presenter whose attach/initialize failed can
        // never run: dispose it (releasing anything OnViewAttached subscribed) and rethrow, matching
        // WindowNavigator's failure contract.
        private static void Run(IDisposable presenter, ControlLifecycleController controller, Action connect)
        {
            controller.WireControlEvents();
            try { connect(); }
            catch
            {
                presenter.Dispose();
                throw;
            }
            controller.ThrowIfControlDisposed();
        }
    }
}
