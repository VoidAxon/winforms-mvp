using System.Diagnostics;
using Kibinago.ViewActions;
using Xunit;
using Xunit.Abstractions;

namespace Kibinago.Samples.Tests.ViewActions
{
    /// <summary>
    /// Performance budget tests for <see cref="ViewActionDispatcher"/>. These are not
    /// micro-benchmarks (no BenchmarkDotNet dependency) but generous upper bounds that
    /// prevent obvious regressions — particularly to guard the contract that simple users
    /// without an observer pay essentially zero overhead.
    /// </summary>
    /// <remarks>
    /// The thresholds are intentionally loose so the tests don't flake on slow CI hardware.
    /// Typical observed numbers on a modern dev box are ~10x faster than the asserted budget.
    /// If these fail you almost certainly introduced a regression, not just hit a slow box.
    /// </remarks>
    public class ViewActionDispatcherPerformanceTests
    {
        private static readonly ViewAction TestAction = ViewAction.Create("Perf.Action");
        private readonly ITestOutputHelper _output;

        public ViewActionDispatcherPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FastPath_NoObserver_DispatchesMillionInUnderOneSecond()
        {
            var dispatcher = new ViewActionDispatcher();
            int counter = 0;
            dispatcher.Register(TestAction, () => counter++);

            // Warm up
            for (int i = 0; i < 1000; i++) dispatcher.Dispatch(TestAction);

            const int iterations = 1_000_000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                dispatcher.Dispatch(TestAction);
            }
            sw.Stop();

            _output.WriteLine($"Fast path: {iterations} dispatches in {sw.ElapsedMilliseconds}ms ({(double)sw.ElapsedTicks / iterations:F2} ticks/dispatch)");

            // Generous budget. Real number is typically ~50-100ms on modern hardware.
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Fast-path regression: {iterations} dispatches took {sw.ElapsedMilliseconds}ms (expected < 1000ms)");
            Assert.Equal(iterations + 1000, counter);
        }

        private sealed class NoOpObserver : IActionObserver
        {
            public void OnExecuted(ActionExecution execution) { }
        }

        [Fact]
        public void WithObserver_DispatchesMillionInUnderTwoSeconds()
        {
            // An observer adds a Stopwatch and one ActionExecution per dispatch.
            var dispatcher = new ViewActionDispatcher { Observer = new NoOpObserver() };
            int counter = 0;
            dispatcher.Register(TestAction, () => counter++);

            for (int i = 0; i < 1000; i++) dispatcher.Dispatch(TestAction);   // warm-up

            const int iterations = 1_000_000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                dispatcher.Dispatch(TestAction);
            }
            sw.Stop();

            _output.WriteLine($"With observer: {iterations} dispatches in {sw.ElapsedMilliseconds}ms ({(double)sw.ElapsedTicks / iterations:F2} ticks/dispatch)");

            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Observer regression: {iterations} dispatches took {sw.ElapsedMilliseconds}ms (expected < 2000ms)");
            Assert.Equal(iterations + 1000, counter);
        }
    }
}
