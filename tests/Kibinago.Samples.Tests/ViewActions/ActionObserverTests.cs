using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Logging;
using Kibinago.Samples.Tests.TestHelpers;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// The optional IActionObserver hears about every handler that ran, once, when it has finished:
    /// what ran, with which payload, for which presenter, when and how long, and how it ended. It never
    /// changes what the dispatch does.
    /// </summary>
    public class ActionObserverTests
    {
        private static readonly ViewAction Save = ViewAction.Create("Observer.Save");
        private static readonly ViewAction<int> Open = ViewAction.Create<int>("Observer.Open");

        private static (ViewActionDispatcher dispatcher, RecordingActionObserver observer, CapturingLogger log) NewDispatcher()
        {
            var observer = new RecordingActionObserver();
            var log = new CapturingLogger();
            return (new ViewActionDispatcher { Observer = observer, Logger = log }, observer, log);
        }

        private sealed class ThrowingObserver : IActionObserver
        {
            public int Calls;
            public void OnExecuted(ActionExecution execution)
            {
                Calls++;
                throw new InvalidOperationException("observer");
            }
        }

        // ---- what is reported ----

        [Fact]
        public void SyncHandler_IsReportedOnce_WithItsDetails()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            dispatcher.OwnerType = typeof(ActionObserverTests);
            dispatcher.Register(Open, (int id) => Thread.Sleep(20));
            var before = DateTimeOffset.UtcNow;

            dispatcher.Dispatch(Open, 42);

            var execution = Assert.Single(observer.Executions);
            Assert.Equal(Open.Key, execution.Action);
            Assert.Equal(42, execution.Payload);
            Assert.Equal(typeof(ActionObserverTests), execution.PresenterType);
            Assert.InRange(execution.StartedAt, before.AddSeconds(-1), DateTimeOffset.UtcNow);
            Assert.True(execution.Elapsed >= TimeSpan.FromMilliseconds(15), execution.Elapsed.ToString());
            Assert.False(execution.IsAsync);
            Assert.Equal(ActionOutcome.Succeeded, execution.Outcome);
            Assert.Null(execution.Exception);
        }

        [Fact]
        public async Task AsyncHandler_IsReportedWhenItFinishes_NotWhenItStarts()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)gate.Task);

            var running = dispatcher.DispatchAsync(Save);
            Assert.Empty(observer.Executions);
            gate.SetResult(true);
            await running;

            var execution = Assert.Single(observer.Executions);
            Assert.True(execution.IsAsync);
            Assert.Equal(ActionOutcome.Succeeded, execution.Outcome);
            Assert.True(dispatcher.CanDispatch(Save));   // re-enabled before the report
        }

        [Fact]
        public void EachRun_IsReportedSeparately()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            dispatcher.Register(Open, (int id) => { });

            dispatcher.Dispatch(Open, 1);
            dispatcher.Dispatch(Open, 2);

            Assert.Equal(new object[] { 1, 2 }, observer.Executions.Select(e => e.Payload).ToArray());
        }

        // ---- outcomes ----

        [Fact]
        public async Task AsyncFailure_IsFailed_WithTheException()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, async () => { await gate.Task; throw new InvalidOperationException("late"); });

            var running = dispatcher.DispatchAsync(Save);
            gate.SetResult(true);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => running);

            var execution = Assert.Single(observer.Executions);
            Assert.Equal(ActionOutcome.Failed, execution.Outcome);
            Assert.Same(ex, execution.Exception);
        }

        [Fact]
        public async Task CanceledTask_IsCanceled()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            dispatcher.Register(Save, () => Task.FromCanceled(new CancellationToken(true)));

            await dispatcher.DispatchAsync(Save);

            var execution = Assert.Single(observer.Executions);
            Assert.Equal(ActionOutcome.Canceled, execution.Outcome);
            Assert.Null(execution.Exception);
        }

        [Fact]
        public async Task CancelledByDisposal_IsCanceled_WithTheCancellation()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            using (var lifetime = new CancellationTokenSource())
            {
                dispatcher.Lifetime = lifetime.Token;
                var gate = new TaskCompletionSource<bool>();
                dispatcher.Register(Save, async ct => { await gate.Task; ct.ThrowIfCancellationRequested(); });

                var running = dispatcher.DispatchAsync(Save);
                lifetime.Cancel();
                gate.SetResult(true);
                await running;   // silent
            }

            var execution = Assert.Single(observer.Executions);
            Assert.Equal(ActionOutcome.Canceled, execution.Outcome);
            Assert.IsAssignableFrom<OperationCanceledException>(execution.Exception);
        }

        [Fact]
        public void OperationCanceled_NotFromDisposal_IsFailed()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            dispatcher.Register(Save, () => throw new OperationCanceledException("timeout"));

            Assert.Throws<OperationCanceledException>(() => dispatcher.Dispatch(Save));

            Assert.Equal(ActionOutcome.Failed, Assert.Single(observer.Executions).Outcome);
        }

        // ---- not executions ----

        [Fact]
        public async Task RejectedDispatches_AreNotReported()
        {
            var (dispatcher, observer, _) = NewDispatcher();
            bool enabled = false;
            dispatcher.Register(Save, () => { }, canExecute: () => enabled);
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Open, (int id) => (Task)gate.Task);

            dispatcher.Dispatch(Save);                              // disabled
            dispatcher.Dispatch(ViewAction.Create("Observer.None")); // unregistered
            dispatcher.Dispatch(Open.Key, "wrong");                  // payload mismatch
            var running = dispatcher.DispatchAsync(Open, 1);         // starts
            dispatcher.Dispatch(Open, 2);                            // ignored while running

            Assert.Empty(observer.Executions);
            gate.SetResult(true);
            await running;
            Assert.Equal(1, Assert.Single(observer.Executions).Payload);
        }

        // ---- the observer never changes the dispatch ----

        [Fact]
        public void ThrowingObserver_IsLogged_AndTheDispatchIsUnchanged()
        {
            var observer = new ThrowingObserver();
            var log = new CapturingLogger();
            var dispatcher = new ViewActionDispatcher { Observer = observer, Logger = log };
            int ran = 0;
            dispatcher.Register(Save, () => ran++);

            dispatcher.Dispatch(Save);   // does not throw

            Assert.Equal(1, ran);
            Assert.Equal(1, observer.Calls);
            Assert.Contains(log.Entries, e => e.Level == LogLevel.Error);
            Assert.True(dispatcher.CanDispatch(Save));
        }

        [Fact]
        public void ThrowingObserver_KeepsTheHandlersFailure()
        {
            var dispatcher = new ViewActionDispatcher { Observer = new ThrowingObserver(), Logger = new CapturingLogger() };
            dispatcher.Register(Save, () => throw new InvalidOperationException("handler"));

            var ex = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(Save));

            Assert.Equal("handler", ex.Message);
        }

        [Fact]
        public async Task ObserverReplacedWhileRunning_TheOneInPlaceAtTheStartIsTold()
        {
            var (dispatcher, first, _) = NewDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)gate.Task);

            var running = dispatcher.DispatchAsync(Save);
            var second = new RecordingActionObserver();
            dispatcher.Observer = second;
            gate.SetResult(true);
            await running;

            Assert.Single(first.Executions);
            Assert.Empty(second.Executions);
        }
    }
}
