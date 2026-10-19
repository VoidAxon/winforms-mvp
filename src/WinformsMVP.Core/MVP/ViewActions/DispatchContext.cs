using System;

namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Per-dispatch context shared across the middleware pipeline.
    /// Only constructed when at least one middleware is registered on the dispatcher;
    /// the fast (no-middleware) path never allocates a <see cref="DispatchContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The context intentionally exposes only strong-typed core properties. Middleware
    /// instances are encouraged to carry their own state in instance fields rather than
    /// in a generic <c>Items</c> dictionary on the context — this keeps the surface area
    /// small and type-safe. A typed <c>GetFeature&lt;T&gt;/SetFeature&lt;T&gt;</c> API can
    /// be added later if real cross-middleware state-passing needs emerge.
    /// </para>
    /// <para>
    /// <see cref="Payload"/> is intentionally read-only to avoid surprising "magic
    /// conversion" semantics where a middleware silently changes what the handler sees.
    /// </para>
    /// </remarks>
    public class DispatchContext
    {
        /// <summary>The action being dispatched.</summary>
        public ViewAction Action { get; }

        /// <summary>The payload supplied to <c>Dispatch</c>. May be <c>null</c>.</summary>
        public object Payload { get; }

        /// <summary>
        /// The payload type the registered handler expects, or <c>null</c> for
        /// parameterless handlers.
        /// </summary>
        public Type ExpectedPayloadType { get; }

        /// <summary>
        /// <c>true</c> once the registered handler has actually been invoked (for an asynchronous
        /// handler: once it has started; see <see cref="Completion"/>). Middleware
        /// can inspect this in <c>finally</c> blocks to know whether the dispatch reached
        /// the terminal step (for example, whether to record an audit entry as "executed"
        /// vs "short-circuited").
        /// </summary>
        public bool HandlerExecuted { get; internal set; }

        /// <summary>
        /// For an asynchronous handler, the task that completes when the handler finishes; for a
        /// synchronous handler, a completed task. Middleware runs around the handler's synchronous
        /// part only — observe this to time or audit the whole asynchronous operation. A failure
        /// after the first <c>await</c> is logged by the dispatcher, not surfaced through
        /// <see cref="Exception"/> (middleware has already returned by then).
        /// </summary>
        public System.Threading.Tasks.Task Completion { get; internal set; } = System.Threading.Tasks.Task.CompletedTask;

        /// <summary>
        /// The exception thrown by the handler, if any. Set by the framework's built-in
        /// safety-net catch around the handler invocation. Middleware can read this in
        /// outer <c>finally</c> blocks to react to failures, and may clear it (set to
        /// <c>null</c>) to signal "I handled it" to subsequent observers.
        /// </summary>
        public Exception Exception { get; set; }

        internal IViewActionHandler Handler { get; }

        /// <summary>The token passed to an asynchronous handler (cancelled when the owner is disposed).</summary>
        public System.Threading.CancellationToken CancellationToken { get; }

        internal DispatchContext(
            ViewAction action,
            object payload,
            Type expectedPayloadType,
            IViewActionHandler handler,
            System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken))
        {
            CancellationToken = cancellationToken;
            Action = action;
            Payload = payload;
            ExpectedPayloadType = expectedPayloadType;
            Handler = handler;
        }
    }
}
