using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using WinformsMVP.Logging;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Low-level guarded invocation used by the rate limiters' runners: marshals the callback onto a
    /// captured <see cref="SynchronizationContext"/> (so it lands on the WinForms / WPF UI thread),
    /// keeps the runner's "in flight" state consistent whatever the callback does, and never lets a
    /// failure take down the timer's thread-pool thread.
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
                    .LogError(ex, "Debounced/throttled action threw on a thread-pool thread and no onError handler was supplied.");
            }
            catch
            {
                // Logging must never turn a swallowed callback failure into a crash.
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/>. If <paramref name="context"/> is non-null the call is
        /// posted to it (asynchronously); otherwise it runs inline on the caller's thread (the timer's
        /// thread-pool thread). <paramref name="isLive"/> is re-checked at the moment of invocation —
        /// after marshaling — so an action already queued to a context is skipped if the owner was
        /// disposed in the meantime. <paramref name="onCompleted"/> always runs after the attempt (in a
        /// finally), whether the action ran, threw, or was skipped — the owner uses it to release its
        /// "in flight" state. An exception from the action goes to <paramref name="onError"/>; without
        /// one it propagates out of the posted callback (to the UI thread's unhandled-exception
        /// handler, like an event handler's), and only on the thread-pool path — where it would end
        /// the process — is it logged and dropped.
        /// </summary>
        public static void Run(Action action, SynchronizationContext context, Action<Exception> onError, Func<bool> isLive, Action onCompleted)
        {
            if (context != null)
                context.Post(_ => Guarded(action, onError, isLive, onCompleted, propagate: true), null);
            else
                Guarded(action, onError, isLive, onCompleted, propagate: false);
        }

        /// <summary>
        /// Runs <paramref name="action"/> now on the calling thread (Flush). Without
        /// <paramref name="onError"/> an exception propagates to the caller.
        /// </summary>
        public static void RunHere(Action action, Action<Exception> onError, Func<bool> isLive)
            => Guarded(action, onError, isLive, null, propagate: true);

        /// <summary>
        /// Reports a failure of an async action outside its own call: to <paramref name="onError"/>,
        /// else rethrown on <paramref name="context"/> (preserving the stack), else logged.
        /// </summary>
        public static void Report(Exception ex, SynchronizationContext context, Action<Exception> onError)
        {
            if (onError != null)
            {
                try { onError(ex); }
                catch { /* Never let a faulty error handler escalate into a process kill. */ }
                return;
            }
            if (context != null)
            {
                var failure = ExceptionDispatchInfo.Capture(ex);
                context.Post(_ => failure.Throw(), null);
                return;
            }
            LogUnhandled(ex);
        }

        private static void Guarded(Action action, Action<Exception> onError, Func<bool> isLive, Action onCompleted, bool propagate)
        {
            try
            {
                if (isLive != null && !isLive())
                    return; // Owner was disposed after this callback was scheduled/posted.

                try
                {
                    action();
                }
                catch (Exception ex) when (onError != null || !propagate)
                {
                    if (onError == null)
                    {
                        // Thread-pool path with no handler: don't end the process, but never fail silently.
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
