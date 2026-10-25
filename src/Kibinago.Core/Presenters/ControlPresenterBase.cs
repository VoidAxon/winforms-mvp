using System;
using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Base class for presenters that manage Control / UserControl views.
    /// Use this class when the presenter does not require initialization parameters.
    /// </summary>
    /// <remarks>
    /// Construction is two-phase, identical in shape to <see cref="WindowPresenterBase{TView}"/>:
    /// the constructor takes only the presenter's own dependencies, then the view is supplied via
    /// <c>AttachView</c> and initialization runs via <c>Initialize</c>. The
    /// <c>Connect</c> extension (see <c>ControlPresenterConnectExtensions</c> in the Kibinago package) drives that
    /// sequence and wires lifecycle teardown. This keeps the presenter free of any
    /// <c>System.Windows.Forms</c> dependency, and — because <c>Initialize</c> runs after the
    /// constructor chain has completed — it removes the constructor-ordering trap where
    /// <c>OnInitialize</c> could observe not-yet-assigned derived fields.
    /// </remarks>
    /// <typeparam name="TView">The view interface type, must implement IViewBase</typeparam>
    public abstract class ControlPresenterBase<TView> :
        PresenterBase<TView>,
        IViewAttacher<TView>,
        IInitializable
        where TView : IViewBase
    {
        /// <summary>
        /// Creates the presenter. Derived constructors take only the presenter's own dependencies; the
        /// view is attached and the presenter initialized later, by <c>Connect</c> or <c>AttachChild</c>.
        /// </summary>
        protected ControlPresenterBase()
        {
        }

        /// <summary>
        /// Attaches the view to this presenter. Called by the <c>Connect</c> extension.
        /// </summary>
        void IViewAttacher<TView>.AttachView(TView view)
        {
            SetView(view);
        }

        /// <summary>
        /// Initializes the presenter. Called after the view is attached.
        /// At this point the View property and all derived fields are guaranteed to be set.
        /// </summary>
        void IInitializable.Initialize() => InitializeCore(RegisterViewActions, OnInitialize);

        /// <summary>
        /// Override to register view actions (UI event bindings).
        /// </summary>
        protected virtual void RegisterViewActions() { }

        /// <summary>
        /// Override to perform initialization logic after the view is attached.
        /// At this point the View property is guaranteed to be set.
        /// Note: the control may not have a parent container or window handle yet — handle-dependent
        /// work belongs on the concrete control, not here.
        /// </summary>
        protected virtual void OnInitialize() { }
    }
}
