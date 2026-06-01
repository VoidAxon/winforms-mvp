using System;
using System.Collections.Generic;
using WinformsMVP.Logging;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    /// <summary>
    /// In-memory <see cref="ILogger"/> for tests that need to assert on log output.
    /// Records every log call into <see cref="Entries"/> with level, formatted message, and exception.
    /// </summary>
    public class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public bool IsEnabled(LogLevel level) => true;

        public void Log(LogLevel level, Exception exception, string message, params object[] args)
        {
            Entries.Add(new LogEntry
            {
                Level = level,
                Message = MessageFormatter.Format(message, args),
                Exception = exception
            });
        }
    }

    /// <summary>
    /// Factory that hands out the same <see cref="CapturingLogger"/> regardless of category name.
    /// Lets callers inspect a single log stream across multiple components.
    /// </summary>
    public class CapturingLoggerFactory : ILoggerFactory
    {
        public CapturingLogger Logger { get; } = new CapturingLogger();

        public ILogger CreateLogger(string categoryName) => Logger;
        public ILogger CreateLogger(Type type) => Logger;
    }

    public class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }
}
