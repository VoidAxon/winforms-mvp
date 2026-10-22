using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class ThrottlerTests
    {
        // Runs posted callbacks inline so the leading-edge fire is observable synchronously and the
        // trailing fire is deterministic, independent of the ambient SynchronizationContext.
        private sealed class InlineSyncContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) => d(state);
        }

        // Single-threaded "UI pump": posted callbacks are executed one at a time on a dedicated
        // thread, mirroring how a WinForms/WPF message loop serializes BeginInvoke work.
        private sealed class SingleThreadPump : SynchronizationContext, IDisposable
        {
            private readonly System.Collections.Concurrent.BlockingCollection<Action> _queue
                = new System.Collections.Concurrent.BlockingCollection<Action>();
            private readonly Thread _thread;

            public SingleThreadPump()
            {
                _thread = new Thread(Loop) { IsBackground = true };
                _thread.Start();
            }

            private void Loop()
            {
                foreach (var work in _queue.GetConsumingEnumerable())
                {
                    try { work(); } catch { /* mirror a pump that swallows handler faults */ }
                }
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                try { _queue.Add(() => d(state)); }
                catch (InvalidOperationException) { /* pump already completed */ }
            }

            public void Dispose() => _queue.CompleteAdding();
        }

        [Fact]
        public void FirstSignal_FiresImmediately()
        {
            int count = 0;
            using (var t = new Throttler(
                TimeSpan.FromMilliseconds(100),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                t.Signal();   // leading edge runs inline within Signal()
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void BurstWithinInterval_FiresLeadingThenSingleTrailing()
        {
            int count = 0;
            var second = new ManualResetEventSlim(false);
            using (var t = new Throttler(
                TimeSpan.FromMilliseconds(80),
                () => { if (Interlocked.Increment(ref count) == 2) second.Set(); },
                synchronizationContext: new InlineSyncContext()))
            {
                for (int i = 0; i < 10; i++) t.Signal();   // 1 leading now + 9 collapsed into 1 trailing

                Assert.True(second.Wait(10000), "trailing fire did not occur");
                Thread.Sleep(150);                          // ensure no further fires
                Assert.Equal(2, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Dispose_PreventsTrailingFire()
        {
            int count = 0;
            var t = new Throttler(
                TimeSpan.FromMilliseconds(100),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext());

            t.Signal();   // leading -> 1
            t.Signal();   // schedules trailing
            t.Dispose();
            t.Dispose();  // idempotent
            Thread.Sleep(200);
            Assert.Equal(1, Volatile.Read(ref count));
        }

        [Fact]
        public void ActionThrows_RoutedToOnError()
        {
            Exception captured = null;
            using (var t = new Throttler(
                TimeSpan.FromMilliseconds(50),
                (Action)(() => throw new InvalidOperationException("boom")),
                onError: ex => captured = ex,
                synchronizationContext: new InlineSyncContext()))
            {
                t.Signal();   // leading fires inline, throws, is caught
                Assert.IsType<InvalidOperationException>(captured);
            }
        }

        [Fact]
        public void Flush_RunsPendingTrailingSynchronously()
        {
            int count = 0;
            using (var t = new Throttler(
                TimeSpan.FromMilliseconds(10_000),   // long interval so the trailing timer won't fire on its own
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                t.Signal();                 // leading -> 1
                t.Signal();                 // schedules trailing
                Assert.Equal(1, Volatile.Read(ref count));

                t.Flush();                  // runs the trailing now, on this thread
                Assert.Equal(2, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Flush_WithNoTrailingPending_IsNoOp()
        {
            int count = 0;
            using (var t = new Throttler(
                TimeSpan.FromMilliseconds(100),
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                t.Signal();   // leading -> 1, nothing trailing pending
                t.Flush();    // no-op
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void SlowAction_DoesNotPileUp()
        {
            // Action (50ms) is much slower than the interval (10ms). Without drop-while-running,
            // trailing fires would queue up on the pump and replay dozens of stale calls after the
            // signals stop. With coalescing, throughput is paced by the action's own duration.
            using (var pump = new SingleThreadPump())
            {
                int executed = 0;
                using (var t = new Throttler(
                    TimeSpan.FromMilliseconds(10),
                    () => { Interlocked.Increment(ref executed); Thread.Sleep(50); },
                    synchronizationContext: pump))
                {
                    var sw = Stopwatch.StartNew();
                    while (sw.ElapsedMilliseconds < 300)   // ~60 signals
                    {
                        t.Signal();
                        Thread.Sleep(5);
                    }
                    Thread.Sleep(400);   // let the pump drain any coalesced trailing work

                    int count = Volatile.Read(ref executed);
                    // ~700ms of wall time / 50ms per run => well under ~15; a pile-up would be ~60+.
                    Assert.True(count >= 1, "action never ran");
                    Assert.True(count <= 15, "action piled up: ran " + count + " times");
                }
            }
        }

        [Fact]
        public void SlowAction_NeverRunsConcurrently()
        {
            // No SynchronizationContext: leading runs on the signaling thread, trailing on the timer
            // thread. Drop-while-running must still prevent the action from overlapping itself.
            int concurrent = 0;
            int max = 0;
            var maxGate = new object();

            // A null argument means "capture SynchronizationContext.Current", and xUnit installs its
            // own context on the test thread (posting there can starve on a busy CI machine), so
            // construct with no current context to get the context-free behavior this test is about.
            Throttler throttler;
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                throttler = new Throttler(
                    TimeSpan.FromMilliseconds(5),
                    () =>
                    {
                        int c = Interlocked.Increment(ref concurrent);
                        lock (maxGate) { if (c > max) max = c; }
                        Thread.Sleep(20);
                        Interlocked.Decrement(ref concurrent);
                    },
                    synchronizationContext: null);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            using (var t = throttler)
            {
                var threads = new List<Thread>();
                for (int i = 0; i < 4; i++)
                {
                    var th = new Thread(() =>
                    {
                        var sw = Stopwatch.StartNew();
                        while (sw.ElapsedMilliseconds < 200) { t.Signal(); Thread.Sleep(2); }
                    }) { IsBackground = true };
                    threads.Add(th);
                    th.Start();
                }
                foreach (var th in threads) th.Join();
                Thread.Sleep(100);   // let any trailing run finish

                Assert.Equal(1, Volatile.Read(ref max));
            }
        }

        [Fact]
        public void Ctor_NullAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Throttler(TimeSpan.FromMilliseconds(10), (Action)null));
        }

        [Fact]
        public void Ctor_ZeroInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Throttler(TimeSpan.Zero, () => { }));
        }
    }
}
