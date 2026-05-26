using System.Diagnostics;
using WinformsMVP.MVP.ViewActions;
using Xunit;
using Xunit.Abstractions;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Performance budget tests for <see cref="ViewActionDispatcher"/>. These are not
    /// micro-benchmarks (no BenchmarkDotNet dependency) but generous upper bounds that
    /// prevent obvious regressions — particularly to guard the contract that simple users
    /// without middleware pay essentially zero overhead.
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
        public void FastPath_ZeroMiddleware_DispatchesMillionInUnderOneSecond()
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

        [Fact]
        public void SlowPath_FiveMiddleware_DispatchesMillionInUnderTwoSeconds()
        {
            var dispatcher = new ViewActionDispatcher();
            int counter = 0;
            dispatcher.Register(TestAction, () => counter++);

            // Five pass-through middleware
            for (int i = 0; i < 5; i++)
            {
                dispatcher.Use((ctx, next) => next(ctx));
            }

            // Warm up (also triggers pipeline compilation)
            for (int i = 0; i < 1000; i++) dispatcher.Dispatch(TestAction);

            const int iterations = 1_000_000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                dispatcher.Dispatch(TestAction);
            }
            sw.Stop();

            _output.WriteLine($"Slow path (5 MW): {iterations} dispatches in {sw.ElapsedMilliseconds}ms ({(double)sw.ElapsedTicks / iterations:F2} ticks/dispatch)");

            // Generous budget. Should easily clear 2s on any halfway-modern CPU.
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Slow-path regression: {iterations} dispatches with 5 middleware took {sw.ElapsedMilliseconds}ms (expected < 2000ms)");
            Assert.Equal(iterations + 1000, counter);
        }

        [Fact]
        public void Pipeline_IsCachedAcrossDispatches_NotRebuiltEachCall()
        {
            // Sanity check: after warm-up the per-dispatch cost is essentially flat.
            // If we accidentally recompile the pipeline per dispatch, total time scales
            // with iteration count and this test will blow out the slow-path budget.
            var dispatcher = new ViewActionDispatcher();
            dispatcher.Register(TestAction, () => { });

            // Add 10 middleware - if we recompiled per dispatch the cost would be dominated
            // by 10 closure allocations per call.
            for (int i = 0; i < 10; i++)
            {
                dispatcher.Use((ctx, next) => next(ctx));
            }

            for (int i = 0; i < 1000; i++) dispatcher.Dispatch(TestAction);  // warm-up

            const int iterations = 500_000;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                dispatcher.Dispatch(TestAction);
            }
            sw.Stop();

            _output.WriteLine($"Cached pipeline (10 MW): {iterations} dispatches in {sw.ElapsedMilliseconds}ms");

            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Pipeline caching regression: {iterations} dispatches with 10 middleware took {sw.ElapsedMilliseconds}ms");
        }
    }
}
