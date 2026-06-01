using System;
using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;

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
        /// To bridge Microsoft.Extensions.Logging or other ecosystems, write a small
        /// adapter at the composition root (see <c>MultiProjectDemo.Shell/Logging/</c>
        /// in the sample solution).
        /// </summary>
        ILoggerFactory LoggerFactory { get; }

        /// <summary>
        /// Optional global configuration hook applied to every presenter's
        /// <see cref="ViewActionDispatcher"/> on first access. Use this to register
        /// application-wide middleware (audit, authorization, telemetry) that must
        /// run for every dispatch in every presenter.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Global middleware is added <b>before</b> any local middleware registered by
        /// the presenter itself in <c>RegisterViewActions</c>, so the global step always
        /// wraps (is "outer to") the local steps. Cross-cutting policies that must not
        /// be bypassed (audit logging, authorization) belong here.
        /// </para>
        /// <para>
        /// Returns <c>null</c> when no global configuration is desired (the default).
        /// </para>
        /// </remarks>
        Action<ViewActionDispatcher> ConfigureDispatcher { get; }
    }
}
