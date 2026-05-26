using System;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Sink that discards all log records. Used as the default when no factory is
    /// configured, and as a safe non-null fallback throughout the framework.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        public bool IsEnabled(LogLevel level) => false;

        public void Log(LogLevel level, Exception exception, string message, params object[] args)
        {
            // intentionally empty
        }
    }
}
