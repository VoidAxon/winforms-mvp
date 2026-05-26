using System;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Logging;
using WinformsMVP.Common.EventAggregator;
using WinformsMVP.Samples.Tests.TestHelpers;
using Xunit;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Tests that verify <see cref="EventAggregator"/> reports filter exceptions, handler exceptions,
    /// and Post failures through the supplied <see cref="ILoggerFactory"/> instead of silently
    /// swallowing them with <c>Debug.WriteLine</c>.
    /// </summary>
    public class EventAggregatorLoggerTests
    {
        public class TestMessage
        {
            public string Content { get; set; }
        }

        [Fact]
        public void DefaultConstructor_StillSwallowsErrors_NoExceptionsPropagate()
        {
            var aggregator = new EventAggregator();

            aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("boom"));

            var ex = Record.Exception(() => aggregator.Publish(new TestMessage { Content = "x" }));

            Assert.Null(ex);
        }

        [Fact]
        public void Publish_HandlerThrows_LogsErrorThroughInjectedLoggerFactory()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var aggregator = new EventAggregator(loggerFactory);

            aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("boom"));

            aggregator.Publish(new TestMessage { Content = "x" });

            Assert.Single(loggerFactory.Logger.Entries);
            Assert.Equal(LogLevel.Error, loggerFactory.Logger.Entries[0].Level);
            Assert.IsType<InvalidOperationException>(loggerFactory.Logger.Entries[0].Exception);
            Assert.Contains("handler", loggerFactory.Logger.Entries[0].Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Publish_FilterThrows_LogsErrorAndSkipsHandler()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var aggregator = new EventAggregator(loggerFactory);

            bool handlerCalled = false;
            aggregator.Subscribe<TestMessage>(
                _ => handlerCalled = true,
                filter: _ => throw new InvalidOperationException("filter failure"));

            aggregator.Publish(new TestMessage { Content = "x" });

            Assert.False(handlerCalled);
            Assert.Single(loggerFactory.Logger.Entries);
            Assert.Equal(LogLevel.Error, loggerFactory.Logger.Entries[0].Level);
            Assert.Contains("filter", loggerFactory.Logger.Entries[0].Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Publish_MultipleSubscribers_ContinuesAfterOneThrows_AndAllAreLogged()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var aggregator = new EventAggregator(loggerFactory);

            int successCount = 0;
            aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("first"));
            aggregator.Subscribe<TestMessage>(_ => successCount++);
            aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("third"));

            aggregator.Publish(new TestMessage { Content = "x" });

            Assert.Equal(1, successCount);
            Assert.Equal(2, loggerFactory.Logger.Entries.Count);
            Assert.All(loggerFactory.Logger.Entries, e => Assert.Equal(LogLevel.Error, e.Level));
        }

        [Fact]
        public void Constructor_NullLoggerFactory_FallsBackToNullLoggerSilently()
        {
            var aggregator = new EventAggregator(loggerFactory: null);

            aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("boom"));

            var ex = Record.Exception(() => aggregator.Publish(new TestMessage { Content = "x" }));

            Assert.Null(ex);
        }

        [Fact]
        public void Publish_FromBackgroundThread_HandlerExceptionStillLogged()
        {
            // EventAggregator captures SyncContext.Current at construction. Set one before constructing,
            // restore it before awaiting anything so the test thread itself never depends on draining.
            var prev = SynchronizationContext.Current;
            var ctx = new SingleThreadSyncContext();
            SynchronizationContext.SetSynchronizationContext(ctx);

            CapturingLoggerFactory loggerFactory;
            EventAggregator aggregator;
            try
            {
                loggerFactory = new CapturingLoggerFactory();
                aggregator = new EventAggregator(loggerFactory);
                aggregator.Subscribe<TestMessage>(_ => throw new InvalidOperationException("boom"));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }

            // Publish from a true background thread; subscribers get marshaled back to `ctx` via Post.
            Task.Run(() => aggregator.Publish(new TestMessage { Content = "x" })).GetAwaiter().GetResult();

            ctx.Drain();

            Assert.Single(loggerFactory.Logger.Entries);
            Assert.Equal(LogLevel.Error, loggerFactory.Logger.Entries[0].Level);
            Assert.IsType<InvalidOperationException>(loggerFactory.Logger.Entries[0].Exception);
        }

        /// <summary>
        /// Minimal SynchronizationContext that records Post callbacks and runs them on demand.
        /// Used to simulate the WinForms UI thread without depending on Application.Run.
        /// </summary>
        private sealed class SingleThreadSyncContext : SynchronizationContext
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue =
                new System.Collections.Concurrent.ConcurrentQueue<Action>();

            public override void Post(SendOrPostCallback d, object state)
            {
                _queue.Enqueue(() => d(state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                d(state);
            }

            public void Drain()
            {
                while (_queue.TryDequeue(out var action))
                {
                    action();
                }
            }
        }
    }
}
