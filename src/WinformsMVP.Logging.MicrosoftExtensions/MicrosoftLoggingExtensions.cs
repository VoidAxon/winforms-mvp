using Ms = Microsoft.Extensions.Logging;
using Fx = WinformsMVP.Logging;

namespace WinformsMVP.Logging.MicrosoftExtensions
{
    public static class MicrosoftLoggingExtensions
    {
        /// <summary>
        /// Wraps a Microsoft.Extensions.Logging factory so it satisfies the
        /// framework's <see cref="Fx.ILoggerFactory"/> contract. Use this at the
        /// composition root when configuring <c>DefaultPlatformServices</c>.
        /// </summary>
        public static Fx.ILoggerFactory AsFrameworkLoggerFactory(this Ms.ILoggerFactory factory)
            => new MicrosoftLoggerFactoryAdapter(factory);
    }
}
