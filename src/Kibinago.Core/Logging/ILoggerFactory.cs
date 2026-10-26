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
    }

    /// <summary>Helpers over <see cref="ILoggerFactory"/>.</summary>
    public static class LoggerFactoryExtensions
    {
        /// <summary>Creates a logger whose category is the full name of <paramref name="type"/>.</summary>
        /// <param name="factory">The factory.</param>
        /// <param name="type">The type whose full name is the category (usually the calling class).</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> or <paramref name="type"/> is <c>null</c>.</exception>
        public static ILogger CreateLogger(this ILoggerFactory factory, Type type)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (type == null) throw new ArgumentNullException(nameof(type));
            return factory.CreateLogger(type.FullName);
        }
    }
}
