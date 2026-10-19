using System;
using System.Threading;
using WinformsMVP.Logging;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Low-level guarded invocation used by <see cref="CoalescingRunner"/>: marshals the callback
    /// onto a captured <see cref="SynchronizationContext"/> (so it lands on the WinForms / WPF UI
    /// thread) and guards it so a throwing callback can never take down the timer's thread-pool
    /// thread or the UI message pump.
    /// </summary>
    internal static class RateLimitedInvoke
    {
        private static void LogUnhandled(Exception ex)
        {
            try
            {
                var factory = (ILoggerFactory)WinformsMVP.Services.ServiceLocator.Current
                    .GetService(typeof(ILoggerFactory));
                factory?.CreateLogger("WinformsMVP.Common.RateLimit")
                    .LogError(ex, "Debounced/throttled action threw and no onError handler was supplied.");
            }
            catch
            {
                // Logging must never turn a swallowed callback failure into a crash.
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/>. If <paramref name="context"/> is non-null the call is
        /// posted to it (asynchronously); otherwise it runs inline on the caller's thread.
        /// <paramref name="isLive"/> is re-checked at the moment of invocation — after marshaling —
        /// so an action already queued to a context is skipped if the owner was disposed in the
        /// meantime. <paramref name="onCompleted"/> always runs after the attempt (in a finally),
        /// whether the action ran, threw, or was skipped — the owner uses it to release its
        /// "in flight" state. Exceptions from the action are routed to <paramref name="onError"/>
        /// (or swallowed if null); the error handler and the completion callback are themselves guarded.
        /// </summary>
        public static void Run(Action action, SynchronizationContext context, Action<Exception> onError, Func<bool> isLive, Action onCompleted)
        {
            if (context != null)
                context.Post(_ => Guarded(action, onError, isLive, onCompleted), null);
            else
                Guarded(action, onError, isLive, onCompleted);
        }

        private static void Guarded(Action action, Action<Exception> onError, Func<bool> isLive, Action onCompleted)
        {
            try
            {
                if (isLive != null && !isLive())
                    return; // Owner was disposed after this callback was scheduled/posted.

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    if (onError == null)
                    {
                        // No handler: don't crash the timer/UI thread, but never fail silently.
                        LogUnhandled(ex);
                        return;
                    }

                    try
                    {
                        onError(ex);
                    }
                    catch
                    {
                        // Never let a faulty error handler escalate into a process kill.
                    }
                }
            }
            finally
            {
                if (onCompleted != null)
                {
                    try
                    {
                        onCompleted();
                    }
                    catch
                    {
                        // The completion callback only flips internal state; never let it escape.
                    }
                }
            }
        }
    }
}
