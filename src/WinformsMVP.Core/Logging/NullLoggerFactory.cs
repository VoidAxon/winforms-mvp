using System;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Factory that always returns <see cref="NullLogger.Instance"/>.
    /// </summary>
    public sealed class NullLoggerFactory : ILoggerFactory
    {
        public static readonly NullLoggerFactory Instance = new NullLoggerFactory();

        private NullLoggerFactory() { }

        public ILogger CreateLogger(string categoryName) => NullLogger.Instance;
        public ILogger CreateLogger(Type type) => NullLogger.Instance;
    }
}
