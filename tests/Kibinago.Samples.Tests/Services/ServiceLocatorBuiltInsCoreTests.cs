using Kibinago.Common;
using Kibinago.Events;
using Kibinago.Services;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    // Built-ins that come from Kibinago.Core itself (present even without the WinForms package).
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

        [Fact]
        public void Default_ResolvesSingletonEventAggregator()
        {
            var first = ServiceLocator.Current.Resolve<IEventAggregator>();

            Assert.IsType<EventAggregator>(first);
            Assert.Same(first, ServiceLocator.Current.Resolve<IEventAggregator>());
        }

        [Fact]
        public void Reset_DisposesTheDefaultEventAggregator()
        {
            var aggregator = (EventAggregator)ServiceLocator.Current.Resolve<IEventAggregator>();

            ServiceLocator.Reset();

            Assert.Throws<System.ObjectDisposedException>(() => aggregator.Subscribe<string>(_ => { }));
        }
    }
}
