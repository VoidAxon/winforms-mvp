using System;
using Ms = Microsoft.Extensions.Logging;
using Fx = WinformsMVP.Logging;

namespace WinformsMVP.Logging.MicrosoftExtensions
{
    /// <summary>
    /// Adapts a <see cref="Ms.ILoggerFactory"/> so it can be passed to
    /// <c>DefaultPlatformServices</c> as a <see cref="Fx.ILoggerFactory"/>.
    /// </summary>
    public sealed class MicrosoftLoggerFactoryAdapter : Fx.ILoggerFactory
    {
        private readonly Ms.ILoggerFactory _inner;

        public MicrosoftLoggerFactoryAdapter(Ms.ILoggerFactory inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Fx.ILogger CreateLogger(string categoryName)
            => new MicrosoftLoggerAdapter(_inner.CreateLogger(categoryName));

        public Fx.ILogger CreateLogger(Type type)
            => new MicrosoftLoggerAdapter(_inner.CreateLogger(type?.FullName ?? string.Empty));
    }
}
