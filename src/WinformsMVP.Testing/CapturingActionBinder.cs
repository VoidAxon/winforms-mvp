using System;
using System.Threading.Tasks;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Testing
{
    /// <summary>
    /// Test-double <see cref="IViewActionBinder"/> that captures the dispatcher the framework
    /// hands to the view, giving tests the same action entry point a real Form's bound controls use.
    /// </summary>
    /// <remarks>
    /// The framework calls <c>View.ActionBinder?.Bind(dispatcher)</c> right after
    /// <c>RegisterViewActions</c>. A mock view exposes an instance of this class, and the test
    /// drives the presenter through its real entry point, so <c>canExecute</c> and middleware run
    /// as in production:
    /// <code>
    /// public sealed class MockOrderView : IOrderView
    /// {
    ///     public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
    ///     public IViewActionBinder ActionBinder => Binder;
    /// }
    ///
    /// view.Binder.Dispatch(CommonActions.Save);
    /// await view.Binder.DispatchAsync(CommonActions.Load);   // async handler: awaits completion
    /// </code>
    /// </remarks>
    public class CapturingActionBinder : IViewActionBinder
    {
        /// <summary>
        /// The dispatcher captured from the framework's auto-bind, or null before the presenter
        /// was started (<see cref="PresenterTestHost"/>).
        /// </summary>
        public ViewActionDispatcher Dispatcher { get; private set; }

        /// <summary>True between the framework's bind and <see cref="Unbind"/> (the presenter's disposal).</summary>
        public bool IsBound => Dispatcher != null;

        private bool _unbound;

        /// <inheritdoc />
        public void Bind(ViewActionDispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            _unbound = false;
        }

        /// <summary>
        /// Called by the framework when the presenter is disposed. Afterwards dispatching does nothing
        /// (like a click on a control whose presenter is gone) and <see cref="CanDispatch(ViewAction)"/>
        /// returns false.
        /// </summary>
        public void Unbind()
        {
            if (Dispatcher == null) return;
            Dispatcher = null;
            _unbound = true;
        }

        /// <summary>Dispatches through the captured dispatcher — the same path a bound control uses.</summary>
        public virtual void Dispatch(ViewAction action, object payload = null) => Captured()?.Dispatch(action, payload);

        /// <summary>
        /// Dispatches and returns a task that completes when the handler has finished (and faults
        /// with its exception) — await it for asynchronous handlers.
        /// </summary>
        public virtual Task DispatchAsync(ViewAction action, object payload = null)
            => Captured()?.DispatchAsync(action, payload) ?? Task.CompletedTask;

        /// <summary>Evaluates the action's <c>canExecute</c> through the captured dispatcher.</summary>
        public virtual bool CanDispatch(ViewAction action) => Captured()?.CanDispatch(action) ?? false;

        /// <summary>Dispatches a typed action; the payload type is checked at compile time.</summary>
        public void Dispatch<T>(ViewAction<T> action, T payload) => Dispatch(action.Key, payload);

        /// <summary>Typed <see cref="DispatchAsync(ViewAction, object)"/>.</summary>
        public Task DispatchAsync<T>(ViewAction<T> action, T payload) => DispatchAsync(action.Key, payload);

        /// <summary>Typed <see cref="CanDispatch(ViewAction)"/>.</summary>
        public bool CanDispatch<T>(ViewAction<T> action) => CanDispatch(action.Key);

        // Null after Unbind (inert); throws before the first bind (a test mistake).
        private ViewActionDispatcher Captured()
        {
            if (Dispatcher == null && !_unbound)
                throw new InvalidOperationException(
                    "No dispatcher captured yet. Start the presenter with PresenterTestHost.Start(...) " +
                    "(it attaches the view and binds its ActionBinder) before dispatching.");
            return Dispatcher;
        }
    }
}
