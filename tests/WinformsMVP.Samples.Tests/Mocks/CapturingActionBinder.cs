using System;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Test-double <see cref="IViewActionBinder"/> that captures the dispatcher the framework
    /// hands to the view, giving tests the same action entry point a real Form's binder uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework auto-calls <c>View.ActionBinder?.Bind(dispatcher)</c> right after
    /// <c>RegisterViewActions</c>. A mock view exposes an instance of this class, and the test
    /// drives the presenter through its real entry point:
    /// </para>
    /// <code>
    /// _view.Binder.Dispatch(CommonActions.Save);   // CanExecute and middleware run as in production
    /// </code>
    /// <para>
    /// This keeps the presenter's surface untouched — no public or internal members are added
    /// for testing. Like a null binder, it also avoids constructing a real
    /// <see cref="ViewActionBinder"/>, whose constructor registers WinForms control strategies.
    /// </para>
    /// </remarks>
    public sealed class CapturingActionBinder : IViewActionBinder
    {
        /// <summary>
        /// The dispatcher captured from the framework's auto-bind,
        /// or null before <c>presenter.Initialize()</c> has run.
        /// </summary>
        public ViewActionDispatcher Dispatcher { get; private set; }

        public void Bind(ViewActionDispatcher dispatcher) => Dispatcher = dispatcher;

        /// <summary>Dispatches through the captured dispatcher — the same path a bound control uses.</summary>
        public void Dispatch(ViewAction action, object payload = null) => Captured().Dispatch(action, payload);

        /// <summary>
        /// Dispatches and returns a task that completes when the handler has finished — await it
        /// for asynchronous handlers.
        /// </summary>
        public System.Threading.Tasks.Task DispatchAsync(ViewAction action, object payload = null)
            => Captured().DispatchAsync(action, payload);

        /// <summary>Evaluates the action's CanExecute predicate through the captured dispatcher.</summary>
        public bool CanDispatch(ViewAction action) => Captured().CanDispatch(action);

        /// <summary>Dispatches a typed action; the payload type is checked at compile time.</summary>
        public void Dispatch<T>(ViewAction<T> action, T payload) => Captured().Dispatch(action, payload);

        /// <summary>Typed <see cref="DispatchAsync(ViewAction, object)"/>.</summary>
        public System.Threading.Tasks.Task DispatchAsync<T>(ViewAction<T> action, T payload)
            => Captured().DispatchAsync(action, payload);

        /// <summary>Typed <see cref="CanDispatch(ViewAction)"/>.</summary>
        public bool CanDispatch<T>(ViewAction<T> action) => Captured().CanDispatch(action);

        private ViewActionDispatcher Captured()
        {
            if (Dispatcher == null)
                throw new InvalidOperationException(
                    "No dispatcher captured. Attach the view and call presenter.Initialize() before dispatching.");
            return Dispatcher;
        }
    }
}
