using System;

namespace Kibinago.Logging
{
    /// <summary>
    /// Factory that always returns <see cref="NullLogger.Instance"/>.
    /// </summary>
    public sealed class NullLoggerFactory : ILoggerFactory
    {
        /// <summary>The shared instance.</summary>
        public static readonly NullLoggerFactory Instance = new NullLoggerFactory();

        private NullLoggerFactory() { }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
    }
}
