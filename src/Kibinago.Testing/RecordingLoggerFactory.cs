using System;
using System.Collections.Generic;
using Kibinago.Logging;

namespace Kibinago.Testing
{
    /// <summary>One recorded log call.</summary>
    public sealed class LogEntry
    {
        /// <summary>Creates a record.</summary>
        public LogEntry(string category, LogLevel level, Exception exception, string message, string template, IReadOnlyList<object> args)
        {
            Category = category;
            Level = level;
            Exception = exception;
            Message = message;
            Template = template;
            Args = args;
        }

        /// <summary>The logger's category (the type's full name for <c>CreateLogger(Type)</c>).</summary>
        public string Category { get; }

        /// <summary>The level.</summary>
        public LogLevel Level { get; }

        /// <summary>The exception passed, or null.</summary>
        public Exception Exception { get; }

        /// <summary>The message with its placeholders filled in.</summary>
        public string Message { get; }

        /// <summary>The message template as passed.</summary>
        public string Template { get; }

        /// <summary>A copy of the arguments as passed.</summary>
        public IReadOnlyList<object> Args { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Level} [{Category}] {Message}";
    }

    /// <summary>
    /// <see cref="ILoggerFactory"/> whose loggers record every entry (all levels enabled) into one
    /// shared history, for asserting what a presenter or the framework logged.
    /// </summary>
    public class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly object _gate = new object();
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        /// <summary>Every entry so far, in order (a snapshot).</summary>
        public IReadOnlyList<LogEntry> Entries
        {
            get { lock (_gate) return _entries.ToArray(); }
        }

        /// <summary>Forgets the recorded entries.</summary>
        public void Clear()
        {
            lock (_gate) _entries.Clear();
        }

        /// <inheritdoc />
        public virtual ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

        private void Add(LogEntry entry)
        {
            lock (_gate) _entries.Add(entry);
        }

        private sealed class RecordingLogger : ILogger
        {
            private readonly RecordingLoggerFactory _owner;
            private readonly string _category;

            public RecordingLogger(RecordingLoggerFactory owner, string category)
            {
                _owner = owner;
                _category = category;
            }

            public bool IsEnabled(LogLevel level) => level != LogLevel.None;

            public void Log(LogLevel level, Exception exception, string message, params object[] args)
            {
                var copy = args == null ? new object[0] : (object[])args.Clone();
                _owner.Add(new LogEntry(_category, level, exception, MessageFormatter.Format(message, copy), message, copy));
            }
        }
    }
}
