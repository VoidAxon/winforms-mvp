using System;
using Kibinago.Services;
using Xunit;

namespace Kibinago.Samples.Tests.Services
{
    [CollectionDefinition("ServiceLocator", DisableParallelization = true)]
    public class ServiceLocatorCollection { }

    [Collection("ServiceLocator")] // serialize: ServiceLocator.Current is process-global
    public class ServiceLocatorTests : IDisposable
    {
        public interface IFoo { }
        private sealed class Foo : IFoo { }

        public ServiceLocatorTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        [Fact]
        public void Current_DefaultsToEmptyProvider_NotNull()
        {
            Assert.NotNull(ServiceLocator.Current);
            Assert.Null(ServiceLocator.Current.Resolve<IFoo>()); // empty by default in this phase
        }

        [Fact]
        public void Current_CanBeReplaced()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterInstance<IFoo>(new Foo());
            ServiceLocator.Current = sp;
            Assert.NotNull(ServiceLocator.Current.Resolve<IFoo>());
        }

        [Fact]
        public void Configure_RegistersIntoFreshProvider()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IFoo>(new Foo()));
            Assert.NotNull(ServiceLocator.Current.Resolve<IFoo>());
        }

        [Fact]
        public void Reset_RestoresEmptyDefault()
        {
            ServiceLocator.Configure(reg => reg.RegisterInstance<IFoo>(new Foo()));
            ServiceLocator.Reset();
            Assert.Null(ServiceLocator.Current.Resolve<IFoo>());
        }
    
        [Fact]
        public void Configure_AfterLazyDefaultWasCreated_DisposesItAndInstallsNew()
        {
            var lazyDefault = (DefaultServiceProvider)ServiceLocator.Current;   // framework-created
            var probe = new DefaultServiceProviderTests.DisposableProbe();
            lazyDefault.RegisterFactory<DefaultServiceProviderTests.IDisposableProbe>(_ => probe);
            lazyDefault.Resolve<DefaultServiceProviderTests.IDisposableProbe>();

            ServiceLocator.Configure(_ => { });

            Assert.True(probe.Disposed);
            Assert.NotSame(lazyDefault, ServiceLocator.Current);
        }

        [Fact]
        public void Configure_Twice_DisposesThePreviouslyConfiguredRoot()
        {
            ServiceLocator.Configure(_ => { });
            var first = (DefaultServiceProvider)ServiceLocator.Current;
            var probe = new DefaultServiceProviderTests.DisposableProbe();
            first.RegisterFactory<DefaultServiceProviderTests.IDisposableProbe>(_ => probe);
            first.Resolve<DefaultServiceProviderTests.IDisposableProbe>();

            ServiceLocator.Configure(_ => { });

            Assert.True(probe.Disposed);   // Configure built that root itself, so it owns it
        }

        [Fact]
        public void Setter_DoesNotDisposePreviousRoot()
        {
            var a = new DefaultServiceProvider();
            var probe = new DefaultServiceProviderTests.DisposableProbe();
            a.RegisterFactory<DefaultServiceProviderTests.IDisposableProbe>(_ => probe);
            a.Resolve<DefaultServiceProviderTests.IDisposableProbe>();

            ServiceLocator.Current = a;
            ServiceLocator.Current = new DefaultServiceProvider();

            Assert.False(probe.Disposed);   // an assigned root belongs to whoever assigned it
        }

        [Fact]
        public void Reset_DisposesLazyDefaultRoot()
        {
            var lazyDefault = (DefaultServiceProvider)ServiceLocator.Current;
            var probe = new DefaultServiceProviderTests.DisposableProbe();
            lazyDefault.RegisterFactory<DefaultServiceProviderTests.IDisposableProbe>(_ => probe);
            lazyDefault.Resolve<DefaultServiceProviderTests.IDisposableProbe>();

            ServiceLocator.Reset();

            Assert.True(probe.Disposed);
        }
}
}
