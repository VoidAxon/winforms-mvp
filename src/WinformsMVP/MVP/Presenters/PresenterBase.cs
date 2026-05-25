using System;
using Microsoft.Extensions.Logging;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Core.Presenters
{
    /// <summary>
    /// Base class for all presenters providing common functionality.
    /// This class should not be used directly - use WindowPresenterBase or ControlPresenterBase instead.
    /// </summary>
    public abstract class PresenterBase<TView> : IPresenter where TView : IViewBase
    {
        protected TView View { get; private set; }
        protected readonly ViewActionDispatcher _dispatcher;

        // Platform services infrastructure
        private WinformsMVP.Services.IPlatformServices _platform;
        protected bool _initialized = false;

        protected PresenterBase()
        {
            _dispatcher = new ViewActionDispatcher();
        }

        /// <summary>
        /// Sets the platform services for testing purposes.
        /// MUST be called before AttachView() or Initialize().
        /// </summary>
        /// <param name="platform">The platform services to use</param>
        /// <exception cref="ArgumentNullException">Thrown when platform is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when called after initialization</exception>
        internal void SetPlatformServices(WinformsMVP.Services.IPlatformServices platform)
        {
            if (platform == null)
                throw new ArgumentNullException(nameof(platform));

            if (_initialized)
                throw new InvalidOperationException(
                    "Cannot set platform services after presenter has been initialized. " +
                    "Call SetPlatformServices() before AttachView() or Initialize().");

            _platform = platform;
        }

        /// <summary>
        /// Gets the platform services container.
        /// Defaults to PlatformServices.Default if not explicitly set.
        /// </summary>
        protected WinformsMVP.Services.IPlatformServices Platform =>
            _platform ?? (_platform = WinformsMVP.Services.PlatformServices.Default);

        /// <summary>
        /// Convenience property for accessing IMessageService.
        /// Use this instead of Platform.MessageService for cleaner code.
        /// </summary>
        protected WinformsMVP.Services.IMessageService Messages => Platform.MessageService;

        /// <summary>
        /// Convenience property for accessing IDialogProvider.
        /// Use this instead of Platform.DialogProvider for cleaner code.
        /// </summary>
        protected WinformsMVP.Services.IDialogProvider Dialogs => Platform.DialogProvider;

        /// <summary>
        /// Convenience property for accessing IFileService.
        /// Use this instead of Platform.FileService for cleaner code.
        /// </summary>
        protected WinformsMVP.Services.IFileService Files => Platform.FileService;

        /// <summary>
        /// Convenience property for accessing IWindowNavigator.
        /// Use this instead of Platform.WindowNavigator for cleaner code.
        /// </summary>
        protected WinformsMVP.Services.IWindowNavigator Navigator => Platform.WindowNavigator;

        private ILogger _logger;

        /// <summary>
        /// Logger for this presenter.
        /// Uses ILogger&lt;PresenterType&gt; pattern for structured logging.
        /// Default implementation uses Debug provider.
        /// Configure custom providers via Platform.LoggerFactory.
        /// </summary>
        /// <example>
        /// <code>
        /// Logger.LogInformation("User {UserName} opened screen", userName);
        /// Logger.LogError(ex, "Failed to save data");
        /// </code>
        /// </example>
        protected ILogger Logger
        {
            get
            {
                if (_logger == null)
                {
                    var loggerFactory = Platform.LoggerFactory;
                    _logger = loggerFactory.CreateLogger(this.GetType());
                }
                return _logger;
            }
        }

        /// <summary>
        /// Sets the view for this presenter. Called by derived classes.
        /// </summary>
        protected void SetView(TView view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));

            View = view;
            OnViewAttached();
        }

        /// <summary>
        /// Called when the view is attached to this presenter.
        /// </summary>
        protected abstract void OnViewAttached();

        public Type ViewInterfaceType => typeof(TView);

        private bool _dispatcherLoggerWired;

        protected ViewActionDispatcher Dispatcher
        {
            get
            {
                if (!_dispatcherLoggerWired)
                {
                    _dispatcher.Logger = Logger;
                    _dispatcherLoggerWired = true;
                }
                return _dispatcher;
            }
        }

        /// <summary>
        /// Helper method to dispatch actions from view events.
        /// </summary>
        protected void OnViewActionTriggered(object sender, ActionRequestEventArgs e)
        {
            DispatchAction(e);
        }

        /// <summary>
        /// Dispatches an action request to the registered handler.
        /// </summary>
        protected void DispatchAction(ActionRequestEventArgs e)
        {
            if (e == null) return;

            var key = e.ActionKey;
            object payload = null;

            if (e is IActionRequestEventArgsWithValue valueProvider)
            {
                payload = valueProvider.GetValue();
            }

            Dispatcher.Dispatch(key, payload);
        }

        // IDisposable implementation
        private bool _isDisposed = false;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                Cleanup();
                _isDisposed = true;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Override to clean up resources when the presenter is disposed.
        /// </summary>
        protected virtual void Cleanup() { }
    }
}
