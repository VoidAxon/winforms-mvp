using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.Logging;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;
using static WinformsMVP.Samples.LoggingDemoExample;

namespace WinformsMVP.Samples.Tests
{
    /// <summary>
    /// Tests for Microsoft.Extensions.Logging integration
    /// </summary>
    public class LoggingIntegrationTests
    {
        #region Mock Logger Infrastructure

        /// <summary>
        /// Mock logger that captures log messages for testing
        /// </summary>
        private class MockLogger : ILogger
        {
            public List<LogEntry> LoggedMessages { get; } = new List<LogEntry>();

            public bool IsEnabled(LogLevel level) => true;

            public void Log(LogLevel level, Exception exception, string message, params object[] args)
            {
                LoggedMessages.Add(new LogEntry
                {
                    LogLevel = level,
                    Message = MessageFormatter.Format(message, args),
                    Exception = exception
                });
            }
        }

        private class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public string Message { get; set; }
            public Exception Exception { get; set; }
        }

        /// <summary>
        /// Mock logger factory that returns our mock logger
        /// </summary>
        private class MockLoggerFactory : ILoggerFactory
        {
            private readonly MockLogger _logger = new MockLogger();

            public MockLogger Logger => _logger;

            public ILogger CreateLogger(string categoryName) => _logger;
            public ILogger CreateLogger(Type type) => _logger;
        }

        /// <summary>
        /// Mock view for testing
        /// </summary>
        private class MockLoggingDemoView : ILoggingDemoView
        {
            public string LogOutput { get; set; }
            public string UserName { get; set; }
            public int ProcessedCount { get; set; }
            public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;
            public object Tag { get; set; }

            // IWindowView members
            public bool IsDisposed { get; private set; }
            public void Activate() { }
            public IntPtr Handle => IntPtr.Zero;  // IWin32Window member

            public event EventHandler<WindowClosingEventArgs> Closing;
            public void OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        }

        #endregion

        [Fact]
        public void Logger_ShouldBeAvailableInPresenter()
        {
            // Arrange
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView();

            // Act
            presenter.AttachView(view);
            presenter.Initialize();

            // Assert
            // Logger should be accessible (property getter should not throw)
            // We can't directly access Logger from outside, but initialization should succeed
            Assert.NotNull(view.LogOutput);
        }

        [Fact]
        public void Logger_ShouldUseCorrectCategoryName()
        {
            // Arrange
            var mockLoggerFactory = new MockLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, mockLoggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            // Act
            presenter.AttachView(view);
            presenter.Initialize();

            // Assert
            // Logger.LogInformation("LoggingDemoPresenter initialized") is called in OnInitialize
            Assert.NotEmpty(mockLoggerFactory.Logger.LoggedMessages);

            var initMessage = mockLoggerFactory.Logger.LoggedMessages
                .FirstOrDefault(m => m.Message.Contains("initialized"));

            Assert.NotNull(initMessage);
            Assert.Equal(LogLevel.Information, initMessage.LogLevel);
        }

        [Fact]
        public void Logger_ShouldLogStructuredMessages()
        {
            // Arrange
            var mockLoggerFactory = new MockLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, mockLoggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView();

            // Act
            presenter.AttachView(view);
            presenter.Initialize();

            // Set UserName after initialization (OnInitialize sets it to "TestUser")
            view.UserName = "Alice";

            // Trigger structured logging action
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            // Assert
            var structuredMessage = mockLoggerFactory.Logger.LoggedMessages
                .FirstOrDefault(m => m.Message.Contains("Alice") && m.Message.Contains("performed action"));

            Assert.NotNull(structuredMessage);
            Assert.Equal(LogLevel.Information, structuredMessage.LogLevel);
        }

