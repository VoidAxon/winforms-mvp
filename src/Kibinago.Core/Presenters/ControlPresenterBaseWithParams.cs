using System;
using Kibinago.Views;

namespace Kibinago.Presenters
{
    /// <summary>
    /// Base class for presenters that manage Control / UserControl views with initialization parameters.
    /// Use this class when the presenter requires parameters to initialize (e.g., entity ID, filter criteria).
    /// </summary>
    /// <remarks>
    /// Two-phase construction, identical in shape to <see cref="WindowPresenterBase{TView, TParam}"/>:
    /// the constructor takes only the presenter's own dependencies; the view and parameters are supplied
    /// via <c>AttachView</c> + <c>Initialize(TParam)</c>, driven by the parameterized
    /// <c>Connect</c> overload. No <c>System.Windows.Forms</c> dependency, and no constructor-ordering trap.
    /// </remarks>
    /// <typeparam name="TView">The view interface type, must implement IViewBase</typeparam>
    /// <typeparam name="TParam">The type of initialization parameters</typeparam>
    public abstract class ControlPresenterBase<TView, TParam> :
        PresenterBase<TView>,
        IViewAttacher<TView>,
        IInitializable<TParam>
        where TView : IViewBase
    {
        /// <summary>
        /// Gets the initialization parameters passed to this presenter.
        /// Available once the presenter has been initialized.
        /// </summary>
        protected TParam Parameters { get; private set; }

        /// <summary>
        /// Creates the presenter. Derived constructors take only the presenter's own dependencies; the
        /// view and <see cref="Parameters"/> are supplied later, by <c>Connect</c> or <c>AttachChild</c>.
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
        /// Initializes the presenter with parameters. Called after the view is attached.
        /// At this point the View property, Parameters, and all derived fields are guaranteed to be set.
        /// </summary>
        /// <param name="parameters">Initialization parameters</param>
        void IInitializable<TParam>.Initialize(TParam parameters)
            => InitializeCore(() => { Parameters = parameters; RegisterViewActions(); },
                              () => OnInitialize(parameters));

        /// <summary>
        /// Override to register view actions (UI event bindings).
        /// </summary>
        protected virtual void RegisterViewActions() { }

        /// <summary>
        /// Override to perform initialization logic with parameters.
        /// At this point both the View property and Parameters are guaranteed to be set.
        /// Note: the control may not have a parent container or window handle yet.
        /// </summary>
        /// <param name="parameters">The initialization parameters</param>
        protected abstract void OnInitialize(TParam parameters);
    }
}
