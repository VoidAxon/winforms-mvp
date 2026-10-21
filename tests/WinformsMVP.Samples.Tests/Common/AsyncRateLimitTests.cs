using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>The async (Func&lt;CancellationToken, Task&gt;) overloads of Debouncer / Throttler / RateLimit.</summary>
    public class AsyncRateLimitTests
    {
        // Single-threaded "UI pump" that records what posted work throws (Application.ThreadException).
        private sealed class Pump : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
            public readonly ConcurrentQueue<Exception> Thrown = new ConcurrentQueue<Exception>();

            public Pump()
            {
                new Thread(() =>
                {
                    SetSynchronizationContext(this);
                    foreach (var work in _queue.GetConsumingEnumerable())
                    {
                        try { work(); } catch (Exception ex) { Thrown.Enqueue(ex); }
                    }
                }) { IsBackground = true }.Start();
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                try { _queue.Add(() => d(state)); }
                catch (InvalidOperationException) { }
            }

            public void Dispose() => _queue.CompleteAdding();
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs = 10000)
        {
            var end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (condition()) return true;
                Thread.Sleep(5);
            }
            return condition();
        }

        [Fact]
        public void Debounce_Async_FiresOnceAfterLastSignal_OnTheContext()
        {
            using (var pump = new Pump())
            {
                int count = 0;
                bool onPump = false;
                using (var d = new Debouncer(TimeSpan.FromMilliseconds(30), async ct =>
                {
                    onPump = SynchronizationContext.Current == pump;
                    await Task.Yield();
                    Interlocked.Increment(ref count);
                }, synchronizationContext: pump))
                {
                    for (int i = 0; i < 5; i++) d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref count) == 1));
                    Thread.Sleep(150);
                    Assert.Equal(1, Volatile.Read(ref count));
                    Assert.True(onPump);
                }
            }
        }

        [Fact]
        public void Debounce_AsyncLambdaWithoutToken_BindsToTheTaskOverload_AndNeverOverlaps()
        {
            // `async () => ...` must not become async void: the run lasts until its task completes.
            using (var pump = new Pump())
            {
                int concurrent = 0, max = 0, runs = 0;
                using (var d = RateLimit.Debounce(10, async () =>
                {
                    int c = Interlocked.Increment(ref concurrent);
                    if (c > max) max = c;
                    await Task.Delay(80);
                    Interlocked.Decrement(ref concurrent);
                    Interlocked.Increment(ref runs);
                }, synchronizationContext: pump, cancelPrevious: false))
                {
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref concurrent) == 1));
                    d.Signal();   // fires while the first run is awaiting
                    Assert.True(WaitUntil(() => Volatile.Read(ref runs) == 2));
                    Assert.Equal(1, max);
                }
            }
        }

        [Fact]
        public void Debounce_Async_NewFireCancelsTheRunInFlight_ThenRunsTheLatest()
        {
            using (var pump = new Pump())
            {
                var tokens = new List<CancellationToken>();
                var completed = new ConcurrentQueue<int>();
                int started = 0;
                using (var d = new Debouncer(TimeSpan.FromMilliseconds(10), async ct =>
                {
                    int run = Interlocked.Increment(ref started);
                    lock (tokens) tokens.Add(ct);
                    await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { }, TaskScheduler.Default);
                    if (!ct.IsCancellationRequested) completed.Enqueue(run);
                    if (run == 2) completed.Enqueue(2);
                }, synchronizationContext: pump))
                {
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref started) == 1));
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref started) == 2), "the latest run did not start");
                    lock (tokens) Assert.True(tokens[0].IsCancellationRequested, "the run in flight was not cancelled");
                    Assert.True(pump.Thrown.IsEmpty, "cancellation must be silent");
                }
            }
        }

        [Fact]
        public void Debounce_Async_CancelPreviousFalse_LetsTheRunFinish_ThenRunsOnceMore()
        {
            using (var pump = new Pump())
            {
                var release = new TaskCompletionSource<bool>();
                var tokens = new List<CancellationToken>();
                int started = 0, finished = 0;
                using (var d = new Debouncer(TimeSpan.FromMilliseconds(10), async ct =>
                {
                    int run = Interlocked.Increment(ref started);
                    lock (tokens) tokens.Add(ct);
                    if (run == 1) await release.Task;
                    Interlocked.Increment(ref finished);
                }, synchronizationContext: pump, cancelPrevious: false))
                {
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref started) == 1));
                    d.Signal();
                    d.Signal();
                    Thread.Sleep(100);
                    Assert.Equal(1, Volatile.Read(ref started));   // waits for the run in flight
                    lock (tokens) Assert.False(tokens[0].IsCancellationRequested);

                    release.SetResult(true);
                    Assert.True(WaitUntil(() => Volatile.Read(ref finished) == 2));
                    Thread.Sleep(100);
                    Assert.Equal(2, Volatile.Read(ref started));   // one trailing run for the whole burst
                }
            }
        }

        [Fact]
        public void Debounce_Async_DisposeCancelsTheRunInFlight_Silently()
        {
            using (var pump = new Pump())
            {
                CancellationToken token = default;
                var started = new ManualResetEventSlim(false);
                var ended = new ManualResetEventSlim(false);
                var d = new Debouncer(TimeSpan.FromMilliseconds(10), async ct =>
                {
                    token = ct;
                    started.Set();
                    try { await Task.Delay(Timeout.Infinite, ct); }
                    finally { ended.Set(); }
                }, synchronizationContext: pump);

                d.Signal();
                Assert.True(started.Wait(10000));
                d.Dispose();
                Assert.True(ended.Wait(10000));
                Assert.True(token.IsCancellationRequested);
                Thread.Sleep(50);
                Assert.True(pump.Thrown.IsEmpty, "an OperationCanceledException from disposal must be silent");
            }
        }

        [Fact]
        public void Debounce_Async_Throws_RoutedToOnError()
        {
            using (var pump = new Pump())
            {
                Exception captured = null;
                var done = new ManualResetEventSlim(false);
                using (var d = new Debouncer(TimeSpan.FromMilliseconds(10), async ct =>
                {
                    await Task.Yield();
                    throw new InvalidOperationException("boom");
                }, onError: ex => { captured = ex; done.Set(); }, synchronizationContext: pump))
                {
                    d.Signal();
                    Assert.True(done.Wait(10000));
                    Assert.IsType<InvalidOperationException>(captured);
                    Assert.True(pump.Thrown.IsEmpty);
                }
            }
        }

        [Fact]
        public void Debounce_Async_Throws_NoHandler_PropagatesThroughContext()
        {
            using (var pump = new Pump())
            using (var d = new Debouncer(TimeSpan.FromMilliseconds(10), async ct =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }, synchronizationContext: pump))
            {
                d.Signal();
                Assert.True(WaitUntil(() => !pump.Thrown.IsEmpty));
                Assert.True(pump.Thrown.TryPeek(out var thrown));
                Assert.IsType<InvalidOperationException>(thrown);
            }
        }

        [Fact]
        public void Debounce_Async_AfterAFailure_FiresAgain()
        {
            using (var pump = new Pump())
            {
                int runs = 0;
                using (var d = new Debouncer(TimeSpan.FromMilliseconds(10), ct =>
                {
                    if (Interlocked.Increment(ref runs) == 1) throw new InvalidOperationException("sync throw");
                    return Task.CompletedTask;
                }, onError: _ => { }, synchronizationContext: pump))
                {
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref runs) == 1));
                    Thread.Sleep(50);
                    d.Signal();
                    Assert.True(WaitUntil(() => Volatile.Read(ref runs) == 2));
                }
            }
        }

        [Fact]
        public void Throttle_Async_LeadingFireRunsAtOnce_AndDisposeCancels()
        {
            using (var pump = new Pump())
            {
                CancellationToken token = default;
                var started = new ManualResetEventSlim(false);
                var t = RateLimit.Throttle(1000, async ct =>
                {
                    token = ct;
                    started.Set();
                    await Task.Delay(Timeout.Infinite, ct);
                }, synchronizationContext: pump);

                t.Signal();
                Assert.True(started.Wait(10000), "the leading fire did not run");
                t.Dispose();
                Assert.True(WaitUntil(() => token.IsCancellationRequested));
                Thread.Sleep(50);
                Assert.True(pump.Thrown.IsEmpty);
            }
        }

        [Fact]
        public void Ctor_NullAsyncAction_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Debouncer(TimeSpan.FromMilliseconds(10), (Func<CancellationToken, Task>)null));
            Assert.Throws<ArgumentNullException>(() => new Throttler(TimeSpan.FromMilliseconds(10), (Func<Task>)null));
        }
    }
}
