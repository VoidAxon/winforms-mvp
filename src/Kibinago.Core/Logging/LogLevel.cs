namespace Kibinago.Logging
{
    /// <summary>
    /// Log severity levels. Values match Microsoft.Extensions.Logging.LogLevel so a
    /// thin application-level adapter can map between the two by cast.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Most detailed messages, for tracing a flow step by step.</summary>
        Trace = 0,
        /// <summary>Diagnostic messages useful during development.</summary>
        Debug = 1,
        /// <summary>The general flow of the application.</summary>
        Information = 2,
        /// <summary>An unexpected situation the application recovered from.</summary>
        Warning = 3,
        /// <summary>A failure of the current operation.</summary>
        Error = 4,
        /// <summary>A failure that needs immediate attention (e.g. data loss, the application cannot continue).</summary>
        Critical = 5,
        /// <summary>Not a severity: as a minimum level, turns logging off.</summary>
        None = 6,
    }
}
