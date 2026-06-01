using System;
using System.Windows.Forms;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    /// <summary>
    /// Base class for presenters that manage UserControl views.
    /// Use this class when the presenter does not require initialization parameters.
    /// The view is injected through the constructor and the presenter is automatically
    /// initialized and disposed with the control's lifecycle.
    /// </summary>
    /// <typeparam name="TView">The view interface type, must implement IViewBase</typeparam>
    public abstract class ControlPresenterBase<TView> : PresenterBase<TView>
        where TView : IViewBase
    {
        /// <summary>
        /// Creates a new presenter for a UserControl view.
        /// </summary>
        /// <param name="view">The view instance (typically a UserControl)</param>
        protected ControlPresenterBase(TView view)
        {
            SetView(view);
            Initialize();

            // Subscribe to control lifecycle events
            if (view is Control control)
            {
                control.HandleCreated += OnControlHandleCreated;
                control.Disposed += OnControlDisposed;
            }
            else if (view is UserControl userControl)
            {
                userControl.Load += OnControlLoad;
                userControl.Disposed += OnControlDisposed;
            }
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            RegisterViewActions();

            // Automatically bind ViewActionBinder to dispatcher after actions are registered.
            // Views that don't participate in the ViewAction system simply don't implement IActionableView.
            if (View is IActionableView actionable)
            {
                actionable.ActionBinder?.Bind(_dispatcher);
            }

            OnInitialize();
        }

        /// <summary>
        /// Override to register view actions (UI event bindings).
        /// </summary>
        protected virtual void RegisterViewActions() { }

        /// <summary>
        /// Override to perform initialization logic after the view is attached.
        /// At this point, the View property is guaranteed to be set.
        /// Note: The control may not have a parent container yet.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// Called when the UserControl is loaded (has parent and handle created).
        /// Override this to perform operations that require the control to be fully initialized.
        /// Only called if the view is a UserControl.
        /// </summary>
        protected virtual void OnControlLoad(object sender, EventArgs e)
        {
            // Default implementation: empty
        }

        /// <summary>
        /// Called when the Control's window handle is created.
        /// Override this to perform operations that require a window handle.
        /// Called for all Control types.
        /// </summary>
        protected virtual void OnControlHandleCreated(object sender, EventArgs e)
        {
            // Default implementation: empty
        }

        /// <summary>
        /// Called when the UserControl is being disposed.
        /// Automatically disposes this presenter as well.
        /// </summary>
        protected virtual void OnControlDisposed(object sender, EventArgs e)
        {
            if (sender is UserControl userControl)
            {
                userControl.Load -= OnControlLoad;
                userControl.Disposed -= OnControlDisposed;
            }
            else if (sender is Control control)
            {
                control.HandleCreated -= OnControlHandleCreated;
                control.Disposed -= OnControlDisposed;
            }

            Dispose();
        }
    }
}
