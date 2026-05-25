using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    /// <summary>
    /// In-memory <see cref="ILogger"/> for tests that need to assert on log output.
    /// Records every log call into <see cref="Entries"/> with level, formatted message, and exception.
    /// </summary>
    public class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            Entries.Add(new LogEntry
            {
                Level = logLevel,
                Message = formatter != null ? formatter(state, exception) : state?.ToString(),
                Exception = exception
            });
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Factory that hands out the same <see cref="CapturingLogger"/> regardless of category name.
    /// Lets callers inspect a single log stream across multiple components.
    /// </summary>
    public class CapturingLoggerFactory : ILoggerFactory
    {
        public CapturingLogger Logger { get; } = new CapturingLogger();

        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => Logger;
        public void Dispose() { }
    }

    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
