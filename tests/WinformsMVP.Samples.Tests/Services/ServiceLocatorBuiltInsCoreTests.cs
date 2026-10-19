using WinformsMVP.Common;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    // Built-ins that come from WinformsMVP.Core itself (present even without the WinForms package).
    [Collection("ServiceLocator")]
    public class ServiceLocatorBuiltInsCoreTests : System.IDisposable
    {
        public ServiceLocatorBuiltInsCoreTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void Default_RegistersChangeTrackingEngines()
        {
            Assert.Same(BuiltInObjectCloner.Instance, ServiceLocator.Current.Resolve<IObjectCloner>());
            Assert.Same(BuiltInObjectContentComparer.Instance, ServiceLocator.Current.Resolve<IObjectContentComparer>());
        }
    }
}
