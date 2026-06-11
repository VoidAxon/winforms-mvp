using System;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Creates <see cref="ILogger"/> instances scoped by category. Hosts typically
    /// register one factory at startup and register it in the service provider.
    /// </summary>
    public interface ILoggerFactory
    {
        ILogger CreateLogger(string categoryName);
        ILogger CreateLogger(Type type);
    }
}
