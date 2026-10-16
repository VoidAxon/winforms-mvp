using System;
using System.Diagnostics;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Simple logger that writes to <see cref="Debug.WriteLine(string)"/>. Useful for
    /// .NET Framework 4.0 hosts that cannot reference Microsoft.Extensions.Logging
    /// but still want visibility into framework internals while debugging.
    /// </summary>
    public sealed class DebugLogger : ILogger
    {
        private readonly string _category;

        public DebugLogger(string category)
        {
            _category = category ?? string.Empty;
        }

        public bool IsEnabled(LogLevel level) => level != LogLevel.None;

        public void Log(LogLevel level, Exception exception, string message, params object[] args)
        {
            if (!IsEnabled(level)) return;

            var formatted = MessageFormatter.Format(message, args);

            Debug.WriteLine("[" + level + "] [" + _category + "] " + formatted);
            if (exception != null) Debug.WriteLine(exception);
        }
    }
}
