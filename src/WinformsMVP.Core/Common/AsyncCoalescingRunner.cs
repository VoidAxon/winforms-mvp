using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinformsMVP.Common
{
    /// <summary>
    /// The async counterpart of <see cref="CoalescingRunner"/>, for a
    /// <c>Func&lt;CancellationToken, Task&gt;</c> action: a run is "in flight" until its task completes,
    /// so runs never overlap and at most one more is pending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fire that arrives while a run is in flight is coalesced into one run after it. With
    /// <c>cancelPrevious</c> (the default) it also cancels the token of the run in flight, so a stale
    /// search stops early and the latest one follows; without it the run in flight finishes first.
    /// Disposal cancels the run in flight and drops the pending one.
    /// </para>
    /// <para>
    /// The action starts on the captured context (the UI thread) and its continuations come back to
    /// it. A cancellation the runner asked for is silent. Any other failure goes to <c>onError</c>, or
    /// without one is rethrown on the context (the UI thread's unhandled-exception handler, like an
    /// async ViewAction handler's); with no context it is logged.
    /// </para>
    /// </remarks>
    internal sealed class AsyncCoalescingRunner : IRateLimitRunner
    {
        private readonly object _gate = new object();
        private readonly Func<CancellationToken, Task> _action;
        private readonly SynchronizationContext _context;
        private readonly Action<Exception> _onError;
        private readonly Func<bool> _isLive;
        private readonly bool _cancelPrevious;
        private bool _running;
        private bool _runAgain;
        private CancellationTokenSource _current;

        public AsyncCoalescingRunner(Func<CancellationToken, Task> action, SynchronizationContext context, Action<Exception> onError,
                                     Func<bool> isLive, bool cancelPrevious)
        {
            _action = action;
            _context = context;
            _onError = onError;
            _isLive = isLive;
            _cancelPrevious = cancelPrevious;
        }

        public void Request() => Start(inline: false);

        public void RunInlineNow() => Start(inline: true);

        public void CancelInFlight()
        {
            CancellationTokenSource current;
            lock (_gate)
            {
                current = _current;
                _runAgain = false;
            }
            Cancel(current);
        }

        private void Start(bool inline)
        {
            CancellationTokenSource stale = null;
            bool dispatch;
            lock (_gate)
            {
                if (_running)
                {
                    _runAgain = true;   // one run after the one in flight, however many fires arrive
                    if (_cancelPrevious) stale = _current;
                    dispatch = false;
                }
                else
                {
                    _running = true;
                    dispatch = true;
                }
            }
            Cancel(stale);   // outside the lock: token callbacks run synchronously
            if (!dispatch) return;

            if (inline || _context == null) RunOnce();
            else _context.Post(_ => RunOnce(), null);
        }

        private async void RunOnce()
        {
            // async void is safe here: every exception is caught below, so nothing escapes to the context.
            CancellationTokenSource cts = null;
            try
            {
                lock (_gate)
                {
                    // Posted but superseded before it started: let the newer fire run instead.
                    if (!_isLive() || (_runAgain && _cancelPrevious)) return;
                    cts = new CancellationTokenSource();
                    _current = cts;
                }
                await (_action(cts.Token) ?? Task.CompletedTask);
            }
            catch (OperationCanceledException) when (cts != null && cts.IsCancellationRequested)
            {
                // Cancelled by a newer fire or by disposal: not a failure.
            }
            catch (Exception ex)
            {
                RateLimitedInvoke.Report(ex, _context, _onError);
            }
            finally
            {
                OnCompleted(cts);
            }
        }

        private void OnCompleted(CancellationTokenSource cts)
        {
            bool again;
            lock (_gate)
            {
                if (ReferenceEquals(_current, cts)) _current = null;
                again = _runAgain && _isLive();
                _runAgain = false;
                _running = again;   // keep the slot for the trailing run
            }
            cts?.Dispose();
            if (!again) return;

            if (_context == null) RunOnce();
            else _context.Post(_ => RunOnce(), null);
        }

        private static void Cancel(CancellationTokenSource cts)
        {
            try { cts?.Cancel(); }
            catch (ObjectDisposedException) { /* the run completed meanwhile */ }
            catch (AggregateException) { /* a token callback threw: the run reports its own failure */ }
        }

    }
}
