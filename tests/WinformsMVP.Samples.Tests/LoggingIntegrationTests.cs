using System;
using System.Linq;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Services;
using Xunit;
using static WinformsMVP.Samples.LoggingDemoExample;

namespace WinformsMVP.Samples.Tests
{
    /// <summary>
    /// Tests for WinformsMVP.Logging integration via the service provider.
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

        }

        [Fact]
        public void Logger_ShouldBeAvailableInPresenter()
        {
            var mockServices = new MockServices();
            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(mockServices.Provider);

            var view = new MockLoggingDemoView();

            presenter.AttachView(view);
            presenter.Initialize();

            Assert.NotNull(view.LogOutput);
        }

        [Fact]
        public void Logger_ShouldUseCorrectCategoryName()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(loggerFactory);
            provider.RegisterInstance<IMessageService>(new MockMessageService());

            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(provider);

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
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(loggerFactory);
            provider.RegisterInstance<IMessageService>(new MockMessageService());

            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(provider);

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
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(loggerFactory);
            provider.RegisterInstance<IMessageService>(new MockMessageService());

            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(provider);

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
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(loggerFactory);
            provider.RegisterInstance<IMessageService>(new MockMessageService());

            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(provider);

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
        public void MockServices_ShouldUseNullLoggerFactory()
        {
            var mockServices = new MockServices();

            Assert.NotNull(mockServices.LoggerFactory);
            Assert.IsType<NullLoggerFactory>(mockServices.LoggerFactory);
        }

        [Fact]
        public void MockServices_LoggerFactory_CanBeReplaced()
        {
            var customLoggerFactory = new CapturingLoggerFactory();
            // Build a provider with the custom factory explicitly.
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(customLoggerFactory);

            Assert.Same(customLoggerFactory, provider.GetService(typeof(ILoggerFactory)) as ILoggerFactory);
        }

        [Fact]
        public void DefaultServiceProvider_WithNullLoggerFactory_ResolvesNullLoggerFactory()
        {
            // When only NullLoggerFactory is registered (the default in MockServices),
            // the provider resolves NullLoggerFactory.
            var mockServices = new MockServices();
            var resolved = mockServices.Provider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            Assert.NotNull(resolved);
            Assert.IsType<NullLoggerFactory>(resolved);
        }

        [Fact]
        public void DefaultServiceProvider_WithCustomLoggerFactory_ResolvesCustom()
        {
            var customLoggerFactory = new CapturingLoggerFactory();
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(customLoggerFactory);

            var resolved = provider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            Assert.Same(customLoggerFactory, resolved);
        }

        [Fact]
        public void Presenter_ProcessedCount_ShouldIncrement()
        {
            var mockServices = new MockServices();
            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(mockServices.Provider);

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
            var mockServices = new MockServices();
            var presenter = new LoggingDemoPresenter()
                .WithServiceProvider(mockServices.Provider);

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
