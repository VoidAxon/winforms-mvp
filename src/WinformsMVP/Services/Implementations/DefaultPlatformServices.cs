using System;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default implementation of platform services.
    /// Each service is lazy-initialized.
    /// </summary>
    /// <remarks>
    /// The framework intentionally depends only on the in-box <c>WinformsMVP.Logging</c>
    /// abstraction. When no <see cref="ILoggerFactory"/> is supplied, logging falls back to
    /// <see cref="NullLoggerFactory.Instance"/> — i.e. no output. Host applications that want
    /// concrete providers (Debug, Console, Application Insights, Seq, ...) either implement
    /// <see cref="ILogger"/> themselves or bridge Microsoft.Extensions.Logging via the
    /// <c>WinformsMVP.Logging.MicrosoftExtensions</c> adapter package.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Host opts in to the Debug provider (no extra dependencies):
    /// PlatformServices.Default = new DefaultPlatformServices(
    ///     viewMappingRegister: register,
    ///     loggerFactory: new DebugLoggerFactory());
    ///
    /// // Or bridge Microsoft.Extensions.Logging (requires the adapter package):
    /// var msFactory = LoggerFactory.Create(b => b.AddDebug());
    /// PlatformServices.Default = new DefaultPlatformServices(
    ///     viewMappingRegister: register,
    ///     loggerFactory: msFactory.AsFrameworkLoggerFactory());
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
            : this(viewMappingRegister, loggerFactory, serviceProvider: null)
        {
        }

        /// <summary>
        /// Initializes with a ViewMappingRegister, LoggerFactory, and an optional
        /// <see cref="IServiceProvider"/> that participates in View resolution.
        /// </summary>
        /// <param name="viewMappingRegister">View mapping register (creates new one if null).</param>
        /// <param name="loggerFactory">
        /// Logger factory. When <c>null</c>, falls back to <see cref="NullLoggerFactory.Instance"/>.
        /// </param>
        /// <param name="serviceProvider">
        /// Optional DI container exposed via the BCL <see cref="IServiceProvider"/> abstraction.
        /// When supplied, View interfaces unknown to <paramref name="viewMappingRegister"/> are
        /// resolved from this provider (the inner register still wins for explicit registrations).
        /// When <c>null</c>, behaviour matches the two-argument constructor.
        /// </param>
        /// <remarks>
        /// The framework itself never references a specific DI container type — only the BCL
        /// <see cref="IServiceProvider"/>. Host applications can plug in any container
        /// (Microsoft.Extensions.DependencyInjection, Autofac, etc.) by adapting it to
        /// <see cref="IServiceProvider"/>.
        /// </remarks>
        public DefaultPlatformServices(
            IViewMappingRegister viewMappingRegister,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider)
            : this(viewMappingRegister, loggerFactory, serviceProvider, configureDispatcher: null)
        {
        }

        /// <summary>
        /// Full-fidelity constructor adding an optional global
        /// <see cref="ViewActionDispatcher"/> configuration hook.
        /// </summary>
        /// <param name="viewMappingRegister">View mapping register (creates new one if null).</param>
        /// <param name="loggerFactory">
        /// Logger factory; falls back to <see cref="NullLoggerFactory.Instance"/> when null.
        /// </param>
        /// <param name="serviceProvider">
        /// Optional DI container exposed via <see cref="IServiceProvider"/>. See the
        /// three-argument constructor for the resolution contract.
        /// </param>
        /// <param name="configureDispatcher">
        /// Optional callback invoked against every presenter's
        /// <see cref="ViewActionDispatcher"/> on first access. Use this to register
        /// application-wide middleware (audit, authorization, telemetry).
        /// </param>
        public DefaultPlatformServices(
            IViewMappingRegister viewMappingRegister,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            Action<ViewActionDispatcher> configureDispatcher)
        {
            _windowNavigator = new Lazy<IWindowNavigator>(() =>
            {
                var register = viewMappingRegister ?? new ViewMappingRegister();
                if (serviceProvider != null)
                {
                    register = register.WithServiceProvider(serviceProvider);
                }
                return new WindowNavigator(register);
            });

            _loggerFactory = new Lazy<ILoggerFactory>(() => loggerFactory ?? NullLoggerFactory.Instance);
            ConfigureDispatcher = configureDispatcher;
        }

        public IDialogProvider DialogProvider => _dialogProvider.Value;
        public IMessageService MessageService => _messageService.Value;
        public IFileService FileService => _fileService.Value;
        public IWindowNavigator WindowNavigator => _windowNavigator.Value;
        public ILoggerFactory LoggerFactory => _loggerFactory.Value;
        public Action<ViewActionDispatcher> ConfigureDispatcher { get; }
    }
}
