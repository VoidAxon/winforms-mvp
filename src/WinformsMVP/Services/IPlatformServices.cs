using WinformsMVP.Logging;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Interface for platform services.
    /// Provides infrastructure services such as messages, dialogs, logging, and file operations.
    /// </summary>
    public interface IPlatformServices
    {
        /// <summary>
        /// Dialog provider (file open, save, folder selection, etc.)
        /// </summary>
        IDialogProvider DialogProvider { get; }

        /// <summary>
        /// Message service (message boxes, toast notifications, etc.)
        /// </summary>
        IMessageService MessageService { get; }

        /// <summary>
        /// File service (file read/write, directory operations, etc.)
        /// </summary>
        IFileService FileService { get; }

        /// <summary>
        /// Window navigator (modal/non-modal window display)
        /// </summary>
        IWindowNavigator WindowNavigator { get; }

        /// <summary>
        /// Logger factory for creating loggers.
        /// Defaults to <see cref="NullLoggerFactory.Instance"/> when not configured.
        /// Bridge Microsoft.Extensions.Logging or other ecosystems via the
        /// <c>WinformsMVP.Logging.MicrosoftExtensions</c> adapter package.
        /// </summary>
        ILoggerFactory LoggerFactory { get; }
    }
}