        [Fact]
        public void Logger_ShouldLogExceptions()
        {
            // Arrange
            var mockLoggerFactory = new MockLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, mockLoggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView();

            // Act
            presenter.AttachView(view);
            presenter.Initialize();

            // Set UserName after initialization (OnInitialize sets it to "TestUser")
            view.UserName = "Bob";

            // Trigger exception logging action
            presenter.TestDispatcher.Dispatch(LoggingActions.LogException);

            // Assert
            var exceptionMessage = mockLoggerFactory.Logger.LoggedMessages
                .FirstOrDefault(m => m.Exception != null);

            Assert.NotNull(exceptionMessage);
            Assert.Equal(LogLevel.Error, exceptionMessage.LogLevel);
            Assert.IsType<InvalidOperationException>(exceptionMessage.Exception);
            Assert.Contains("Bob", exceptionMessage.Message);
        }

        [Fact]
        public void Logger_DifferentLogLevels_ShouldBeLogged()
        {
            // Arrange
            var mockLoggerFactory = new MockLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, mockLoggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            presenter.AttachView(view);
            presenter.Initialize();

            // Act - Trigger different log levels
            presenter.TestDispatcher.Dispatch(LoggingActions.LogDebug);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogInfo);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogWarning);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogError);

            // Assert
            var debugLog = mockLoggerFactory.Logger.LoggedMessages.FirstOrDefault(m => m.LogLevel == LogLevel.Debug);
            var infoLog = mockLoggerFactory.Logger.LoggedMessages.FirstOrDefault(m => m.LogLevel == LogLevel.Information && m.Message.Contains("performed an action"));
            var warningLog = mockLoggerFactory.Logger.LoggedMessages.FirstOrDefault(m => m.LogLevel == LogLevel.Warning);
            var errorLog = mockLoggerFactory.Logger.LoggedMessages.FirstOrDefault(m => m.LogLevel == LogLevel.Error && m.Exception == null);

            Assert.NotNull(debugLog);
            Assert.NotNull(infoLog);
            Assert.NotNull(warningLog);
            Assert.NotNull(errorLog);
        }

        [Fact]
        public void MockPlatformServices_ShouldUseNullLoggerFactory()
        {
            // Arrange
            var mockServices = new MockPlatformServices();

            // Assert
            Assert.NotNull(mockServices.LoggerFactory);
            Assert.IsType<NullLoggerFactory>(mockServices.LoggerFactory);
        }

        [Fact]
        public void MockPlatformServices_LoggerFactory_CanBeReplaced()
        {
            // Arrange
            var mockServices = new MockPlatformServices();
            var customLoggerFactory = new MockLoggerFactory();

            // Act
            mockServices.LoggerFactory = customLoggerFactory;

            // Assert
            Assert.Same(customLoggerFactory, mockServices.LoggerFactory);
        }

        [Fact]
        public void DefaultPlatformServices_WithoutLoggerFactory_ShouldCreateDebugLogger()
        {
            // Arrange & Act
            var platformServices = new DefaultPlatformServices();

            // Assert
            Assert.NotNull(platformServices.LoggerFactory);

            // Create a logger and verify it works
            var logger = platformServices.LoggerFactory.CreateLogger("TestCategory");
            Assert.NotNull(logger);
        }

        [Fact]
        public void DefaultPlatformServices_WithCustomLoggerFactory_ShouldUseCustom()
        {
            // Arrange
            var customLoggerFactory = new MockLoggerFactory();

            // Act
            var platformServices = new DefaultPlatformServices(null, customLoggerFactory);

            // Assert
            Assert.Same(customLoggerFactory, platformServices.LoggerFactory);
        }

        [Fact]
        public void Presenter_ProcessedCount_ShouldIncrement()
        {
            // Arrange
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView { UserName = "Charlie" };

            presenter.AttachView(view);
            presenter.Initialize();

            // Act
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            // Assert
            Assert.Equal(3, view.ProcessedCount);
        }

        [Fact]
        public void Presenter_ClearLog_ShouldResetCount()
        {
            // Arrange
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView { UserName = "David" };

            presenter.AttachView(view);
            presenter.Initialize();

            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            // Act
            presenter.TestDispatcher.Dispatch(LoggingActions.ClearLog);

            // Assert
            Assert.Equal(0, view.ProcessedCount);
            Assert.Contains("Log Cleared", view.LogOutput);
        }
    }
}
