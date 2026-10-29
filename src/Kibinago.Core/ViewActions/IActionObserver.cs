namespace Kibinago.ViewActions
{
    /// <summary>
    /// Observes every action handler that ran, once it has finished — for operation diagnostics: timing,
    /// an operation log, the payloads behind a failure. It cannot change what happens (no interception,
    /// no retry, no catching): decisions belong in the handler, unexpected errors in
    /// <c>Application.ThreadException</c>, permissions in <c>canExecute</c> and the services.
    /// </summary>
    /// <remarks>
    /// <para>Register one per application: <c>mvp.Actions.Observe(observer)</c> (or as a singleton
    /// <see cref="IActionObserver"/> service). Called on the thread the handler finished on (the UI thread
    /// for a WinForms dispatch), after the action is enabled again; for an asynchronous handler, when its
    /// task ends — also after the presenter was disposed. Keep it quick and passive: write to a queue
    /// rather than a database, and do not dispatch actions from it.</para>
    /// <para>Only what goes through a dispatcher is observed: ViewAction bindings and the explicit
    /// pattern's <c>ActionRequest</c>, not a view event the presenter handles directly. To log such an
    /// operation, register its handler and call <c>Dispatcher.Dispatch(key)</c> from the event.</para>
    /// <para>An exception thrown by the observer is logged and ignored; it never changes the handler's
    /// outcome, and that execution is simply not recorded. If the record must not be lost (an audit log),
    /// handle the failure of your own writing inside the observer (buffer and retry, fall back to a local
    /// file). The payload is the handler's own object: copy what you keep, and leave out personal data
    /// the log must not hold.</para>
    /// </remarks>
    public interface IActionObserver
    {
        /// <summary>Called once for each handler that ran, when it has finished (succeeded, failed or was cancelled).</summary>
        /// <param name="execution">What ran, with what, for how long and how it ended.</param>
        void OnExecuted(ActionExecution execution);
    }
}
