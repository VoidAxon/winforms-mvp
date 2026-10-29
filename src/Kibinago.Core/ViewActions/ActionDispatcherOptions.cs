namespace Kibinago.ViewActions
{
    /// <summary>
    /// Settings every presenter's dispatcher takes when it is attached (<c>mvp.Actions</c>). Register one
    /// instance; without it the defaults apply.
    /// </summary>
    public sealed class ActionDispatcherOptions
    {
        /// <summary>
        /// Whether wiring mistakes (unregistered key, wrong payload type) are logged and ignored
        /// (<see cref="DispatchValidationMode.Lenient"/>, the default) or throw (<see cref="DispatchValidationMode.Strict"/>,
        /// typically in Debug builds: <c>mvp.Actions.StrictValidation()</c>).
        /// </summary>
        public DispatchValidationMode ValidationMode { get; set; } = DispatchValidationMode.Lenient;

        internal ActionDispatcherOptions Copy() => new ActionDispatcherOptions { ValidationMode = ValidationMode };
    }
}
