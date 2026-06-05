using System;
using System.Linq;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Services.Implementations;
using Xunit;
using static WinformsMVP.Samples.LoggingDemoExample;

namespace WinformsMVP.Samples.Tests
{
    /// <summary>
    /// Tests for WinformsMVP.Logging integration via DefaultPlatformServices.
    /// </summary>
    public class LoggingIntegrationTests
    {
        private class MockLoggingDemoView : ILoggingDemoView
        {
            public string LogOutput { get; set; }
            public string UserName { get; set; }
            public int ProcessedCount { get; set; }
            public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;
            public object Tag { get; set; }

            public IntPtr Handle => IntPtr.Zero;
        }

        [Fact]
        public void Logger_ShouldBeAvailableInPresenter()
        {
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView();

            presenter.AttachView(view);
            presenter.Initialize();

            Assert.NotNull(view.LogOutput);
        }

        [Fact]
        public void Logger_ShouldUseCorrectCategoryName()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, loggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            presenter.AttachView(view);
            presenter.Initialize();

            Assert.NotEmpty(loggerFactory.Logger.Entries);

            var initMessage = loggerFactory.Logger.Entries
                .FirstOrDefault(m => m.Message.Contains("initialized"));

            Assert.NotNull(initMessage);
            Assert.Equal(LogLevel.Information, initMessage.Level);
        }

        [Fact]
        public void Logger_ShouldLogStructuredMessages()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, loggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView();

            presenter.AttachView(view);
            presenter.Initialize();

            view.UserName = "Alice";

            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            var structuredMessage = loggerFactory.Logger.Entries
                .FirstOrDefault(m => m.Message.Contains("Alice") && m.Message.Contains("performed action"));

            Assert.NotNull(structuredMessage);
            Assert.Equal(LogLevel.Information, structuredMessage.Level);
        }

        [Fact]
        public void Logger_ShouldLogExceptions()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, loggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView();

            presenter.AttachView(view);
            presenter.Initialize();

            view.UserName = "Bob";

            presenter.TestDispatcher.Dispatch(LoggingActions.LogException);

            var exceptionMessage = loggerFactory.Logger.Entries
                .FirstOrDefault(m => m.Exception != null);

            Assert.NotNull(exceptionMessage);
            Assert.Equal(LogLevel.Error, exceptionMessage.Level);
            Assert.IsType<InvalidOperationException>(exceptionMessage.Exception);
            Assert.Contains("Bob", exceptionMessage.Message);
        }

        [Fact]
        public void Logger_DifferentLogLevels_ShouldBeLogged()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var platformServices = new DefaultPlatformServices(null, loggerFactory);

            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(platformServices);

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            presenter.AttachView(view);
            presenter.Initialize();

            presenter.TestDispatcher.Dispatch(LoggingActions.LogDebug);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogInfo);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogWarning);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogError);

            var debugLog = loggerFactory.Logger.Entries.FirstOrDefault(m => m.Level == LogLevel.Debug);
            var infoLog = loggerFactory.Logger.Entries.FirstOrDefault(m => m.Level == LogLevel.Information && m.Message.Contains("performed an action"));
            var warningLog = loggerFactory.Logger.Entries.FirstOrDefault(m => m.Level == LogLevel.Warning);
            var errorLog = loggerFactory.Logger.Entries.FirstOrDefault(m => m.Level == LogLevel.Error && m.Exception == null);

            Assert.NotNull(debugLog);
            Assert.NotNull(infoLog);
            Assert.NotNull(warningLog);
            Assert.NotNull(errorLog);
        }

        [Fact]
        public void MockPlatformServices_ShouldUseNullLoggerFactory()
        {
            var mockServices = new MockPlatformServices();

            Assert.NotNull(mockServices.LoggerFactory);
            Assert.IsType<NullLoggerFactory>(mockServices.LoggerFactory);
        }

        [Fact]
        public void MockPlatformServices_LoggerFactory_CanBeReplaced()
        {
            var mockServices = new MockPlatformServices();
            var customLoggerFactory = new CapturingLoggerFactory();

            mockServices.LoggerFactory = customLoggerFactory;

            Assert.Same(customLoggerFactory, mockServices.LoggerFactory);
        }

        [Fact]
        public void DefaultPlatformServices_WithoutLoggerFactory_ShouldUseNullLoggerFactory()
        {
            var platformServices = new DefaultPlatformServices();

            Assert.NotNull(platformServices.LoggerFactory);
            Assert.Same(NullLoggerFactory.Instance, platformServices.LoggerFactory);
        }

        [Fact]
        public void DefaultPlatformServices_WithCustomLoggerFactory_ShouldUseCustom()
        {
            var customLoggerFactory = new CapturingLoggerFactory();

            var platformServices = new DefaultPlatformServices(null, customLoggerFactory);

            Assert.Same(customLoggerFactory, platformServices.LoggerFactory);
        }

        [Fact]
        public void Presenter_ProcessedCount_ShouldIncrement()
        {
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView { UserName = "Charlie" };

            presenter.AttachView(view);
            presenter.Initialize();

            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            Assert.Equal(3, view.ProcessedCount);
        }

        [Fact]
        public void Presenter_ClearLog_ShouldResetCount()
        {
            var mockServices = new MockPlatformServices();
            var presenter = new LoggingDemoPresenter();
            presenter.SetPlatformServices(mockServices);

            var view = new MockLoggingDemoView { UserName = "David" };

            presenter.AttachView(view);
            presenter.Initialize();

            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);
            presenter.TestDispatcher.Dispatch(LoggingActions.LogStructured);

            presenter.TestDispatcher.Dispatch(LoggingActions.ClearLog);

            Assert.Equal(0, view.ProcessedCount);
            Assert.Contains("Log Cleared", view.LogOutput);
        }
    }
}
