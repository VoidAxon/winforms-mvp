using System;

namespace Kibinago.Logging
{
    /// <summary>
    /// Creates <see cref="DebugLogger"/> instances, which write through <see cref="System.Diagnostics.Trace"/>.
    /// </summary>
    public sealed class DebugLoggerFactory : ILoggerFactory
    {
        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new DebugLogger(categoryName);
    }
}
