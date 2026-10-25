using System;
using System.Threading;

namespace Kibinago.Common
{
    /// <summary>
    /// Serializes invocations of a single action with <b>drop-while-running</b> (coalescing)
    /// semantics, shared by <see cref="Debouncer"/> and <see cref="Throttler"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>Action</c> overloads use this runner; the <c>Func&lt;CancellationToken, Task&gt;</c> overloads use
    /// <see cref="AsyncCoalescingRunner"/>.
    /// </para>
    /// <para>
    /// While an action is in flight (posted to the context but not yet finished), any further
    /// <see cref="Request"/> does not enqueue another invocation — it only records that one more
    /// run is due. When the in-flight action completes, at most one trailing run is dispatched.
    /// </para>
    /// <para>
    /// This bounds the work to "at most one running + at most one pending" at any instant, so a
    /// slow action (one that takes longer than the debounce/throttle window) can neither pile up a
    /// backlog of queued calls nor run concurrently with itself — even when there is no UI
    /// SynchronizationContext to serialize callbacks.
    /// </para>
    /// </remarks>
    internal sealed class CoalescingRunner : IRateLimitRunner
    {
        private readonly object _gate = new object();
        private readonly Action _action;
        private readonly Action<Exception> _onError;
        private readonly SynchronizationContext _context;
        private readonly Func<bool> _isLive;
        private bool _running;
        private bool _runAgain;

        public CoalescingRunner(Action action, SynchronizationContext context, Action<Exception> onError, Func<bool> isLive)
        {
            _action = action;
            _context = context;
            _onError = onError;
            _isLive = isLive;
        }

        /// <summary>
        /// Requests an invocation. Dispatches immediately if idle; otherwise coalesces into a single
        /// trailing run that fires once the in-flight action completes.
        /// </summary>
        public void Request()
        {
            lock (_gate)
            {
                if (_running)
                {
                    _runAgain = true;
                    return;
                }
                _running = true;
            }
            Dispatch();
        }

        private void Dispatch()
        {
            RateLimitedInvoke.Run(_action, _context, _onError, _isLive, OnCompleted);
        }

        private void OnCompleted()
        {
            lock (_gate)
            {
                _running = false;
                if (!_runAgain)
                    return;
                _runAgain = false;
                _running = true; // claim the slot for the trailing run before leaving the lock
            }
            Dispatch();
        }

        /// <summary>
        /// Runs the action once, synchronously on the calling thread, bypassing the context and the
        /// coalescing gate. Used by Flush — call it on the UI thread, where no in-flight action can
        /// be running concurrently.
        /// </summary>
        public void RunInlineNow()
        {
            RateLimitedInvoke.RunHere(_action, _onError, _isLive);
        }

        /// <summary>A synchronous action cannot be interrupted: nothing to cancel.</summary>
        public void CancelInFlight() { }
    }
}
