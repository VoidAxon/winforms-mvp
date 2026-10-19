using System;

namespace WinformsMVP.Logging
{
    /// <summary>
    /// Convenience extension methods. Names match
    /// <c>Microsoft.Extensions.Logging.LoggerExtensions</c> so call sites do not
    /// change when migrating between abstractions.
    /// </summary>
    public static class LoggerExtensions
    {
        public static void LogTrace(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Trace, null, message, args);
        }

        public static void LogDebug(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Debug, null, message, args);
        }

        public static void LogInformation(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Information, null, message, args);
        }

        public static void LogWarning(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Warning, null, message, args);
        }

        public static void LogError(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Error, null, message, args);
        }

        public static void LogError(this ILogger logger, Exception exception, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Error, exception, message, args);
        }

        public static void LogCritical(this ILogger logger, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Critical, null, message, args);
        }

        public static void LogCritical(this ILogger logger, Exception exception, string message, params object[] args)
        {
            if (logger == null) return;
            logger.Log(LogLevel.Critical, exception, message, args);
        }
    }
}
