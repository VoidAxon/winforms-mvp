using System;

namespace Kibinago.Logging
{
    /// <summary>
    /// Sink that discards all log records. Used as the default when no factory is
    /// configured, and as a safe non-null fallback throughout the framework.
    /// </summary>
    public sealed class NullLogger : ILogger
    {
        /// <summary>The shared instance.</summary>
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        /// <inheritdoc />
        /// <remarks>Always false.</remarks>
        public bool IsEnabled(LogLevel level) => false;

        /// <inheritdoc />
        public void Log(LogLevel level, Exception exception, string message, params object[] args)
        {
            // intentionally empty
        }
    }
}
