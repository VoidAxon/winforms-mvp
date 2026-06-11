using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
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
    }
}
