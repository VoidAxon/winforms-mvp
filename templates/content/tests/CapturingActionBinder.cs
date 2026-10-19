using System;
using System.Threading.Tasks;
using WinformsMVP.MVP.ViewActions;

namespace MvpApp.Tests
{
    /// <summary>
    /// The ActionBinder for mock views. The framework hands the presenter's dispatcher to the
    /// view's binder during initialization; this binder keeps it, so a test dispatches actions
    /// through the same channel a real button uses (canExecute, payload checks, async handling).
    /// </summary>
    public sealed class CapturingActionBinder : IViewActionBinder
    {
        /// <summary>The captured dispatcher, or null before the presenter was started.</summary>
        public ViewActionDispatcher Dispatcher { get; private set; }

        public void Bind(ViewActionDispatcher dispatcher) => Dispatcher = dispatcher;

        /// <summary>Dispatches like a click on a bound control.</summary>
        public void Dispatch(ViewAction action, object payload = null) => Captured().Dispatch(action, payload);

        /// <summary>Dispatches and waits until an asynchronous handler has finished.</summary>
        public Task DispatchAsync(ViewAction action, object payload = null) => Captured().DispatchAsync(action, payload);

        /// <summary>Whether a bound control would be enabled.</summary>
        public bool CanDispatch(ViewAction action) => Captured().CanDispatch(action);

        private ViewActionDispatcher Captured()
            => Dispatcher ?? throw new InvalidOperationException(
                "No dispatcher captured. Start the presenter with PresenterTestHost.Start before dispatching.");
    }
}
