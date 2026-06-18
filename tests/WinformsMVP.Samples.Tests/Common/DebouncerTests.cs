using System;
using System.Collections.Concurrent;
using System.Threading;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class DebouncerTests
    {
        // Runs posted callbacks inline on the calling (timer) thread so tests are deterministic
        // regardless of the ambient SynchronizationContext, while still exercising the marshaling path.
        private sealed class InlineSyncContext : SynchronizationContext
        {
            public int PostCount;
            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref PostCount);
                d(state);
            }
        }

        // Captures posted callbacks instead of running them, so a test can interleave Dispose
        // between the post and its execution (simulating "Dispose on the UI thread before the
        // queued callback is pumped").
        private sealed class QueuedSyncContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
            private readonly ManualResetEventSlim _posted;
            public QueuedSyncContext(ManualResetEventSlim posted) { _posted = posted; }
            public override void Post(SendOrPostCallback d, object state)
            {
                _queue.Enqueue(() => d(state));
                _posted.Set();
            }
            public bool TryDrainOne()
            {
                Action a;
                if (_queue.TryDequeue(out a)) { a(); return true; }
                return false;
            }
        }

        [Fact]
        public void Signal_FiresOnceAfterLastSignal()
        {
            int count = 0;
            var fired = new ManualResetEventSlim(false);
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(50),
                () => { Interlocked.Increment(ref count); fired.Set(); },
                synchronizationContext: new InlineSyncContext()))
            {
                for (int i = 0; i < 5; i++) d.Signal();   // burst: only the last survives

                Assert.True(fired.Wait(2000), "debounced action did not fire");
                Thread.Sleep(150);                         // allow any erroneous extra fire
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Cancel_PreventsPendingFire()
        {
            int count = 0;
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(100),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                d.Cancel();
                Thread.Sleep(250);
                Assert.Equal(0, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Dispose_PreventsPendingFire()
        {
            int count = 0;
            var d = new Debouncer(
                TimeSpan.FromMilliseconds(100),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext());

            d.Signal();
            d.Dispose();
            d.Dispose();              // idempotent
            Thread.Sleep(250);
            Assert.Equal(0, Volatile.Read(ref count));
        }

        [Fact]
        public void ActionThrows_RoutedToOnError()
        {
            Exception captured = null;
            var done = new ManualResetEventSlim(false);
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(30),
                () => throw new InvalidOperationException("boom"),
                onError: ex => { captured = ex; done.Set(); },
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                Assert.True(done.Wait(2000), "onError was not invoked");
                Assert.IsType<InvalidOperationException>(captured);
            }
        }

        [Fact]
        public void ActionThrows_NoHandler_IsSwallowed()
        {
            var fired = new ManualResetEventSlim(false);
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(30),
                () => { fired.Set(); throw new InvalidOperationException("boom"); },
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                Assert.True(fired.Wait(2000));
                Thread.Sleep(50);     // the guard must swallow the exception; no crash
            }
        }

        [Fact]
        public void Dispose_AfterCallbackPosted_SkipsActionWhenItRuns()
        {
            int count = 0;
            var posted = new ManualResetEventSlim(false);
            var ctx = new QueuedSyncContext(posted);
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(30),
                () => Interlocked.Increment(ref count),
                synchronizationContext: ctx))
            {
                d.Signal();
                Assert.True(posted.Wait(2000), "timer never posted the callback");

                d.Dispose();                     // dispose before the posted callback is pumped
                Assert.True(ctx.TryDrainOne());  // now run it (as the UI pump would)

                Assert.Equal(0, Volatile.Read(ref count));  // re-check suppressed the stale fire
            }
        }

        [Fact]
        public void Callback_IsMarshaledOntoProvidedContext()
        {
            var ctx = new InlineSyncContext();
            var fired = new ManualResetEventSlim(false);
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(30),
                () => fired.Set(),
                synchronizationContext: ctx))
            {
                d.Signal();
                Assert.True(fired.Wait(2000));
                Assert.True(ctx.PostCount >= 1, "callback was not posted to the context");
            }
        }

        [Fact]
        public void Flush_RunsPendingInvocationSynchronously()
        {
            int count = 0;
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(10_000),   // long delay: would not fire on its own during the test
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                d.Flush();                                  // runs it right now, on this thread
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Flush_WithNothingPending_IsNoOp()
        {
            int count = 0;
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(50),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                d.Flush();                                  // never signaled
                Assert.Equal(0, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Flush_DoesNotFireAgainOnTimer()
        {
            int count = 0;
            using (var d = new Debouncer(
                TimeSpan.FromMilliseconds(40),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                d.Flush();              // consumes the pending fire
                Thread.Sleep(120);      // the timer must not fire a second time
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Ctor_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Debouncer(TimeSpan.FromMilliseconds(10), null));
        }

        [Fact]
        public void Ctor_NegativeDelay_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Debouncer(TimeSpan.FromMilliseconds(-1), () => { }));
        }
    }
}
