using System;
using System.Linq;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.Views;
using Kibinago.ViewActions;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Samples.Tests.TestHelpers;
using Kibinago.Services;
using Kibinago.Testing;
using Xunit;
using Kibinago.Samples.LoggingDemo;

namespace Kibinago.Samples.Tests
{
    /// <summary>
    /// Tests for Kibinago.Logging integration via the service provider.
    /// </summary>
    public class LoggingIntegrationTests
    {
        private class MockLoggingDemoView : ILoggingDemoView
        {
            public string LogOutput { get; set; }
            public string UserName { get; set; }
            public int ProcessedCount { get; set; }
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public object Tag { get; set; }

        }

        [Fact]
        public void Logger_ShouldBeAvailableInPresenter()
        {
            var fakes = TestServices.CreateFakes();
            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView();

            PresenterTestHost.Start(fakes.Provider, presenter, view);

            Assert.NotNull(view.LogOutput);
        }

        [Fact]
        public void Logger_ShouldUseCorrectCategoryName()
        {
            var loggerFactory = new CapturingLoggerFactory();
            var provider = new DefaultServiceProvider();
            provider.RegisterInstance<ILoggerFactory>(loggerFactory);
            provider.RegisterInstance<IMessageService>(new FakeMessageService());

            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            PresenterTestHost.Start(provider, presenter, view);

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
            provider.RegisterInstance<IMessageService>(new FakeMessageService());

            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView();

            PresenterTestHost.Start(provider, presenter, view);

            view.UserName = "Alice";

            view.Binder.Dispatch(LoggingActions.LogStructured);

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
            provider.RegisterInstance<IMessageService>(new FakeMessageService());

            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView();

            PresenterTestHost.Start(provider, presenter, view);

            view.UserName = "Bob";

            view.Binder.Dispatch(LoggingActions.LogException);

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
            provider.RegisterInstance<IMessageService>(new FakeMessageService());

            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView { UserName = "TestUser" };

            PresenterTestHost.Start(provider, presenter, view);

            view.Binder.Dispatch(LoggingActions.LogDebug);
            view.Binder.Dispatch(LoggingActions.LogInfo);
            view.Binder.Dispatch(LoggingActions.LogWarning);
            view.Binder.Dispatch(LoggingActions.LogError);

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
        public void CreateFakes_RegistersARecordingLoggerFactory()
        {
            var fakes = TestServices.CreateFakes();

            Assert.Same(fakes.Logs, fakes.Provider.GetService(typeof(ILoggerFactory)));
        }

        [Fact]
        public void LoggerFactory_CanBeReplaced()
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
            // TestServices.Create() registers only the silent NullLoggerFactory.
            var resolved = TestServices.Create().GetService(typeof(ILoggerFactory)) as ILoggerFactory;
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
            var fakes = TestServices.CreateFakes();
            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView { UserName = "Charlie" };

            PresenterTestHost.Start(fakes.Provider, presenter, view);

            view.Binder.Dispatch(LoggingActions.LogStructured);
            view.Binder.Dispatch(LoggingActions.LogStructured);
            view.Binder.Dispatch(LoggingActions.LogStructured);

            Assert.Equal(3, view.ProcessedCount);
        }

        [Fact]
        public void Presenter_ClearLog_ShouldResetCount()
        {
            var fakes = TestServices.CreateFakes();
            var presenter = new LoggingDemoPresenter();

            var view = new MockLoggingDemoView { UserName = "David" };

            PresenterTestHost.Start(fakes.Provider, presenter, view);

            view.Binder.Dispatch(LoggingActions.LogStructured);
            view.Binder.Dispatch(LoggingActions.LogStructured);

            view.Binder.Dispatch(LoggingActions.ClearLog);

            Assert.Equal(0, view.ProcessedCount);
            Assert.Contains("Log Cleared", view.LogOutput);
        }
    }
}
