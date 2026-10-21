using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// An unhandled handler exception is logged and then propagates like an ordinary WinForms event
    /// handler's (to Application.ThreadException): out of Dispatch for a synchronous failure, posted
    /// to the dispatching SynchronizationContext for a failure after an await (like async void), and
    /// as a faulted task from DispatchAsync. Cancellation stays silent; a middleware that catches the
    /// exception has handled it (ctx.Exception stays a record either way).
    /// </summary>
    public class ActionExceptionPropagationTests
    {
        private static readonly ViewAction Save = ViewAction.Create("Propagation.Save");

        /// <summary>Queues posted callbacks; the test runs them as a message loop would.</summary>
        private sealed class QueueContext : SynchronizationContext
        {
            private readonly Queue<(SendOrPostCallback, object)> _queue = new Queue<(SendOrPostCallback, object)>();
            public override void Post(SendOrPostCallback d, object state) => _queue.Enqueue((d, state));

            /// <summary>Runs queued callbacks; returns what they threw.</summary>
            public List<Exception> Pump()
            {
                var thrown = new List<Exception>();
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    while (_queue.Count > 0)
                    {
                        var (d, state) = _queue.Dequeue();
                        try { d(state); }
                        catch (Exception ex) { thrown.Add(ex); }
                    }
                }
                finally { SetSynchronizationContext(previous); }
                return thrown;
            }
        }

        private static (ViewActionDispatcher dispatcher, CapturingLogger log) NewDispatcher()
        {
            var log = new CapturingLogger();
            return (new ViewActionDispatcher { Logger = log }, log);
        }

        // ---- synchronous handlers ----

        [Fact]
        public void Dispatch_SyncHandlerThrows_PropagatesAfterLoggingAndCleanup()
        {
            var (dispatcher, log) = NewDispatcher();
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));
            bool executed = false, refreshed = false;
            dispatcher.ActionExecuted += (s, e) => executed = true;
            dispatcher.CanExecuteChanged += (s, e) => refreshed = true;

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save));

            Assert.Equal("boom", ex.Message);
            Assert.Contains(nameof(Dispatch_SyncHandlerThrows_PropagatesAfterLoggingAndCleanup), ex.StackTrace); // original stack kept
            Assert.Equal(LogLevel.Error, log.Entries.Single().Level);
            Assert.False(executed);
            Assert.True(refreshed);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task DispatchAsync_SyncHandlerThrows_FaultsTheTask()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));

            var task = dispatcher.DispatchAsync(Save);

            Assert.True(task.IsFaulted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        }

        // ---- asynchronous handlers ----

        [Fact]
        public async Task DispatchAsync_FailureAfterAwait_FaultsTheTaskAndReEnables()
        {
            var (dispatcher, log) = NewDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, async () => { await gate.Task; throw new InvalidOperationException("late"); });

            var task = dispatcher.DispatchAsync(Save);
            Assert.False(dispatcher.CanDispatch(Save));
            gate.SetResult(true);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Equal("late", ex.Message);
            Assert.True(dispatcher.CanDispatch(Save));
            Assert.Single(log.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public void Dispatch_FailureAfterAwait_IsPostedToTheDispatchingContext()
        {
            var (dispatcher, _) = NewDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, async () => { await gate.Task; throw new InvalidOperationException("late"); });
            var context = new QueueContext();

            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try { dispatcher.Dispatch(Save); }   // returns at the await: nothing thrown yet
            finally { SynchronizationContext.SetSynchronizationContext(previous); }

            gate.SetResult(true);
            var thrown = context.Pump();

            var ex = Assert.IsType<InvalidOperationException>(Assert.Single(thrown));
            Assert.Equal("late", ex.Message);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public void Dispatch_AlreadyFaultedTask_PropagatesSynchronously()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Register(Save, () => Task.FromException(new InvalidOperationException("faulted")));

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save));
            Assert.Equal("faulted", ex.Message);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task Cancellation_StaysSilent()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Register(Save, () => Task.FromCanceled(new CancellationToken(true)));

            await dispatcher.DispatchAsync(Save);   // does not throw
            dispatcher.Dispatch(Save);              // does not throw
        }

        // ---- middleware ----

        [Fact]
        public void Middleware_SeesTheExceptionInFinally_BeforeItPropagates()
        {
            var (dispatcher, _) = NewDispatcher();
            Exception seen = null;
            dispatcher.Use((ctx, next) => { try { next(ctx); } finally { seen = ctx.Exception; } });
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save));
            Assert.IsType<InvalidOperationException>(seen);
        }

        [Fact]
        public void Middleware_ThatClearsTheException_HasHandledIt()
        {
            var (dispatcher, _) = NewDispatcher();
            bool executed = false, refreshed = false;
            dispatcher.ActionExecuted += (s, e) => executed = true;
            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch (InvalidOperationException) { ctx.Exception = null; }   // e.g. showed its own message
            });
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));
            dispatcher.CanExecuteChanged += (s, e) => refreshed = true;

            dispatcher.Dispatch(Save);   // nothing propagates

            Assert.False(executed);      // handled is not succeeded
            Assert.True(refreshed);      // a failed handler may have changed state
        }

        [Fact]
        public void Middleware_ThatCatchesAndKeepsTheRecord_HasHandledIt()
        {
            var (dispatcher, _) = NewDispatcher();
            Exception audited = null;
            dispatcher.Use((ctx, next) => { try { next(ctx); } finally { audited = ctx.Exception; } });   // outer: audit
            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch (InvalidOperationException) { /* showed an error dialog; ctx.Exception stays set */ }
            });
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));

            dispatcher.Dispatch(Save);   // nothing propagates

            Assert.IsType<InvalidOperationException>(audited);   // still visible to auditing
        }

        [Fact]
        public void Middleware_ThatRethrowsAnotherException_PropagatesThatOne()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Use((ctx, next) =>
            {
                try { next(ctx); }
                catch (InvalidOperationException ex) { throw new ApplicationException("wrapped", ex); }
            });
            dispatcher.Register(Save, () => throw new InvalidOperationException("boom"));

            var ex = Assert.Throws<ApplicationException>(() => dispatcher.Dispatch(Save));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task DispatchAsync_ThroughMiddleware_FailureAfterAwait_FaultsTheTask()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Use((ctx, next) => next(ctx));
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, async () => { await gate.Task; throw new InvalidOperationException("late"); });

            var task = dispatcher.DispatchAsync(Save);
            gate.SetResult(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        // ---- edge cases ----

        [Fact]
        public async Task MiddlewareThrowsAfterAnAlreadyFaultedHandler_ItsExceptionPropagates_OnBothApis()
        {
            var (dispatcher, _) = NewDispatcher();
            dispatcher.Use((ctx, next) => { next(ctx); throw new ApplicationException("middleware"); });
            dispatcher.Register(Save, () => Task.FromException(new InvalidOperationException("handler")));

            Assert.Throws<ApplicationException>(() => dispatcher.Dispatch(Save));
            await Assert.ThrowsAsync<ApplicationException>(() => dispatcher.DispatchAsync(Save));
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task DispatchAsync_HandlerFaultedLaterWithOperationCanceled_Faults()
        {
            // An OperationCanceledException that is not the owner's disposal is a failure, not a cancellation.
            var (dispatcher, _) = NewDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);

            var task = dispatcher.DispatchAsync(Save);
            handler.SetException(new OperationCanceledException("timeout"));

            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsFaulted);
        }

        [Fact]
        public void Dispatch_HandlerFaultedLaterWithOperationCanceled_IsPosted()
        {
            var (dispatcher, _) = NewDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);
            var context = new QueueContext();

            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context);
            try { dispatcher.Dispatch(Save); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }

            handler.SetException(new OperationCanceledException("timeout"));
            Assert.IsType<OperationCanceledException>(Assert.Single(context.Pump()));
        }

        [Fact]
        public async Task SubscriberThrowsOperationCanceledAfterAnAsyncHandler_DispatchAsyncStillCompletes()
        {
            var (dispatcher, _) = NewDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);
            dispatcher.ActionExecuted += (s, e) => throw new OperationCanceledException("subscriber");

            var task = dispatcher.DispatchAsync(Save);
            handler.SetResult(true);

            var finished = await Task.WhenAny(task, Task.Delay(5000));
            Assert.Same(task, finished);                              // never left pending
            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public async Task SubscriberThrows_AfterAHandledMiddlewareFailure_StillFaultsDispatchAsync()
        {
            var (dispatcher, _) = NewDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);
            dispatcher.Use((ctx, next) => { next(ctx); ctx.Exception = new ApplicationException("handled by middleware"); });
            bool handlerDone = false;
            dispatcher.CanExecuteChanged += (s, e) => { if (handlerDone) throw new InvalidOperationException("subscriber"); };

            var task = dispatcher.DispatchAsync(Save);
            handlerDone = true;
            handler.SetResult(true);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Equal("subscriber", ex.Message);
        }

        [Fact]
        public async Task MiddlewareThrowsOperationCanceledAfterStartingAnAsyncHandler_DispatchAsyncFaults()
        {
            var (dispatcher, _) = NewDispatcher();
            var handler = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)handler.Task);
            dispatcher.Use((ctx, next) => { next(ctx); throw new OperationCanceledException("middleware"); });

            var task = dispatcher.DispatchAsync(Save);
            handler.SetResult(true);

            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsFaulted);
        }

        // ---- unchanged: CanExecute failures stay diagnostic ----

        [Fact]
        public void CanExecuteThrows_StaysLogOnly()
        {
            var (dispatcher, log) = NewDispatcher();
            dispatcher.Register(Save, () => { }, canExecute: () => throw new InvalidOperationException("predicate"));

            dispatcher.Dispatch(Save);   // does not throw

            Assert.Contains(log.Entries, e => e.Level == LogLevel.Error);
        }
    }
}
