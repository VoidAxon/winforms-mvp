using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Coalesces a burst of rapid <see cref="Signal"/> calls into a single deferred invocation:
    /// the action fires once, <c>delay</c> after the <i>last</i> signal. Each new signal restarts
    /// the clock, so while signals keep arriving faster than <c>delay</c>, nothing fires — the
    /// classic "fire when the user stops typing / resizing" behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UI-framework agnostic.</b> The callback is marshaled back onto the
    /// <see cref="SynchronizationContext"/> captured at construction time — that is
    /// <c>WindowsFormsSynchronizationContext</c> under WinForms and
    /// <c>DispatcherSynchronizationContext</c> under WPF. Construct the debouncer on the UI thread
    /// to get this automatically, or pass an explicit context. With no context (e.g. a worker
    /// thread) the callback runs inline on the timer's thread-pool thread.
    /// </para>
    /// <para>
    /// <b>Async actions.</b> The <c>Func&lt;Task&gt;</c> / <c>Func&lt;CancellationToken, Task&gt;</c>
    /// overloads keep a run in flight until its task completes; a newer fire cancels its token by
    /// default (<c>cancelPrevious</c>) and <see cref="Dispose"/> cancels it.
    /// </para>
    /// <para>
    /// <b>No pile-up, no concurrency.</b> If the action takes longer than <c>delay</c>, invocations
    /// do not queue up behind it and never overlap: while one run is in flight, further fires are
    /// coalesced into a single trailing run (drop-while-running). See <see cref="CoalescingRunner"/>.
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
    /// <b>Thread-safety &amp; disposal.</b> <see cref="Signal"/>, <see cref="Cancel"/>,
    /// <see cref="Flush"/> and <see cref="Dispose"/> are safe to call concurrently and disposal is
    /// idempotent. After disposal no new invocation starts, and a callback already scheduled or
    /// posted to the context is skipped when it runs (the disposed flag is re-checked at invocation
    /// time) — so disposing on the UI thread reliably suppresses a pending fire. The one unavoidable
    /// case: a callback whose action is <i>already executing</i> at the instant of disposal runs to
    /// completion.
    /// </para>
    /// </remarks>
    public sealed class Debouncer : IDisposable
    {
        private readonly object _gate = new object();
        private readonly int _delayMs;
        private readonly Timer _timer;
        private readonly IRateLimitRunner _runner;
        private bool _pending;
        private volatile bool _disposed;

        /// <param name="delay">Quiet period after the last signal before the action fires. Must be between 0 and Int32.MaxValue milliseconds.</param>
        /// <param name="action">The action to invoke. Required.</param>
        /// <param name="onError">Optional handler for exceptions thrown by <paramref name="action"/>. Without one, an exception propagates on the captured context (the UI thread's unhandled-exception handler, like an event handler's); with no context it is logged and dropped.</param>
        /// <param name="synchronizationContext">Context to marshal the callback onto. If null, <see cref="SynchronizationContext.Current"/> at construction time is captured.</param>
        public Debouncer(TimeSpan delay, Action action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null)
            : this(delay, synchronizationContext, action == null ? throw new ArgumentNullException(nameof(action))
                : (Func<SynchronizationContext, Func<bool>, IRateLimitRunner>)((context, isLive) => new CoalescingRunner(action, context, onError, isLive)))
        {
        }

        /// <summary>
        /// For an async action (<c>async () =&gt; ...</c> binds here, not to <see cref="Action"/>): a run lasts until its
        /// task completes, so runs never overlap. See the <see cref="Func{T, TResult}">token</see> overload.
        /// </summary>
        public Debouncer(TimeSpan delay, Func<Task> action, Action<Exception> onError = null, SynchronizationContext synchronizationContext = null,
                         bool cancelPrevious = true)
            : this(delay, action == null ? throw new ArgumentNullException(nameof(action)) : (Func<CancellationToken, Task>)(_ => action()),
                   onError, synchronizationContext, cancelPrevious)
        {
        }

        /// <summary>
        /// For an async action that takes a <see cref="CancellationToken"/>: a run lasts until its task completes, so
        /// runs never overlap. A fire that arrives meanwhile runs once after it; with <paramref name="cancelPrevious"/>
        /// (the default) it also cancels the token of the run in flight, so the latest request wins (search as you
        /// type). <see cref="Dispose"/> cancels the run in flight. A cancellation the debouncer asked for is silent.
        /// </summary>
        /// <param name="cancelPrevious">True: a newer fire cancels the run in flight. False: the run in flight finishes first (e.g. saving a draft).</param>
        public Debouncer(TimeSpan delay, Func<CancellationToken, Task> action, Action<Exception> onError = null,
                         SynchronizationContext synchronizationContext = null, bool cancelPrevious = true)
            : this(delay, synchronizationContext, action == null ? throw new ArgumentNullException(nameof(action))
                : (Func<SynchronizationContext, Func<bool>, IRateLimitRunner>)((context, isLive) =>
                    new AsyncCoalescingRunner(action, context, onError, isLive, cancelPrevious)))
        {
        }

        private Debouncer(TimeSpan delay, SynchronizationContext synchronizationContext, Func<SynchronizationContext, Func<bool>, IRateLimitRunner> createRunner)
        {
            double ms = delay.TotalMilliseconds;
            if (ms < 0 || ms > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be between 0 and Int32.MaxValue milliseconds.");

            _delayMs = (int)ms;
            var context = synchronizationContext ?? SynchronizationContext.Current;
            _runner = createRunner(context, () => !_disposed);
            _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Records activity and (re)starts the quiet period. Only the final signal in a burst
        /// results in an invocation. No-op after disposal.
        /// </summary>
        public void Signal()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _pending = true;
                _timer.Change(_delayMs, Timeout.Infinite); // one-shot; restarts on every signal
            }
        }

        /// <summary>
        /// Cancels a pending invocation, if any. The next <see cref="Signal"/> starts fresh.
        /// No-op after disposal.
        /// </summary>
        public void Cancel()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _pending = false;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        /// <summary>
        /// If an invocation is pending, runs it now — <b>synchronously, on the calling thread</b>,
        /// bypassing the captured context — and clears the pending state. No-op if nothing is
        /// pending or after disposal. Call this on the UI thread (e.g. just before
        /// <see cref="Dispose"/> when a window closes) so the last signal is not lost. Because it
        /// runs on the caller's thread it cannot deadlock the UI pump the way a blocking "wait for
        /// the posted callback" would.
        /// </summary>
        public void Flush()
        {
            lock (_gate)
            {
                if (_disposed || !_pending) return;
                _pending = false;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            _runner.RunInlineNow();
        }

        private void OnTick(object state)
        {
            lock (_gate)
            {
                if (_disposed || !_pending) return;
                _pending = false;
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
