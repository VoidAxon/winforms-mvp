using System;

namespace Kibinago.Logging
{
    /// <summary>
    /// Convenience extension methods. Names match
    /// <c>Microsoft.Extensions.Logging.LoggerExtensions</c> so call sites do not
    /// change when migrating between abstractions.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>Writes a <see cref="LogLevel.Trace"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogTrace(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Trace, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Debug"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogDebug(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Debug, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Information"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogInformation(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Information, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Warning"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogWarning(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Warning, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Error"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogError(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Error, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Error"/> record with an exception; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="exception">The exception to record.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Error, exception, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Critical"/> record; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogCritical(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Critical, null, message, args);
        }

        /// <summary>Writes a <see cref="LogLevel.Critical"/> record with an exception; does nothing when <paramref name="logger"/> is null.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="exception">The exception to record.</param>
        /// <param name="message">The message template (named or indexed placeholders).</param>
        /// <param name="args">The placeholder values.</param>
        public static void LogCritical(this ILogger logger, Exception exception, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Critical, exception, message, args);
        }
    }
}
