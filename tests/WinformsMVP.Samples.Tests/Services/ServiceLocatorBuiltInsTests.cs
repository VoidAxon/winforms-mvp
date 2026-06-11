using WinformsMVP.Logging;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    [Collection("ServiceLocator")]
    public class ServiceLocatorBuiltInsTests : System.IDisposable
    {
        public ServiceLocatorBuiltInsTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void Default_ResolvesBuiltInServices()
        {
            var sp = ServiceLocator.Current;
            Assert.NotNull(sp.Resolve<IMessageService>());
            Assert.NotNull(sp.Resolve<IDialogProvider>());
            Assert.NotNull(sp.Resolve<IFileService>());
            Assert.NotNull(sp.Resolve<ILoggerFactory>());
            Assert.NotNull(sp.Resolve<IWindowNavigator>());
            Assert.NotNull(sp.Resolve<IViewMappingRegister>());
            Assert.NotNull(sp.Resolve<IAnchoredMessageService>());
        }

        [Fact]
        public void Default_HasNoDispatcherConfigurer()
        {
            Assert.Null(ServiceLocator.Current.Resolve<IDispatcherConfigurer>());
        }

        [Fact]
        public void Configure_KeepsBuiltIns_AndAllowsOverride()
        {
            var custom = new MockMessageService();
            ServiceLocator.Configure(reg => reg.RegisterInstance<IMessageService>(custom));
            Assert.Same(custom, ServiceLocator.Current.Resolve<IMessageService>());
            Assert.NotNull(ServiceLocator.Current.Resolve<IWindowNavigator>()); // built-in still present
        }
    }
}
