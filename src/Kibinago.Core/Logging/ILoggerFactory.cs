using System;

namespace Kibinago.Logging
{
    /// <summary>
    /// Creates <see cref="ILogger"/> instances scoped by category. Hosts typically
    /// register one factory at startup and register it in the service provider.
    /// </summary>
    public interface ILoggerFactory
    {
        /// <summary>Creates a logger for the given category name.</summary>
        ILogger CreateLogger(string categoryName);
        /// <summary>Creates a logger whose category is the full name of <paramref name="type"/>.</summary>
        ILogger CreateLogger(Type type);
    }
}
