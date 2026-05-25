using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default implementation of platform services.
    /// Each service is lazy-initialized.
    /// </summary>
    /// <remarks>
    /// The framework intentionally depends only on <c>Microsoft.Extensions.Logging.Abstractions</c>.
    /// When no <see cref="ILoggerFactory"/> is supplied, logging falls back to
    /// <see cref="NullLoggerFactory.Instance"/> — i.e. no output. Host applications that want
    /// concrete providers (Debug, Console, Application Insights, Seq, ...) must reference the
    /// corresponding package and pass an <see cref="ILoggerFactory"/> explicitly.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Host (e.g. Program.cs) opts in to Debug provider:
    /// // requires PackageReference: Microsoft.Extensions.Logging + Microsoft.Extensions.Logging.Debug
    /// var loggerFactory = LoggerFactory.Create(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));
    /// PlatformServices.Default = new DefaultPlatformServices(
    ///     viewMappingRegister: register,
    ///     loggerFactory: loggerFactory);
    /// </code>
    /// </example>
    public class DefaultPlatformServices : IPlatformServices
    {
        private readonly Lazy<IDialogProvider> _dialogProvider = new Lazy<IDialogProvider>(() => new DialogProvider());
        private readonly Lazy<IMessageService> _messageService = new Lazy<IMessageService>(() => new MessageService());
        private readonly Lazy<IFileService> _fileService = new Lazy<IFileService>(() => new FileService());
        private readonly Lazy<IWindowNavigator> _windowNavigator;
        private readonly Lazy<ILoggerFactory> _loggerFactory;

        /// <summary>
        /// Default constructor. Logging defaults to <see cref="NullLoggerFactory.Instance"/>
        /// (no output); inject a configured <see cref="ILoggerFactory"/> to enable logging.
        /// </summary>
        public DefaultPlatformServices() : this(null, null)
        {
        }

        /// <summary>
        /// Initializes with a specified ViewMappingRegister. Logging defaults to
        /// <see cref="NullLoggerFactory.Instance"/>.
        /// </summary>
        /// <param name="viewMappingRegister">View mapping register (creates new one if null)</param>
        public DefaultPlatformServices(IViewMappingRegister viewMappingRegister)
            : this(viewMappingRegister, null)
        {
        }

        /// <summary>
        /// Initializes with specified ViewMappingRegister and LoggerFactory.
        /// </summary>
        /// <param name="viewMappingRegister">View mapping register (creates new one if null)</param>
        /// <param name="loggerFactory">
        /// Logger factory. When <c>null</c>, falls back to <see cref="NullLoggerFactory.Instance"/>
        /// (no output). Pass a configured factory from the host application to enable logging.
        /// </param>
        public DefaultPlatformServices(
            IViewMappingRegister viewMappingRegister,
            ILoggerFactory loggerFactory)
        {
            _windowNavigator = new Lazy<IWindowNavigator>(() =>
            {
                var register = viewMappingRegister ?? new ViewMappingRegister();
                return new WindowNavigator(register);
            });

            _loggerFactory = new Lazy<ILoggerFactory>(() => loggerFactory ?? NullLoggerFactory.Instance);
        }

        public IDialogProvider DialogProvider => _dialogProvider.Value;
        public IMessageService MessageService => _messageService.Value;
        public IFileService FileService => _fileService.Value;
        public IWindowNavigator WindowNavigator => _windowNavigator.Value;
        public ILoggerFactory LoggerFactory => _loggerFactory.Value;
    }
}
