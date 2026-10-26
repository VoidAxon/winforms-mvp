namespace Kibinago.Services
{
    /// <summary>
    /// The kind of a toast notification; selects its styling.
    /// </summary>
    public enum ToastType
    {
        /// <summary>Neutral information.</summary>
        Info,
        /// <summary>An operation succeeded.</summary>
        Success,
        /// <summary>Something needs the user's attention.</summary>
        Warning,
        /// <summary>An operation failed.</summary>
        Error
    }
}
