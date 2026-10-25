using System;

namespace Kibinago.Logging
{
    public sealed class DebugLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new DebugLogger(categoryName);
        public ILogger CreateLogger(Type type) => new DebugLogger(type?.FullName);
    }
}
