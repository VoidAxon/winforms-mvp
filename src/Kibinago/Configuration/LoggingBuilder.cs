using System;
using Kibinago.Logging;

namespace Kibinago.Configuration
{
    /// <summary>The logger factory (<c>mvp.Logging</c>). Default: <see cref="NullLoggerFactory"/>.</summary>
    public sealed class LoggingBuilder
    {
        private readonly KibinagoBuilder _owner;

        internal LoggingBuilder(KibinagoBuilder owner) { _owner = owner; }

        /// <summary>Logs through <paramref name="loggerFactory"/> (e.g. <see cref="DebugLoggerFactory"/> or an M.E.L. adapter).</summary>
        public LoggingBuilder Use(ILoggerFactory loggerFactory)
        {
            _owner.SetInstance(typeof(ILoggerFactory), loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
            return this;
        }
    }
}
