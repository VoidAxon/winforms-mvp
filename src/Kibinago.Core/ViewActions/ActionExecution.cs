using System;

namespace Kibinago.ViewActions
{
    /// <summary>One finished run of an action handler, as reported to <see cref="IActionObserver"/>.</summary>
    /// <remarks>
    /// Only handlers that actually ran are reported: a dispatch rejected before the handler (not
    /// registered, disabled by <c>canExecute</c>, already running, wrong payload type, presenter
    /// disposed) is not.
    /// </remarks>
    public sealed class ActionExecution
    {
        internal ActionExecution(ViewAction action, object payload, Type presenterType, DateTimeOffset startedAt,
            TimeSpan elapsed, bool isAsync, ActionOutcome outcome, Exception exception)
        {
            Action = action;
            Payload = payload;
            PresenterType = presenterType;
            StartedAt = startedAt;
            Elapsed = elapsed;
            IsAsync = isAsync;
            Outcome = outcome;
            Exception = exception;
        }

        /// <summary>The action that ran (a typed action reports its key).</summary>
        public ViewAction Action { get; }

        /// <summary>The value the handler received, or <c>null</c> for an action without a payload.</summary>
        public object Payload { get; }

        /// <summary>The type of the presenter whose dispatcher ran it, or <c>null</c> for a dispatcher of its own.</summary>
        public Type PresenterType { get; }

        /// <summary>When the handler was called (UTC).</summary>
        public DateTimeOffset StartedAt { get; }

        /// <summary>How long the handler took — for an asynchronous one, until its task ended.</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>Whether the handler is an asynchronous one (registered as a <c>Task</c>-returning delegate).</summary>
        public bool IsAsync { get; }

        /// <summary>How the handler ended.</summary>
        public ActionOutcome Outcome { get; }

        /// <summary>The handler's exception when it <see cref="ActionOutcome.Failed"/> (the same instance that propagates); otherwise <c>null</c>, cancellations included.</summary>
        public Exception Exception { get; }
    }

    /// <summary>How an action handler ended.</summary>
    public enum ActionOutcome
    {
        /// <summary>It returned (or its task completed) without an exception. A failure the handler caught itself counts as success.</summary>
        Succeeded,

        /// <summary>It threw (or its task faulted); the exception propagates as usual (Application.ThreadException).</summary>
        Failed,

        /// <summary>It was cancelled: its task was cancelled, or it threw OperationCanceledException because the presenter was disposed.</summary>
        Canceled,
    }
}
