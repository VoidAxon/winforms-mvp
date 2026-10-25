namespace Kibinago.Common
{
    /// <summary>
    /// How an interaction (a window, a dialog, a failable operation) ended; see <see cref="InteractionResult"/>.
    /// </summary>
    public enum InteractionStatus
    {
        /// <summary>Completed successfully; a typed result carries its value.</summary>
        Ok,
        /// <summary>Cancelled by the user (or closed without a result).</summary>
        Cancel,
        /// <summary>Failed; the result carries an error message and/or exception.</summary>
        Error
    }
}
