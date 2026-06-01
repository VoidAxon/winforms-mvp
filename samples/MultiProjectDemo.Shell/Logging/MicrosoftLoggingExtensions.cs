using Ms = Microsoft.Extensions.Logging;
using Fx = WinformsMVP.Logging;

namespace MultiProjectDemo.Shell.Logging
{
    /// <summary>
    /// Sugar for the composition root: wraps a Microsoft.Extensions.Logging factory
    /// so it satisfies the framework's <see cref="Fx.ILoggerFactory"/> contract.
    /// </summary>
    public static class MicrosoftLoggingExtensions
    {
        public static Fx.ILoggerFactory AsFrameworkLoggerFactory(this Ms.ILoggerFactory factory)
            => new MicrosoftLoggerFactoryAdapter(factory);
    }
}
