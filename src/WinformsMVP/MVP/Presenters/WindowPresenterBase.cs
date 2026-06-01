using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for presenters that manage Form views (dialogs, windows).
    /// Use this class when the presenter does not require initialization parameters.
    /// This presenter is designed to work with WindowNavigator.
    /// </summary>
    /// <typeparam name="TView">The view interface type, must implement IWindowView</typeparam>
    public abstract class WindowPresenterBase<TView> :
        PresenterBase<TView>,
        IViewAttacher<TView>,
        IInitializable
        where TView : IWindowView
    {
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
        /// Initializes the presenter. Called by WindowNavigator after the view is attached.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                throw new InvalidOperationException("Presenter has already been initialized");

            _initialized = true;
            RegisterViewActions();

            // Automatically bind ViewActionBinder to dispatcher after actions are registered
            View.ActionBinder?.Bind(_dispatcher);

            OnInitialize();
        }

        /// <summary>
        /// Override to register view actions (UI event bindings).
        /// </summary>
        protected virtual void RegisterViewActions() { }

        /// <summary>
        /// Override to perform initialization logic after the view is attached.
        /// At this point, the View property is guaranteed to be set.
        /// </summary>
        protected virtual void OnInitialize() { }
    }
}
