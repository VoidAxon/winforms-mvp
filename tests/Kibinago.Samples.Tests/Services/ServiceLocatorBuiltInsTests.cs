using Kibinago.Logging;
using Kibinago.ViewActions;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Services;
using Xunit;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Services
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
        }

        [Fact]
        public void Default_WinFormsDialogProvider_IsSameInstanceAsDialogProvider()
        {
            // The WinForms built-ins arrive through the Kibinago assembly's platform hook,
            // which registers one DialogProvider under both interfaces.
            var sp = ServiceLocator.Current;
            Assert.Same(sp.Resolve<IDialogProvider>(), sp.Resolve<IWinFormsDialogProvider>());
        }

        [Fact]
        public void Default_IToastService_ForwardsToTheWinFormsToastService()
        {
            var sp = ServiceLocator.Current;
            Assert.IsType<Kibinago.Services.WinFormsToastService>(sp.Resolve<IWinFormsToastService>());
            var forwarder = Assert.IsType<Kibinago.Services.ToastServiceForwarder>(sp.Resolve<IToastService>());
            Assert.Same(sp.Resolve<IWinFormsToastService>(), forwarder.Target);
        }

        [Fact]
        public void Default_HasNoDispatcherConfigurer()
        {
            Assert.Null(ServiceLocator.Current.Resolve<IDispatcherConfigurer>());
        }

        [Fact]
        public void Configure_KeepsBuiltIns_AndAllowsOverride()
        {
            var custom = new FakeMessageService();
            ServiceLocator.Configure(reg => reg.RegisterInstance<IMessageService>(custom));
            Assert.Same(custom, ServiceLocator.Current.Resolve<IMessageService>());
            Assert.NotNull(ServiceLocator.Current.Resolve<IWindowNavigator>()); // built-in still present
        }
    }
}
