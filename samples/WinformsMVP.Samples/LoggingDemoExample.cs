using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common.Events;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Samples
{
    /// <summary>
    /// Demonstrates the WinformsMVP.Logging integration in the framework.
    /// Shows different log levels, structured logging, and exception logging.
    /// </summary>
    public class LoggingDemoExample
    {
        // ViewAction keys
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

        // View interface
        public interface ILoggingDemoView : IWindowView
        {
            string LogOutput { get; set; }
            string UserName { get; set; }
            int ProcessedCount { get; set; }
        }

        // Presenter
        public class LoggingDemoPresenter : WindowPresenterBase<ILoggingDemoView>
        {
            // Internal accessor for testing
            internal ViewActionDispatcher TestDispatcher => Dispatcher;

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

        // View implementation (Form)
        public class LoggingDemoForm : Form, ILoggingDemoView
        {
            private ViewActionBinder _binder;
            private TextBox _txtLogOutput;
            private TextBox _txtUserName;
            private Label _lblProcessedCount;
            private Button _btnDebug;
            private Button _btnInfo;
            private Button _btnWarning;
            private Button _btnError;
            private Button _btnStructured;
            private Button _btnException;
            private Button _btnClear;

            public IViewActionBinder ActionBinder => _binder;

            public string LogOutput
            {
                get => _txtLogOutput.Text;
                set => _txtLogOutput.Text = value;
            }

            public string UserName
            {
                get => _txtUserName.Text;
                set => _txtUserName.Text = value;
            }

            public int ProcessedCount
            {
                get => int.TryParse(_lblProcessedCount.Text, out var count) ? count : 0;
                set => _lblProcessedCount.Text = value.ToString();
            }

            public LoggingDemoForm()
            {
                InitializeComponents();
                InitializeActionBindings();
            }

            private void InitializeComponents()
            {
                Text = "Logging Demo";
                Size = new System.Drawing.Size(800, 600);
                StartPosition = FormStartPosition.CenterScreen;

                // Layout panel
                var mainPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    Padding = new Padding(10)
                };

                // User input section
                var inputPanel = new Panel { Height = 60, Dock = DockStyle.Top };
                var lblUserName = new Label
                {
                    Text = "User Name:",
                    Location = new System.Drawing.Point(10, 15),
                    AutoSize = true
                };
                _txtUserName = new TextBox
                {
                    Location = new System.Drawing.Point(100, 12),
                    Width = 200
                };
                var lblCount = new Label
                {
                    Text = "Processed:",
                    Location = new System.Drawing.Point(320, 15),
                    AutoSize = true
                };
                _lblProcessedCount = new Label
                {
                    Text = "0",
                    Location = new System.Drawing.Point(400, 15),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold)
                };
                inputPanel.Controls.AddRange(new Control[] { lblUserName, _txtUserName, lblCount, _lblProcessedCount });

                // Button panel
                var buttonPanel = new FlowLayoutPanel
                {
                    Height = 40,
                    Dock = DockStyle.Top,
                    FlowDirection = FlowDirection.LeftToRight,
                    Padding = new Padding(5)
                };

                _btnDebug = new Button { Text = "Log Debug", Width = 100, Height = 30 };
                _btnInfo = new Button { Text = "Log Info", Width = 100, Height = 30 };
                _btnWarning = new Button { Text = "Log Warning", Width = 100, Height = 30 };
                _btnError = new Button { Text = "Log Error", Width = 100, Height = 30 };
                _btnStructured = new Button { Text = "Structured Log", Width = 120, Height = 30 };
                _btnException = new Button { Text = "Log Exception", Width = 120, Height = 30 };
                _btnClear = new Button { Text = "Clear", Width = 80, Height = 30 };

                buttonPanel.Controls.AddRange(new Control[]
                {
                    _btnDebug, _btnInfo, _btnWarning, _btnError,
                    _btnStructured, _btnException, _btnClear
                });

                // Log output
                _txtLogOutput = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = new System.Drawing.Font("Consolas", 9),
                    ReadOnly = true
                };

                mainPanel.Controls.Add(inputPanel, 0, 0);
                mainPanel.Controls.Add(buttonPanel, 0, 1);
                mainPanel.Controls.Add(_txtLogOutput, 0, 2);

                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
                mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                Controls.Add(mainPanel);
            }

            private void InitializeActionBindings()
            {
                _binder = new ViewActionBinder();
                _binder.Add(LoggingActions.LogDebug, _btnDebug);
                _binder.Add(LoggingActions.LogInfo, _btnInfo);
                _binder.Add(LoggingActions.LogWarning, _btnWarning);
                _binder.Add(LoggingActions.LogError, _btnError);
                _binder.Add(LoggingActions.LogStructured, _btnStructured);
                _binder.Add(LoggingActions.LogException, _btnException);
                _binder.Add(LoggingActions.ClearLog, _btnClear);
            }

            private EventHandler<WindowClosingEventArgs> _closing;
            event EventHandler<WindowClosingEventArgs> IWindowView.Closing
            {
                add => _closing += value;
                remove => _closing -= value;
            }
            void IWindowView.OnClosing(WindowClosingEventArgs args) => _closing?.Invoke(this, args);
        }

        // Demo launcher
        public static void ShowDemo()
        {
            // Register view mapping
            var viewMappingRegister = new ViewMappingRegister();
            viewMappingRegister.Register<ILoggingDemoView, LoggingDemoForm>();

            // Create navigator and presenter
            var navigator = new WindowNavigator(viewMappingRegister);
            var presenter = new LoggingDemoPresenter();

            // Show the window
            navigator.ShowWindow(presenter);
        }
    }

    /// <summary>
    /// Example: Custom ILoggerFactory configuration for production scenarios.
    /// Demonstrates the in-box providers (DebugLoggerFactory). To bridge
    /// Microsoft.Extensions.Logging, write a small adapter at the composition root
    /// — see <c>MultiProjectDemo.Shell/Logging/</c> in the sample solution.
    /// </summary>
    public class CustomLoggingConfiguration
    {
        /// <summary>
        /// Creates the framework's built-in Debug logger factory. Works without any
        /// external packages and is suitable for net40+ host applications.
        /// </summary>
        public static ILoggerFactory CreateDebugLoggerFactory()
        {
            return new DebugLoggerFactory();
        }

        /// <summary>
        /// Configure platform services with the built-in Debug logger.
        /// </summary>
        public static void ConfigureWithCustomLogger()
        {
            var customLoggerFactory = CreateDebugLoggerFactory();
            var platformServices = new WinformsMVP.Services.Implementations.DefaultPlatformServices(
                viewMappingRegister: null,
                loggerFactory: customLoggerFactory);

            // Set as default for all presenters
            WinformsMVP.Services.PlatformServices.Default = platformServices;
        }
    }
}
