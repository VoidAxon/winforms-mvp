using System;
using Ms = Microsoft.Extensions.Logging;
using Fx = WinformsMVP.Logging;

namespace WinformsMVP.Logging.MicrosoftExtensions
{
    /// <summary>
    /// Adapts a <see cref="Ms.ILogger"/> instance so it can be consumed wherever the
    /// framework expects a <see cref="Fx.ILogger"/>. Forwarding preserves
    /// <c>Microsoft.Extensions.Logging</c>'s formatter pipeline, so structured
    /// properties, scopes, and providers all work as configured upstream.
    /// </summary>
    public sealed class MicrosoftLoggerAdapter : Fx.ILogger
    {
        private readonly Ms.ILogger _inner;

        public MicrosoftLoggerAdapter(Ms.ILogger inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool IsEnabled(Fx.LogLevel level) => _inner.IsEnabled(MapLevel(level));

        public void Log(Fx.LogLevel level, Exception exception, string message, params object[] args)
        {
            // Microsoft.Extensions.Logging's ILogger.Log<TState>(...) extension methods
            // (LogInformation, LogError, ...) ultimately call the same overload; reusing
            // the extension keeps message-template parsing and structured property
            // capture identical to native M.E.L. callers.
            var msLevel = MapLevel(level);
            if (exception == null)
            {
                Ms.LoggerExtensions.Log(_inner, msLevel, message, args);
            }
            else
            {
                Ms.LoggerExtensions.Log(_inner, msLevel, exception, message, args);
            }
        }

        internal static Ms.LogLevel MapLevel(Fx.LogLevel level)
        {
            switch (level)
            {
                case Fx.LogLevel.Trace: return Ms.LogLevel.Trace;
                case Fx.LogLevel.Debug: return Ms.LogLevel.Debug;
                case Fx.LogLevel.Information: return Ms.LogLevel.Information;
                case Fx.LogLevel.Warning: return Ms.LogLevel.Warning;
                case Fx.LogLevel.Error: return Ms.LogLevel.Error;
                case Fx.LogLevel.Critical: return Ms.LogLevel.Critical;
                case Fx.LogLevel.None: return Ms.LogLevel.None;
                default: return Ms.LogLevel.Information;
            }
        }
    }
}
