namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Delegate for the next step in the dispatch pipeline. Invoking it transfers
    /// control to the next middleware (or the registered handler, if the current
    /// middleware is the innermost). Not invoking it short-circuits the pipeline —
    /// the handler will not run.
    /// </summary>
    public delegate void DispatchDelegate(DispatchContext context);

    /// <summary>
    /// A cross-cutting interceptor applied around every <see cref="ViewActionDispatcher.Dispatch"/>
    /// call. Middleware uses an "onion" model: code before <c>next(context)</c> runs
    /// pre-handler; code after <c>next(context)</c> (including <c>finally</c> blocks)
    /// runs post-handler in reverse registration order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typical uses include audit logging, performance measurement, authorization gates,
    /// and unified exception handling — concerns that would otherwise be duplicated
    /// across every action handler.
    /// </para>
    /// <para>
    /// Middleware is invoked synchronously on the UI thread. The framework deliberately
    /// keeps the contract minimal; carry per-dispatch state on instance fields when
    /// possible, and only spill onto <see cref="DispatchContext"/> when downstream
    /// middleware needs to observe it.
    /// </para>
    /// </remarks>
    public interface IDispatchMiddleware
    {
        /// <summary>
        /// Runs the middleware. Call <paramref name="next"/> to continue the pipeline,
        /// or omit it to short-circuit.
        /// </summary>
        void Invoke(DispatchContext context, DispatchDelegate next);
    }
}
