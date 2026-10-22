using System;
using System.Diagnostics;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Simple logger that writes to <see cref="Trace.WriteLine(string)"/> (the Visual Studio Output
    /// window or any trace listener; works in Release builds), with no logging package needed.
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

            Trace.WriteLine("[" + level + "] [" + _category + "] " + formatted);
            if (exception != null) Trace.WriteLine(exception);
        }
    }
}
