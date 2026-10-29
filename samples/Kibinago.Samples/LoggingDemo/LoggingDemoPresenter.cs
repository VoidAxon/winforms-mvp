using System;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;

namespace Kibinago.Samples.LoggingDemo
{
    public static class LoggingActions
    {
        public static readonly ViewAction LogDebug = ViewAction.Create("Logging.Debug");
        public static readonly ViewAction LogInfo = ViewAction.Create("Logging.Info");
        public static readonly ViewAction LogWarning = ViewAction.Create("Logging.Warning");
        public static readonly ViewAction LogError = ViewAction.Create("Logging.Error");
        public static readonly ViewAction LogStructured = ViewAction.Create("Logging.Structured");
        public static readonly ViewAction LogException = ViewAction.Create("Logging.Exception");
        public static readonly ViewAction ClearLog = ViewAction.Create("Logging.Clear");
    }

    /// <summary>
    /// Logs through the presenter's <c>Logger</c> (<c>Kibinago.Logging</c>): levels, {Named} placeholders,
    /// exceptions. Where the entries go is decided once at startup (<c>mvp.Logging.Use(...)</c>; this
    /// sample: <c>DebugLoggerFactory</c>, i.e. the debugger's output). The view echoes each call.
    /// </summary>
    public class LoggingDemoPresenter : ControlPresenterBase<ILoggingDemoView>
    {
        protected override void OnViewAttached()
        {
            // Nothing to do - using ViewAction implicit pattern
        }

        protected override void OnInitialize()
        {
            View.LogOutput = "=== Logging Demo ===\n\n";
            View.UserName = "TestUser";
            View.ProcessedCount = 0;

            // Log initialization
            Logger.LogInformation("LoggingDemoPresenter initialized");
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(LoggingActions.LogDebug, OnLogDebug);
            Dispatcher.Register(LoggingActions.LogInfo, OnLogInfo);
            Dispatcher.Register(LoggingActions.LogWarning, OnLogWarning);
            Dispatcher.Register(LoggingActions.LogError, OnLogError);
            Dispatcher.Register(LoggingActions.LogStructured, OnLogStructured);
            Dispatcher.Register(LoggingActions.LogException, OnLogException);
            Dispatcher.Register(LoggingActions.ClearLog, OnClearLog);
        }

        private void OnLogDebug()
        {
            // Debug-level logging (only in development)
            Logger.LogDebug("This is a debug message - only visible in Debug builds");
            AppendToLog("[DEBUG] Debug message logged");
        }

        private void OnLogInfo()
        {
            // Information-level logging
            Logger.LogInformation("User performed an action");
            AppendToLog("[INFO] Information message logged");
        }

        private void OnLogWarning()
        {
            // Warning-level logging
            Logger.LogWarning("This operation might cause issues");
            AppendToLog("[WARNING] Warning message logged");
        }

        private void OnLogError()
        {
            // Error-level logging (without exception)
            Logger.LogError("An error occurred during processing");
            AppendToLog("[ERROR] Error message logged");
        }

        private void OnLogStructured()
        {
            // Structured logging with parameters
            // This allows log aggregation systems to query by specific values
            var userName = View.UserName;
            var timestamp = DateTime.Now;
            View.ProcessedCount++;

            Logger.LogInformation(
                "User {UserName} performed action at {Timestamp}. Total processed: {Count}",
                userName,
                timestamp,
                View.ProcessedCount);

            AppendToLog($"[STRUCTURED] Logged: UserName={userName}, Timestamp={timestamp:HH:mm:ss}, Count={View.ProcessedCount}");
        }

        private void OnLogException()
        {
            // Exception logging with context
            try
            {
                // Simulate an operation that throws
                throw new InvalidOperationException("Simulated error for demo purposes");
            }
            catch (Exception ex)
            {
                // Log exception with message and stack trace
                Logger.LogError(ex, "Failed to process user action for {UserName}", View.UserName);
                AppendToLog($"[EXCEPTION] {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void OnClearLog()
        {
            View.LogOutput = "=== Log Cleared ===\n\n";
            View.ProcessedCount = 0;
            Logger.LogInformation("Log display cleared");
        }

        private void AppendToLog(string message)
        {
            View.LogOutput += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        }
    }
}
