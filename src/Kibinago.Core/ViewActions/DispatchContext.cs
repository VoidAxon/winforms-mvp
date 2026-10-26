using System;

namespace Kibinago.ViewActions
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
        /// <c>true</c> once the registered handler has been invoked (an asynchronous one: once it has
        /// started). Middleware can read it in <c>finally</c> to tell "executed" from
        /// "short-circuited" (for example, in an audit entry).
        /// </summary>
        public bool HandlerExecuted { get; internal set; }

        /// <summary>
        /// The handler's failure, if any — thrown synchronously or after an <c>await</c>. Set as the
        /// failure leaves the handler, so an outer middleware's <c>finally</c> already sees it. It is a
        /// record: whether the failure propagates depends only on whether a middleware caught it.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>The handler threw (even if a middleware then cleared <see cref="Exception"/>).</summary>
        internal bool HandlerFailed { get; set; }

        internal IViewActionHandler Handler { get; }

        /// <summary>What the handler step returned (null until it ran, or for a synchronous handler):
        /// tracked to its end even when a middleware does not await it.</summary>
        internal System.Threading.Tasks.Task HandlerTask { get; set; }

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
