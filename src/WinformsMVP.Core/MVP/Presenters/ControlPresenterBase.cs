using System;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for presenters that manage Control / UserControl views.
    /// Use this class when the presenter does not require initialization parameters.
    /// </summary>
    /// <remarks>
    /// Construction is two-phase, identical in shape to <see cref="WindowPresenterBase{TView}"/>:
    /// the constructor takes only the presenter's own dependencies, then the view is supplied via
    /// <see cref="AttachView"/> and initialization runs via <see cref="Initialize"/>. The
    /// <c>Connect</c> extension (see <see cref="ControlPresenterConnectExtensions"/>) drives that
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
        protected ControlPresenterBase()
        {
        }

        /// <summary>
        /// Attaches the view to this presenter. Called by the <c>Connect</c> extension.
        /// </summary>
        public void AttachView(TView view)
        {
            SetView(view);
        }

        /// <summary>
        /// Initializes the presenter. Called after the view is attached.
        /// At this point the View property and all derived fields are guaranteed to be set.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("Presenter has already been initialized");

            _initialized = true;
            RegisterViewActions();

            // Views that don't participate in the ViewAction system simply don't implement IActionableView.
            if (View is IActionableView actionable)
                actionable.ActionBinder?.Bind(_dispatcher);

            OnInitialize();
        }

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
