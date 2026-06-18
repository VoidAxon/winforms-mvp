using System;
using System.Threading;
using WinformsMVP.Common;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    public class RateLimitTests
    {
        private sealed class InlineSyncContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state) => d(state);
        }

        [Fact]
        public void Debounce_ProducesWorkingDebouncer()
        {
            int count = 0;
            var fired = new ManualResetEventSlim(false);
            using (var d = RateLimit.Debounce(40,
                () => { Interlocked.Increment(ref count); fired.Set(); },
                synchronizationContext: new InlineSyncContext()))
            {
                d.Signal();
                d.Signal();
                Assert.True(fired.Wait(2000));
                Thread.Sleep(120);
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Throttle_ProducesWorkingThrottler()
        {
            int count = 0;
            using (var t = RateLimit.Throttle(100,
                () => Interlocked.Increment(ref count),
                synchronizationContext: new InlineSyncContext()))
            {
                t.Signal();   // leading edge fires inline
                Assert.Equal(1, Volatile.Read(ref count));
            }
        }

        [Fact]
        public void Debounce_NegativeMilliseconds_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RateLimit.Debounce(-1, () => { }));
        }

        [Fact]
        public void Throttle_ZeroMilliseconds_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RateLimit.Throttle(0, () => { }));
        }
    }
}
