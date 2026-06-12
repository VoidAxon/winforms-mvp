using System;
using WinformsMVP.Common.Events;
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

        public event EventHandler<ActionRequestEventArgs> ActionTriggered
        {
            add { }
            remove { }
        }

        public void Bind(ViewActionDispatcher dispatcher) => Dispatcher = dispatcher;

        public void Bind() { }

        public void Unbind() => Dispatcher = null;

        /// <summary>Dispatches through the captured dispatcher — the same path a bound control uses.</summary>
        public void Dispatch(ViewAction action, object payload = null) => Captured().Dispatch(action, payload);

        /// <summary>Evaluates the action's CanExecute predicate through the captured dispatcher.</summary>
        public bool CanDispatch(ViewAction action) => Captured().CanDispatch(action);

        private ViewActionDispatcher Captured()
        {
            if (Dispatcher == null)
                throw new InvalidOperationException(
                    "No dispatcher captured. Attach the view and call presenter.Initialize() before dispatching.");
            return Dispatcher;
        }
    }
}
