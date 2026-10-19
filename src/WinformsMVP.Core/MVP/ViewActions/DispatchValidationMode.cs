namespace WinformsMVP.MVP.ViewActions
{
    /// <summary>
    /// Controls how <see cref="ViewActionDispatcher.Dispatch"/> reacts to dispatch-time
    /// <i>misconfigurations</i> — dispatching an action key that has no registered handler,
    /// or passing a payload whose type does not match the registered handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This setting only affects those two precondition failures. It does <b>not</b> change how
    /// handler or <c>CanExecute</c> exceptions are handled (those are always caught and logged so
    /// one faulty handler cannot crash the UI), and it does <b>not</b> turn a disabled action
    /// (<c>CanExecute</c> returning <c>false</c>) into an error.
    /// </para>
    /// <para>
    /// The intended use is to enable <see cref="Strict"/> in Debug builds (via
    /// <see cref="WinformsMVP.Services.IDispatcherConfigurer"/>) so that a forgotten <c>Register</c> call or a
    /// mistyped <see cref="ViewAction"/> key surfaces loudly the first time it is dispatched,
    /// instead of silently doing nothing. Production builds keep the default <see cref="Lenient"/>
    /// behaviour (graceful degradation: log and ignore).
    /// </para>
    /// </remarks>
    public enum DispatchValidationMode
    {
        /// <summary>
        /// Default. Dispatching an unregistered key or a mismatched payload is logged and ignored
        /// (the handler simply does not run). Robust for production.
        /// </summary>
        Lenient = 0,

        /// <summary>
        /// Dispatching an unregistered key or a mismatched payload throws
        /// <see cref="System.InvalidOperationException"/>. Intended for Debug builds to catch
        /// wiring mistakes (forgotten registration, typo in the key, wrong payload type) early.
        /// </summary>
        Strict = 1,
    }
}
