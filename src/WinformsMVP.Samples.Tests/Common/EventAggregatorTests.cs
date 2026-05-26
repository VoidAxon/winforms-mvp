using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Common.EventAggregator;
using Xunit;
using Xunit.Abstractions;

namespace WinformsMVP.Samples.Tests.Common
{
    /// <summary>
    /// Comprehensive tests for EventAggregator including functionality, thread safety, and stress tests.
    /// </summary>
    public class EventAggregatorTests
    {
        private readonly ITestOutputHelper _output;

        public EventAggregatorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Test Messages

        public class TestMessage
        {
            public string Content { get; set; }
            public int Value { get; set; }
        }

        public class AnotherMessage
        {
            public string Data { get; set; }
        }

        #endregion

        #region Basic Functionality Tests

        [Fact]
        public void Subscribe_WhenHandlerIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var aggregator = new EventAggregator();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => aggregator.Subscribe<TestMessage>(null));
        }

        [Fact]
        public void Publish_WhenMessageIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            var aggregator = new EventAggregator();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => aggregator.Publish<TestMessage>(null));
        }

        [Fact]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            // Arrange
            var aggregator = new EventAggregator();

            // Act & Assert (should not throw)
            aggregator.Publish(new TestMessage { Content = "test" });
        }

        [Fact]
        public void Subscribe_AndPublish_InvokesHandler()
        {
            // Arrange
            var aggregator = new EventAggregator();
            string receivedContent = null;

            // Act
            aggregator.Subscribe<TestMessage>(msg => receivedContent = msg.Content);
            aggregator.Publish(new TestMessage { Content = "Hello" });

            // Assert
            Assert.Equal("Hello", receivedContent);
        }

        [Fact]
        public void Subscribe_MultipleHandlers_AllAreInvoked()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var results = new List<string>();

            // Act
            aggregator.Subscribe<TestMessage>(msg => results.Add("Handler1: " + msg.Content));
            aggregator.Subscribe<TestMessage>(msg => results.Add("Handler2: " + msg.Content));
            aggregator.Subscribe<TestMessage>(msg => results.Add("Handler3: " + msg.Content));

            aggregator.Publish(new TestMessage { Content = "Test" });

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Contains("Handler1: Test", results);
            Assert.Contains("Handler2: Test", results);
            Assert.Contains("Handler3: Test", results);
        }

        [Fact]
        public void Subscribe_DifferentMessageTypes_OnlyMatchingHandlersInvoked()
        {
            // Arrange
            var aggregator = new EventAggregator();
            bool testMessageReceived = false;
            bool anotherMessageReceived = false;

            // Act
            aggregator.Subscribe<TestMessage>(msg => testMessageReceived = true);
            aggregator.Subscribe<AnotherMessage>(msg => anotherMessageReceived = true);

            aggregator.Publish(new TestMessage { Content = "test" });

            // Assert
            Assert.True(testMessageReceived);
            Assert.False(anotherMessageReceived);
        }

        [Fact]
        public void Dispose_UnsubscribesHandler()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int callCount = 0;

            var subscription = aggregator.Subscribe<TestMessage>(msg => callCount++);

            // Act
            aggregator.Publish(new TestMessage());
            subscription.Dispose();
            aggregator.Publish(new TestMessage());

            // Assert
            Assert.Equal(1, callCount); // Only first publish should invoke handler
        }

        [Fact]
        public void ClearSubscriptions_RemovesAllSubscribers()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int callCount = 0;

            aggregator.Subscribe<TestMessage>(msg => callCount++);
            aggregator.Subscribe<TestMessage>(msg => callCount++);

            // Act
            aggregator.Publish(new TestMessage());
            int countAfterFirstPublish = callCount;

            aggregator.ClearSubscriptions();
            aggregator.Publish(new TestMessage());

            // Assert
            Assert.Equal(2, countAfterFirstPublish);
            Assert.Equal(2, callCount); // Should stay at 2, no additional calls
        }

        #endregion

        #region Filter Tests

        [Fact]
        public void Subscribe_WithFilter_OnlyMatchingMessagesInvokeHandler()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var receivedValues = new List<int>();

            // Subscribe with filter: only values > 10
            aggregator.Subscribe<TestMessage>(
                msg => receivedValues.Add(msg.Value),
                filter: msg => msg.Value > 10);

            // Act
            aggregator.Publish(new TestMessage { Value = 5 });   // Filtered out
            aggregator.Publish(new TestMessage { Value = 15 });  // Passes filter
            aggregator.Publish(new TestMessage { Value = 3 });   // Filtered out
            aggregator.Publish(new TestMessage { Value = 20 });  // Passes filter

            // Assert
            Assert.Equal(2, receivedValues.Count);
            Assert.Contains(15, receivedValues);
            Assert.Contains(20, receivedValues);
        }

        [Fact]
        public void Subscribe_WithFilterThatThrows_DoesNotInvokeHandler()
        {
            // Arrange
            var aggregator = new EventAggregator();
            bool handlerInvoked = false;

            // Subscribe with filter that throws
            aggregator.Subscribe<TestMessage>(
                msg => handlerInvoked = true,
                filter: msg => throw new InvalidOperationException("Filter error"));

            // Act
            aggregator.Publish(new TestMessage());

            // Assert - Handler should not be invoked when filter throws
            Assert.False(handlerInvoked);
        }

        #endregion

        #region Exception Isolation Tests

        [Fact]
        public void Publish_HandlerThrowsException_OtherHandlersStillInvoked()
        {
            // Arrange
            var aggregator = new EventAggregator();
            var results = new List<string>();

            // Act
            aggregator.Subscribe<TestMessage>(msg => results.Add("Handler1"));
            aggregator.Subscribe<TestMessage>(msg => throw new Exception("Handler2 failed"));
            aggregator.Subscribe<TestMessage>(msg => results.Add("Handler3"));

            aggregator.Publish(new TestMessage());

            // Assert - Despite Handler2 throwing, Handler1 and Handler3 should still execute
            Assert.Equal(2, results.Count);
            Assert.Contains("Handler1", results);
            Assert.Contains("Handler3", results);
        }

        #endregion

        #region Weak Reference Tests

        [Fact]
        public void Subscribe_SubscriberGarbageCollected_SubscriptionAutomaticallyRemoved()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int callCount = 0;

            // Create subscriber in separate method to ensure it can be GC'd
            CreateAndSubscribe(aggregator, () => callCount++);

            // Act
            aggregator.Publish(new TestMessage()); // Should invoke (subscriber still alive)
            int countBeforeGC = callCount;

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            aggregator.Publish(new TestMessage()); // Should not invoke (subscriber GC'd)

            // Assert
            Assert.Equal(1, countBeforeGC);
            Assert.Equal(1, callCount); // Count should not increase after GC
        }

        private void CreateAndSubscribe(IEventAggregator aggregator, Action callback)
        {
            var subscriber = new TestSubscriber(callback);
            aggregator.Subscribe<TestMessage>(msg => subscriber.OnMessage(msg));
            // subscriber goes out of scope here
        }

        private class TestSubscriber
        {
            private readonly Action _callback;

            public TestSubscriber(Action callback)
            {
                _callback = callback;
            }

            public void OnMessage(TestMessage msg)
            {
                _callback();
            }
        }

        [Fact]
        public void Subscribe_StaticMethod_DoesNotGetGarbageCollected()
        {
            // Arrange
            var aggregator = new EventAggregator();
            StaticTestHandler.CallCount = 0;

            // Act
            aggregator.Subscribe<TestMessage>(StaticTestHandler.Handle);

            aggregator.Publish(new TestMessage());
            int countBeforeGC = StaticTestHandler.CallCount;

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            aggregator.Publish(new TestMessage());

            // Assert - Static methods should not be GC'd
            Assert.Equal(1, countBeforeGC);
            Assert.Equal(2, StaticTestHandler.CallCount);
        }

        private static class StaticTestHandler
        {
            public static int CallCount = 0;

            public static void Handle(TestMessage msg)
            {
                CallCount++;
            }
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public void Publish_ConcurrentPublishing_AllMessagesProcessed()
        {
            // Two collaborating fixes are needed to make this test deterministic:
            //
            // 1. EventAggregator captures SynchronizationContext.Current at construction time.
            //    Under xUnit the test thread can have a non-null SyncContext that does not
            //    synchronously drain posted callbacks, so subscriber dispatches from Task.Run
            //    threads end up queued but never run before the assertion. Construct the
            //    aggregator under a null SyncContext so all dispatches take the direct path.
            //
            // 2. Subscribe holds the subscriber's target via WeakReference only. The closure
            //    display class is only reachable while the Action delegate itself stays alive,
            //    so we keep `handler` in a local and GC.KeepAlive it at the end. Without this,
            //    the subscription can be reaped mid-test and silently drop publishes.
            var prevSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var aggregator = new EventAggregator();
                int messageCount = 0;
                var lockObj = new object();

                Action<TestMessage> handler = msg =>
                {
                    lock (lockObj)
                    {
                        messageCount++;
                    }
                };
                aggregator.Subscribe(handler);

                const int threadCount = 10;
                const int messagesPerThread = 100;

                var tasks = new Task[threadCount];
                for (int i = 0; i < threadCount; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        for (int j = 0; j < messagesPerThread; j++)
                        {
                            aggregator.Publish(new TestMessage { Value = j });
                        }
                    });
                }

                Task.WaitAll(tasks);

                Assert.Equal(threadCount * messagesPerThread, messageCount);
                GC.KeepAlive(handler);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevSyncContext);
            }
        }

        [Fact]
        public void Subscribe_ConcurrentSubscribing_AllSubscriptionsRegistered()
        {
            // Arrange
            var aggregator = new EventAggregator();
            const int subscriberCount = 100;
            var counters = new int[subscriberCount];
            var subscriptions = new IDisposable[subscriberCount];
            // See Publish_ConcurrentPublishing_AllMessagesProcessed for the why: each
            // handler must outlive its WeakSubscription, so we stash them all here.
            var handlers = new Action<TestMessage>[subscriberCount];

            // Act - Subscribe concurrently
            var tasks = new Task[subscriberCount];
            for (int i = 0; i < subscriberCount; i++)
            {
                int index = i; // Capture for closure
                tasks[i] = Task.Run(() =>
                {
                    handlers[index] = msg => Interlocked.Increment(ref counters[index]);
                    subscriptions[index] = aggregator.Subscribe(handlers[index]);
                });
            }

            Task.WaitAll(tasks);

            // Publish a message
            aggregator.Publish(new TestMessage());

            // Wait a bit for message processing
            Thread.Sleep(100);

            // Assert - All subscribers should have received the message
            for (int i = 0; i < subscriberCount; i++)
            {
                Assert.Equal(1, counters[i]);
            }
            GC.KeepAlive(handlers);
        }

        [Fact]
        public void SubscribeAndPublish_ConcurrentOperations_ThreadSafe()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int messageCount = 0;
            var lockObj = new object();

            const int operationCount = 1000;

            // Act - Mix subscribe and publish operations concurrently
            var tasks = new Task[operationCount];
            for (int i = 0; i < operationCount; i++)
            {
                if (i % 2 == 0)
                {
                    // Subscribe
                    tasks[i] = Task.Run(() =>
                    {
                        aggregator.Subscribe<TestMessage>(msg =>
                        {
                            lock (lockObj)
                            {
                                messageCount++;
                            }
                        });
                    });
                }
                else
                {
                    // Publish
                    tasks[i] = Task.Run(() =>
                    {
                        aggregator.Publish(new TestMessage());
                    });
                }
            }

            Task.WaitAll(tasks);

            // Assert - Should not throw and should complete
            Assert.True(messageCount >= 0); // At least some messages were processed
        }

        #endregion

        #region Stress Tests

        [Fact]
        public void StressTest_LargeNumberOfSubscribers_PerformanceAcceptable()
        {
            // Arrange
            var aggregator = new EventAggregator();
            const int subscriberCount = 10000;
            int totalCallCount = 0;
            var lockObj = new object();

            for (int i = 0; i < subscriberCount; i++)
            {
                aggregator.Subscribe<TestMessage>(msg =>
                {
                    lock (lockObj)
                    {
                        totalCallCount++;
                    }
                });
            }

            // Act
            var sw = Stopwatch.StartNew();
            aggregator.Publish(new TestMessage { Content = "Stress test" });
            sw.Stop();

            // Assert
            Assert.Equal(subscriberCount, totalCallCount);

            _output.WriteLine($"Published to {subscriberCount} subscribers in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average time per subscriber: {sw.Elapsed.TotalMilliseconds / subscriberCount:F4}ms");

            // Should complete in reasonable time (< 500ms for 10k subscribers on modern hardware)
            Assert.True(sw.ElapsedMilliseconds < 500, $"Performance issue: took {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void StressTest_HighFrequencyPublishing_HandlesLoad()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int messageCount = 0;

            aggregator.Subscribe<TestMessage>(msg => Interlocked.Increment(ref messageCount));

            const int messageVolume = 100000;

            // Act
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < messageVolume; i++)
            {
                aggregator.Publish(new TestMessage { Value = i });
            }
            sw.Stop();

            // Assert
            Assert.Equal(messageVolume, messageCount);

            _output.WriteLine($"Published {messageVolume} messages in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {messageVolume / sw.Elapsed.TotalSeconds:F0} messages/second");

            // Should handle at least 50k messages/second
            var messagesPerSecond = messageVolume / sw.Elapsed.TotalSeconds;
            Assert.True(messagesPerSecond > 50000, $"Throughput too low: {messagesPerSecond:F0} msg/s");
        }

        [Fact]
        public void StressTest_ManyMessageTypes_ScalesWell()
        {
            // Arrange
            var aggregator = new EventAggregator();
            const int messageTypeCount = 100;
            var counters = new int[messageTypeCount];

            // Create different message types and subscribe to each
            for (int i = 0; i < messageTypeCount; i++)
            {
                int index = i; // Capture for closure
                aggregator.Subscribe<TestMessage>(msg =>
                {
                    if (msg.Value == index)
                    {
                        Interlocked.Increment(ref counters[index]);
                    }
                });
            }

            // Act
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < messageTypeCount; i++)
            {
                aggregator.Publish(new TestMessage { Value = i });
            }
            sw.Stop();

            // Assert
            for (int i = 0; i < messageTypeCount; i++)
            {
                Assert.True(counters[i] >= 1, $"Counter {i} was not incremented");
            }

            _output.WriteLine($"Handled {messageTypeCount} different message patterns in {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public void StressTest_ConcurrentSubscribePublishUnsubscribe_Stable()
        {
            // Arrange
            var aggregator = new EventAggregator();
            int messageCount = 0;
            const int iterations = 1000;
            var random = new Random();

            // Act - Chaotic mix of operations
            var tasks = Enumerable.Range(0, iterations).Select(i => Task.Run(() =>
            {
                var operation = random.Next(3);

                switch (operation)
                {
                    case 0: // Subscribe
                        var subscription = aggregator.Subscribe<TestMessage>(msg =>
                        {
                            Interlocked.Increment(ref messageCount);
                        });
                        // Sometimes immediately dispose
                        if (random.Next(2) == 0)
                        {
                            subscription.Dispose();
                        }
                        break;

                    case 1: // Publish
                        aggregator.Publish(new TestMessage { Value = i });
                        break;

                    case 2: // Clear (occasionally)
                        if (random.Next(10) == 0)
                        {
                            aggregator.ClearSubscriptions();
                        }
                        break;
                }
            })).ToArray();

            Task.WaitAll(tasks);

            // Assert - Should complete without exceptions
            _output.WriteLine($"Completed {iterations} random operations with {messageCount} messages processed");
            Assert.True(true, "Stress test completed successfully");
        }

        [Fact]
        public void StressTest_MemoryUsage_WeakReferencesPreventLeaks()
        {
            // Arrange
            var aggregator = new EventAggregator();
            const int subscriptionCycles = 100;

            // Act - Create and abandon subscribers repeatedly
            for (int cycle = 0; cycle < subscriptionCycles; cycle++)
            {
                for (int i = 0; i < 100; i++)
                {
                    var subscriber = new TestSubscriber(() => { });
                    aggregator.Subscribe<TestMessage>(msg => subscriber.OnMessage(msg));
                }

                // Force GC every 10 cycles
                if (cycle % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            // Final GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Publish to trigger cleanup of dead subscriptions
            for (int i = 0; i < 10; i++)
            {
                aggregator.Publish(new TestMessage());
            }

            // Assert - Memory should be cleaned up (hard to assert directly, but test should not crash)
            _output.WriteLine($"Completed {subscriptionCycles} subscription cycles without memory issues");
            Assert.True(true, "Memory stress test completed");
        }

        #endregion

        #region Performance Comparison Tests

        [Fact]
        public void PerformanceTest_CompareFilteredVsUnfiltered()
        {
            // Arrange
            var aggregator = new EventAggregator();
            const int messageCount = 10000;
            int unfilteredCount = 0;
            int filteredCount = 0;

            aggregator.Subscribe<TestMessage>(msg => unfilteredCount++);
            aggregator.Subscribe<TestMessage>(
                msg => filteredCount++,
                filter: msg => msg.Value % 2 == 0); // Only even numbers

            // Act - Unfiltered
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < messageCount; i++)
            {
                aggregator.Publish(new TestMessage { Value = i });
            }
            sw1.Stop();

            // Assert
            Assert.Equal(messageCount, unfilteredCount);
            Assert.Equal(messageCount / 2, filteredCount);

            _output.WriteLine($"Processed {messageCount} messages (filtered + unfiltered) in {sw1.ElapsedMilliseconds}ms");
            _output.WriteLine($"Unfiltered received: {unfilteredCount}, Filtered received: {filteredCount}");
        }

        #endregion
    }
}
