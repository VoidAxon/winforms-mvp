using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class ServiceProviderExtensionsTests
    {
        private sealed class StubProvider : IServiceProvider
        {
            private readonly object _value;
            public StubProvider(object value) { _value = value; }
            public object GetService(Type serviceType) => _value;
        }

        public interface IFoo { }
        private sealed class Foo : IFoo { }

        [Fact]
        public void GetService_Generic_CastsResult()
        {
            IServiceProvider p = new StubProvider(new Foo());
            IFoo foo = p.GetService<IFoo>();
            Assert.NotNull(foo);
        }

        [Fact]
        public void GetService_Generic_ReturnsNullWhenAbsent()
        {
            IServiceProvider p = new StubProvider(null);
            Assert.Null(p.GetService<IFoo>());
        }

        [Fact]
        public void GetRequiredService_ThrowsWhenAbsent()
        {
            IServiceProvider p = new StubProvider(null);
            Assert.Throws<InvalidOperationException>(() => p.GetRequiredService<IFoo>());
        }
    }
}
