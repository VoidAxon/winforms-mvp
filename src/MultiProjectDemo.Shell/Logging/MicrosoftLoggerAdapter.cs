using System;
using Ms = Microsoft.Extensions.Logging;
using Fx = WinformsMVP.Logging;

namespace MultiProjectDemo.Shell.Logging
{
    /// <summary>
    /// Application-level adapter that bridges <see cref="Ms.ILogger"/> to the
    /// framework's <see cref="Fx.ILogger"/> contract. This is NOT framework code —
    /// the WinformsMVP framework intentionally has zero dependency on
    /// Microsoft.Extensions.Logging so it can multi-target net40/net48. A real host
    /// application that wants the M.E.L. ecosystem (Debug, Console, Application
    /// Insights, Seq, Serilog, ...) writes this ~30-line adapter once and is done.
    /// </summary>
    /// <remarks>
    /// Forwarding via <see cref="Ms.LoggerExtensions"/> preserves M.E.L.'s formatter
    /// pipeline, so structured properties, scopes, and providers configured upstream
    /// flow through unchanged.
    /// </remarks>
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
