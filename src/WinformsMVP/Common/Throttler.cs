using System;
using System.Diagnostics;
using System.Threading;

namespace WinformsMVP.Common
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
    /// <b>No pile-up, no concurrency.</b> If the action takes longer than <c>interval</c>, the
    /// trailing fires do not queue up behind it and never overlap: while one run is in flight,
    /// further fires are coalesced into a single trailing run (drop-while-running). So a slow
    /// consumer is paced by its own completion, not flooded — the queue cannot grow without bound.
    /// See <see cref="CoalescingRunner"/>.
    /// </para>
    /// <para>
    /// <b>Error handling.</b> Exceptions from the action are guarded and routed to the optional
    /// <c>onError</c> handler (or swallowed if none), so neither the trailing timer's thread-pool
    /// thread nor the UI pump can be brought down.
    /// </para>
    /// <para>
    /// <b>Thread-safety &amp; disposal.</b> <see cref="Signal"/>, <see cref="Flush"/> and
    /// <see cref="Dispose"/> are safe to call concurrently and disposal is idempotent. After
    /// disposal no new invocation starts, and a callback already scheduled or posted to the context
    /// is skipped when it runs (the disposed flag is re-checked at invocation time) — so disposing
    /// on the UI thread reliably suppresses a pending trailing fire. The one unavoidable case: a
    /// callback whose action is <i>already executing</i> at the instant of disposal runs to
    /// completion. Net40-safe (BCL only, zero dependencies).
    /// </para>
    /// </remarks>
    public sealed class Throttler : IDisposable
    {
        private readonly object _gate = new object();
        private readonly int _intervalMs;
        private readonly Timer _timer;
        private readonly CoalescingRunner _runner;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private long _lastFireMs;
        private bool _hasFired;
        private bool _trailingPending;
        private volatile bool _disposed;

        /// <param name="interval">Minimum time between invocations. Must be greater than zero and at most Int32.MaxValue milliseconds.</param>
        /// <param name="action">The action to invoke. Required.</param>
        /// <param name="onError">Optional handler for exceptions thrown by <paramref name="action"/>. If null, exceptions are swallowed.</param>
        /// <param name="synchronizationContext">Context to marshal the callback onto. If null, <see cref="SynchronizationContext.Current"/> at construction time is captured.</param>
        public Throttler(TimeSpan interval, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            double ms = interval.TotalMilliseconds;
            if (ms <= 0 || ms > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be greater than 0 and at most Int32.MaxValue milliseconds.");

            _intervalMs = (int)ms;
            var context = synchronizationContext ?? SynchronizationContext.Current;
            _runner = new CoalescingRunner(action, context, onError, () => !_disposed);
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
        }
    }
}
