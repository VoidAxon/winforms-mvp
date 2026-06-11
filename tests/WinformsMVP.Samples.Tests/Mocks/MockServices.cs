using WinformsMVP.Logging;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Test fixture that builds a service provider populated with mock framework services and
    /// exposes the concrete mocks for verification. Replaces the former MockPlatformServices.
    /// </summary>
    public class MockServices
    {
        public MockMessageService MessageService { get; } = new MockMessageService();
        public MockDialogProvider DialogProvider { get; } = new MockDialogProvider();
        public MockFileService FileService { get; } = new MockFileService();
        public MockWindowNavigator WindowNavigator { get; } = new MockWindowNavigator();
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        /// <summary>The provider to inject via <c>presenter.WithServiceProvider(mock.Provider)</c>.</summary>
        public DefaultServiceProvider Provider { get; }

        public MockServices()
        {
            Provider = new DefaultServiceProvider();
            Provider.RegisterInstance<IMessageService>(MessageService);
            Provider.RegisterInstance<IDialogProvider>(DialogProvider);
            Provider.RegisterInstance<IFileService>(FileService);
            Provider.RegisterInstance<IWindowNavigator>(WindowNavigator);
            Provider.RegisterInstance<ILoggerFactory>(LoggerFactory);
        }

        /// <summary>Resets recorded calls on the message and file service mocks.</summary>
        public void Reset() { MessageService.Clear(); FileService.Clear(); }
    }
}
