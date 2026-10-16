using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for presenters that manage Form views with initialization parameters.
    /// Use this class when the presenter requires parameters to initialize (e.g., entity ID, edit mode).
    /// This presenter is designed to work with WindowNavigator.
    /// </summary>
    /// <typeparam name="TView">The view interface type, must implement IWindowView</typeparam>
    /// <typeparam name="TParam">The type of initialization parameters</typeparam>
    public abstract class WindowPresenterBase<TView, TParam> :
        WindowPresenterBaseCore<TView>,
        IViewAttacher<TView>,
        IInitializable<TParam>
        where TView : IWindowView
    {
        /// <summary>
        /// Gets the initialization parameters passed to this presenter.
        /// Available after Initialize() is called.
        /// </summary>
        protected TParam Parameters { get; private set; }

        protected WindowPresenterBase()
        {
        }

        /// <summary>
        /// Attaches the view to this presenter. Called by WindowNavigator.
        /// </summary>
        public void AttachView(TView view)
        {
            SetView(view);
        }

        /// <summary>
        /// Initializes the presenter with parameters. Called by WindowNavigator after the view is attached.
        /// </summary>
        /// <param name="parameters">Initialization parameters</param>
        public void Initialize(TParam parameters)
        {
            if (_initialized)
                throw new InvalidOperationException("Presenter has already been initialized");

            _initialized = true;
            Parameters = parameters;

            RegisterViewActions();

            // Automatically bind ViewActionBinder to dispatcher after actions are registered
            View.ActionBinder?.Bind(_dispatcher);

            OnInitialize(parameters);
        }

        /// <summary>
        /// Override to register view actions (UI event bindings).
        /// </summary>
        protected virtual void RegisterViewActions() { }

        /// <summary>
        /// Override to perform initialization logic with parameters.
        /// At this point, both the View property and Parameters are guaranteed to be set.
        /// </summary>
        /// <param name="parameters">The initialization parameters</param>
        protected abstract void OnInitialize(TParam parameters);
    }
}
