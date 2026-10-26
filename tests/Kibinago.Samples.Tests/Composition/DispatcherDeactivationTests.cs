using System;
using System.Threading.Tasks;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    public class DispatcherDeactivationTests
    {
        private static readonly ViewAction Run = ViewAction.Create("Deactivation.Run");
        private static readonly ViewAction Unknown = ViewAction.Create("Deactivation.Unknown");

        [Fact]
        public void Deactivated_CanDispatchFalse_DispatchDoesNothing()
        {
            var dispatcher = new ViewActionDispatcher();
            int runs = 0;
            dispatcher.Register(Run, () => runs++);

            dispatcher.Deactivate();

            Assert.False(dispatcher.IsActive);
            Assert.False(dispatcher.CanDispatch(Run));
            dispatcher.Dispatch(Run);
            Assert.Equal(0, runs);
        }

        [Fact]
        public void Deactivated_StrictMode_UnknownKeyOrBadPayloadDoesNotThrow()
        {
            var dispatcher = new ViewActionDispatcher { ValidationMode = DispatchValidationMode.Strict };
            dispatcher.Register(Run, () => { });
            dispatcher.Deactivate();

            dispatcher.Dispatch(Unknown);
            dispatcher.Dispatch(Run, "unexpected payload");
        }

        [Fact]
        public async Task Deactivated_DispatchAsyncCompletes_WithoutRunningHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            int runs = 0;
            dispatcher.Register(Run, async () => { await Task.Yield(); runs++; });
            dispatcher.Deactivate();

            await dispatcher.DispatchAsync(Run);

            Assert.Equal(0, runs);
        }

        [Fact]
        public async Task Deactivated_InFlightAsyncCompletion_RaisesNoCanExecuteChanged()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Run, () => gate.Task);
            var running = dispatcher.DispatchAsync(Run);
            int raised = 0;
            dispatcher.CanExecuteChanged += (s, e) => raised++;

            dispatcher.Deactivate();
            dispatcher.RaiseCanExecuteChanged();
            gate.SetResult(true);
            await running;

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Deactivated_MiddlewareNotInvoked()
        {
            var dispatcher = new ViewActionDispatcher();
            int middleware = 0;
            dispatcher.Use(async (ctx, next) => { middleware++; await next(ctx); });
            dispatcher.Register(Run, () => { });
            dispatcher.Deactivate();

            dispatcher.Dispatch(Run);

            Assert.Equal(0, middleware);
        }

        [Fact]
        public void Deactivate_IsIdempotent()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Deactivate();
            dispatcher.Deactivate();
            Assert.False(dispatcher.IsActive);
        }

        [Fact]
        public async Task Deactivated_InFlightAsyncCompletion_RaisesNoActionExecuted()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Run, () => gate.Task);
            var running = dispatcher.DispatchAsync(Run);
            int executed = 0;
            dispatcher.ActionExecuted += (s, e) => executed++;

            dispatcher.Deactivate();
            gate.SetResult(true);
            await running;

            Assert.Equal(0, executed);
        }
    }
}
