using System;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Minimal logging abstraction the framework depends on. The host application can
    /// implement this directly, use a built-in implementation (NullLogger / DebugLogger),
    /// or bridge Microsoft.Extensions.Logging via the
    /// <c>WinformsMVP.Logging.MicrosoftExtensions</c> adapter package.
    /// </summary>
    public interface ILogger
    {
        bool IsEnabled(LogLevel level);

        /// <summary>
        /// Emits a single log record. <paramref name="message"/> uses
        /// <see cref="string.Format(string, object[])"/> placeholder syntax (e.g. "{0}").
        /// Implementations may capture <paramref name="args"/> as structured data or just
        /// format them into a string.
        /// </summary>
        void Log(LogLevel level, Exception exception, string message, params object[] args);
    }
}
