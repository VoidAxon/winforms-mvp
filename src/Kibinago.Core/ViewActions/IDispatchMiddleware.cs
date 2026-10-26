using System.Threading.Tasks;

namespace Kibinago.ViewActions
{
    /// <summary>
    /// The next step in the dispatch pipeline: the next middleware, or the registered handler for the
    /// innermost one. The returned task completes when that step — including an asynchronous
    /// handler — has finished, and faults with its failure. Not calling it short-circuits the
    /// pipeline: the handler does not run.
    /// </summary>
    /// <param name="context">The dispatch being processed.</param>
    /// <returns>A task that completes when the rest of the pipeline has finished.</returns>
    public delegate Task DispatchDelegate(DispatchContext context);

    /// <summary>
    /// A cross-cutting step around every dispatch (audit, timing, authorization, error dialogs). Onion
    /// model: code before <c>await next(context)</c> runs before the handler, code after it (and
    /// <c>finally</c>) runs after the handler has finished — for an asynchronous handler, after its
    /// last <c>await</c> — in reverse registration order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A failure of the handler arrives at <c>await next(context)</c>. Catching it handles it (it does
    /// not propagate; <see cref="DispatchContext.Exception"/> stays a record); rethrowing, or throwing
    /// another exception, propagates that like an unhandled handler failure. A cancellation because
    /// the presenter was disposed arrives as an <see cref="System.OperationCanceledException"/>; do not
    /// show it as an error. As for an asynchronous handler, an <c>async</c> middleware that ends with an
    /// <see cref="System.OperationCanceledException"/> is a cancellation (silent), not a failure; a
    /// handler is not started once the presenter is disposed.
    /// </para>
    /// <para>
    /// Middleware runs on the dispatching (UI) thread, and continues there after an <c>await</c>. The
    /// action stays disabled until the whole pipeline has finished. Always await <c>next</c>: a
    /// middleware that does not still has the handler tracked to its end, but cannot observe it.
    /// </para>
    /// </remarks>
    public interface IDispatchMiddleware
    {
        /// <summary>
        /// Runs the middleware. Await <paramref name="next"/> to continue the pipeline, or return
        /// without calling it to short-circuit.
        /// </summary>
        /// <param name="context">The dispatch being processed.</param>
        /// <param name="next">The rest of the pipeline.</param>
        /// <returns>A task that completes when this middleware has finished.</returns>
        Task InvokeAsync(DispatchContext context, DispatchDelegate next);
    }
}
