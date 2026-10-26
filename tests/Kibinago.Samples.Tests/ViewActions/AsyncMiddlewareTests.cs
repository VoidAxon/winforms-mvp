using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Logging;
using Kibinago.Samples.Tests.TestHelpers;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// Middleware returns a Task: <c>await next(ctx)</c> completes when the handler has finished, so a
    /// middleware wraps the whole asynchronous operation — its catch sees a failure after the
    /// handler's first await, its finally runs after the handler ended.
    /// </summary>
    public class AsyncMiddlewareTests
    {
        private static readonly ViewAction Save = ViewAction.Create("AsyncMiddleware.Save");

        [Fact]
        public async Task CodeAfterAwaitNext_RunsAfterTheAsyncHandlerFinished()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            var log = new List<string>();
            dispatcher.Register(Save, async () => { log.Add("handler start"); await gate.Task; log.Add("handler end"); });
            dispatcher.Use(async (ctx, next) => { log.Add("before"); await next(ctx); log.Add("after"); });

            var dispatch = dispatcher.DispatchAsync(Save);
            Assert.Equal(new[] { "before", "handler start" }, log);

            gate.SetResult(true);
            await dispatch;

            Assert.Equal(new[] { "before", "handler start", "handler end", "after" }, log);
        }

        [Fact]
        public async Task MiddlewareCatch_SeesAFailureAfterTheHandlersAwait_AndHandlesIt()
        {
            var (dispatcher, logger) = NewDispatcher();
            int executed = 0;
            Exception caught = null;
            dispatcher.Register(Save, async () => { await Task.Yield(); throw new InvalidOperationException("db down"); });
            dispatcher.Use(async (ctx, next) =>
            {
                try { await next(ctx); }
                catch (InvalidOperationException ex) { caught = ex; }   // e.g. an error dialog
            });
            dispatcher.ActionExecuted += (s, e) => executed++;

            await dispatcher.DispatchAsync(Save);   // handled: does not fault

            Assert.Equal("db down", caught?.Message);
            Assert.Equal(0, executed);
            Assert.True(dispatcher.CanDispatch(Save));
            Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task Uncaught_FailureAfterAwait_Propagates_AndFinallySeesCtxException()
        {
            var dispatcher = new ViewActionDispatcher();
            Exception seenInFinally = null;
            dispatcher.Register(Save, async () => { await Task.Yield(); throw new InvalidOperationException("db down"); });
            dispatcher.Use(async (ctx, next) =>
            {
                try { await next(ctx); }
                finally { seenInFinally = ctx.Exception; }   // e.g. an audit entry "failed"
            });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(Save));

            Assert.Equal("db down", ex.Message);
            Assert.Same(ex, seenInFinally);
        }

        [Fact]
        public async Task MiddlewareAwaitingBeforeNext_KeepsTheActionDisabled_UntilThePipelineEnds()
        {
            var dispatcher = new ViewActionDispatcher();
            var permission = new TaskCompletionSource<bool>();
            int runs = 0, executed = 0;
            dispatcher.Register(Save, () => runs++);   // a synchronous handler
            dispatcher.Use(async (ctx, next) => { if (await permission.Task) await next(ctx); });
            dispatcher.ActionExecuted += (s, e) => executed++;

            var dispatch = dispatcher.DispatchAsync(Save);
            Assert.False(dispatcher.CanDispatch(Save));
            dispatcher.Dispatch(Save);                     // repeat while the check runs: ignored

            permission.SetResult(true);
            await dispatch;

            Assert.Equal(1, runs);
            Assert.Equal(1, executed);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task MiddlewareThatDoesNotAwaitNext_TheHandlerIsStillTracked_AndItsFailurePropagates()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, async () => { await gate.Task; throw new InvalidOperationException("late"); });
            dispatcher.Use((ctx, next) => { next(ctx); return Task.CompletedTask; });   // forgot to await

            var dispatch = dispatcher.DispatchAsync(Save);
            Assert.False(dispatch.IsCompleted);
            Assert.False(dispatcher.CanDispatch(Save));

            gate.SetResult(true);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatch);
            Assert.Equal("late", ex.Message);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task MiddlewareThatDoesNotAwaitNext_AnAlreadyFaultedHandlerTask_StillPropagates()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => Task.FromException(new InvalidOperationException("at once")));
            dispatcher.Use((ctx, next) => { next(ctx); return Task.CompletedTask; });   // forgot to await

            Assert.Equal("at once", Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save)).Message);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(Save));
            Assert.Equal("at once", ex.Message);
        }

        [Fact]
        public async Task AsyncMiddleware_HandlerFaultedWithOperationCanceled_StillFaults_LikeWithoutMiddleware()
        {
            var dispatcher = new ViewActionDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);
            dispatcher.Use(async (ctx, next) => await next(ctx));

            var task = dispatcher.DispatchAsync(Save);
            handler.SetException(new OperationCanceledException("timeout, not a disposal"));

            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsFaulted);
        }

        [Fact]
        public async Task OwnerDisposedWhileAMiddlewareAwaitsBeforeNext_TheHandlerDoesNotRun()
        {
            var dispatcher = new ViewActionDispatcher();
            var permission = new TaskCompletionSource<bool>();
            int runs = 0, executed = 0;
            dispatcher.Register(Save, () => runs++);   // synchronous: would touch a disposed view
            dispatcher.Use(async (ctx, next) => { await permission.Task; await next(ctx); });
            dispatcher.ActionExecuted += (s, e) => executed++;

            var dispatch = dispatcher.DispatchAsync(Save);
            dispatcher.Deactivate();                   // the window closed meanwhile
            permission.SetResult(true);
            await dispatch;

            Assert.Equal(0, runs);
            Assert.Equal(0, executed);
        }

        [Fact]
        public void PassThroughMiddleware_SyncHandlerFailure_IsRethrownFromDispatch()
        {
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(Save, () => throw new InvalidOperationException("sync"));
            dispatcher.Use((ctx, next) => next(ctx));

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save));
            Assert.Equal("sync", ex.Message);
        }

        [Fact]
        public async Task ShortCircuit_ReturnsWithoutRunningTheHandler()
        {
            var dispatcher = new ViewActionDispatcher();
            int runs = 0, executed = 0;
            dispatcher.Register(Save, () => runs++);
            dispatcher.Use((ctx, next) => Task.CompletedTask);
            dispatcher.ActionExecuted += (s, e) => executed++;

            await dispatcher.DispatchAsync(Save);

            Assert.Equal(0, runs);
            Assert.Equal(0, executed);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        private static (ViewActionDispatcher, CapturingLogger) NewDispatcher()
        {
            var logger = new CapturingLogger();
            return (new ViewActionDispatcher { Logger = logger }, logger);
        }
    }
}
