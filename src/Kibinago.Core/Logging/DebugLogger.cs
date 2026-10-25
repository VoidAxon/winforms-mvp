using System;
using System.Diagnostics;

namespace Kibinago.Logging
{
    /// <summary>
    /// Simple logger that writes to <see cref="Trace.WriteLine(string)"/> (the Visual Studio Output
    /// window or any trace listener; works in Release builds), with no logging package needed.
    /// </summary>
    public sealed class DebugLogger : ILogger
    {
        private readonly string _category;

        /// <summary>Creates a logger whose records are tagged with <paramref name="category"/>.</summary>
        /// <param name="category">The category name; null is treated as empty.</param>
        public DebugLogger(string category)
        {
            _category = category ?? string.Empty;
        }

        /// <inheritdoc />
        /// <remarks>Every level except <see cref="LogLevel.None"/> is enabled.</remarks>
        public bool IsEnabled(LogLevel level) => level != LogLevel.None;

        /// <inheritdoc />
        public void Log(LogLevel level, Exception exception, string message, params object[] args)
        {
            if (!IsEnabled(level)) return;

            var formatted = MessageFormatter.Format(message, args);

            Trace.WriteLine("[" + level + "] [" + _category + "] " + formatted);
            if (exception != null) Trace.WriteLine(exception);
        }
    }
}
