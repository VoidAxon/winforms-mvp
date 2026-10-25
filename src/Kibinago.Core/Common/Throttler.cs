using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Kibinago.Common
{
    /// <summary>
    /// Rate-limits rapid <see cref="Signal"/> calls so the action runs at most once per
    /// <c>interval</c>. Uses leading-and-trailing semantics: the first signal fires immediately
    /// (leading edge); further signals inside the window are collapsed into a single trailing fire
    /// at the window's end, so the latest activity is never lost. Ideal for continuous streams —
    /// scrolling, dragging, resizing — where you want steady periodic updates rather than waiting
    /// for the stream to stop (which is what <see cref="Debouncer"/> does).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UI-framework agnostic.</b> The callback is marshaled back onto the
    /// <see cref="SynchronizationContext"/> captured at construction time
    /// (<c>WindowsFormsSynchronizationContext</c> under WinForms,
    /// <c>DispatcherSynchronizationContext</c> under WPF). Construct it on the UI thread to capture
    /// that automatically, or pass an explicit context; with none, the callback runs inline.
    /// </para>
    /// <para>
    /// <b>Async actions.</b> The <c>Func&lt;Task&gt;</c> / <c>Func&lt;CancellationToken, Task&gt;</c>
    /// overloads keep a run in flight until its task completes; a newer fire cancels its token by
    /// default (<c>cancelPrevious</c>) and <see cref="Dispose"/> cancels it.
    /// </para>
    /// <para>
    /// <b>No pile-up, no concurrency.</b> If the action takes longer than <c>interval</c>, the
    /// trailing fires do not queue up behind it and never overlap: while one run is in flight,
    /// further fires are coalesced into a single trailing run (drop-while-running). So a slow
    /// consumer is paced by its own completion, not flooded — the queue cannot grow without bound.
    /// See <see cref="CoalescingRunner"/>.
    /// </para>
    /// <para>
    /// <b>Error handling.</b> An exception from the action goes to the optional <c>onError</c>
    /// handler. Without one it propagates on the captured context — under WinForms it reaches
    /// <c>Application.ThreadException</c>, like an event handler's or an async ViewAction handler's —
    /// so a failure is never silent. Only with no context (the timer's thread-pool thread, where an
    /// unhandled exception would end the process) is it logged and dropped. <see cref="Flush"/>
    /// runs the action on the calling thread, so there the exception propagates to the caller.
    /// </para>
    /// <para>
    /// <b>Thread-safety &amp; disposal.</b> <see cref="Signal"/>, <see cref="Flush"/> and
    /// <see cref="Dispose"/> are safe to call concurrently and disposal is idempotent. After
    /// disposal no new invocation starts, and a callback already scheduled or posted to the context
    /// is skipped when it runs (the disposed flag is re-checked at invocation time) — so disposing
    /// on the UI thread reliably suppresses a pending trailing fire. The one unavoidable case: a
    /// callback whose action is <i>already executing</i> at the instant of disposal runs to
    /// completion.
    /// </para>
    /// </remarks>
    public sealed class Throttler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly int _intervalMs;
        private readonly Timer _timer;
        private readonly IRateLimitRunner _runner;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastFireMs;
        private bool _hasFired;
        private bool _trailingPending;
        private volatile bool _disposed;

        /// <param name="interval">Minimum time between invocations. Must be greater than zero and at most Int32.MaxValue milliseconds.</param>
        /// <param name="action">The action to invoke. Required.</param>
        /// <param name="onError">Optional handler for exceptions thrown by <paramref name="action"/>. Without one, an exception propagates on the captured context (the UI thread's unhandled-exception handler, like an event handler's); with no context it is logged and dropped.</param>
        /// <param name="synchronizationContext">Context to marshal the callback onto. If null, <see cref="SynchronizationContext.Current"/> at construction time is captured.</param>
        public Throttler(TimeSpan interval, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            : this(interval, synchronizationContext, action == null ? throw new ArgumentNullException(nameof(action))
                : (Func<SynchronizationContext, Func<bool>, IRateLimitRunner>)((context, isLive) => new CoalescingRunner(action, context, onError, isLive)))
        {
        }

        /// <summary>
        /// For an async action (<c>async () =&gt; ...</c> binds here, not to <see cref="Action"/>): a run lasts until its
        /// task completes, so runs never overlap. See the <see cref="Func{T, TResult}">token</see> overload.
        /// </summary>
        public Throttler(TimeSpan interval, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null,
                         bool cancelPrevious = true)
            : this(interval, action == null ? throw new ArgumentNullException(nameof(action)) : (Func<CancellationToken, Task>)(_ => action()),
                   onError, synchronizationContext, cancelPrevious)
        {
        }

        /// <summary>
        /// For an async action that takes a <see cref="CancellationToken"/>: a run lasts until its task completes, so
        /// runs never overlap. A fire that arrives meanwhile runs once after it; with <paramref name="cancelPrevious"/>
        /// (the default) it also cancels the token of the run in flight, so the latest request wins (search as you
        /// type). <see cref="Dispose"/> cancels the run in flight. A cancellation the throttler asked for is silent.
        /// </summary>
        /// <param name="cancelPrevious">True: a newer fire cancels the run in flight. False: the run in flight finishes first (e.g. saving a draft).</param>
        public Throttler(TimeSpan interval, Func<CancellationToken, Task> action, Action<Exception> onError = null,
                         SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            : this(interval, synchronizationContext, action == null ? throw new ArgumentNullException(nameof(action))
                : (Func<SynchronizationContext, Func<bool>, IRateLimitRunner>)((context, isLive) =>
                    new AsyncCoalescingRunner(action, context, onError, isLive, cancelPrevious)))
        {
        }

        private Throttler(TimeSpan interval, SynchronizationContext synchronizationContext, Func<SynchronizationContext, Func<bool>, IRateLimitRunner> createRunner)
        {
            double ms = interval.TotalMilliseconds;
            if (ms <= 0 || ms > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than 0 and at most Int32.MaxValue milliseconds.");

            _intervalMs = (int)ms;
            var context = synchronizationContext ?? SynchronizationContext.Current;
            _runner = createRunner(context, () => !_disposed);
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Records activity. Fires immediately if the interval since the last fire has elapsed
        /// (leading edge); otherwise schedules a single trailing fire at the end of the current
        /// window. No-op after disposal.
        /// </summary>
        public void Signal()
        {
            bool fireNow = false;
            lock (_gate)
            {
                if (_disposed) return;

                long now = _clock.ElapsedMilliseconds;
                long sinceLast = now - _lastFireMs;

                if (!_hasFired || sinceLast >= _intervalMs)
                {
                    // Leading edge: outside the window, fire right away.
                    _hasFired = true;
                    _lastFireMs = now;
                    fireNow = true;
                }
                else if (!_trailingPending)
                {
                    // Inside the window: arrange a single trailing fire when it ends.
                    _trailingPending = true;
                    _timer.Change((int)(_intervalMs - sinceLast), Timeout.Infinite);
                }
            }

            if (fireNow)
                _runner.Request();
        }

        /// <summary>
        /// If a trailing fire is pending, runs it now — <b>synchronously, on the calling thread</b>,
        /// bypassing the captured context — and clears the pending state. No-op if nothing is
        /// pending or after disposal. Call this on the UI thread (e.g. just before
        /// <see cref="Dispose"/>) so the latest activity is not lost. Because it runs on the
        /// caller's thread it cannot deadlock the UI pump the way a blocking wait would.
        /// </summary>
        public void Flush()
        {
            lock (_gate)
            {
                if (_disposed || !_trailingPending) return;
                _trailingPending = false;
                _lastFireMs = _clock.ElapsedMilliseconds;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            _runner.RunInlineNow();
        }

        private void OnTick(object state)
        {
            lock (_gate)
            {
                if (_disposed || !_trailingPending) return;
                _trailingPending = false;
                _lastFireMs = _clock.ElapsedMilliseconds;
            }
            _runner.Request();
        }

        /// <summary>Stops the timer and releases resources. Idempotent.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _timer.Dispose();
            _runner.CancelInFlight();
        }
    }
}
