using System;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    public class DefaultServiceProviderTests
    {
        public interface IFoo { }
        private sealed class Foo : IFoo { }

        [Fact]
        public void RegisterInstance_ThenResolve_ReturnsSameInstance()
        {
            var sp = new DefaultServiceProvider();
            var foo = new Foo();
            sp.RegisterInstance<IFoo>(foo);

            Assert.Same(foo, sp.GetService<IFoo>());
        }

        [Fact]
        public void Resolve_Unregistered_ReturnsNull()
        {
            var sp = new DefaultServiceProvider();
            Assert.Null(sp.GetService(typeof(IFoo)));
        }

        [Fact]
        public void IsRegistered_ReflectsRegistration()
        {
            var sp = new DefaultServiceProvider();
            Assert.False(sp.IsRegistered(typeof(IFoo)));
            sp.RegisterInstance<IFoo>(new Foo());
            Assert.True(sp.IsRegistered(typeof(IFoo)));
        }

        [Fact]
        public void RegisterInstance_LastWins()
        {
            var sp = new DefaultServiceProvider();
            var first = new Foo();
            var second = new Foo();
            sp.RegisterInstance<IFoo>(first);
            sp.RegisterInstance<IFoo>(second);
            Assert.Same(second, sp.GetService<IFoo>());
        }

        [Fact]
        public void RegisterFactory_ResolvesLazily_AndCachesSingleton()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterFactory<IFoo>(_ => { calls++; return new Foo(); });

            Assert.Equal(0, calls);                 // not built until resolved
            var a = sp.GetService<IFoo>();
            var b = sp.GetService<IFoo>();
            Assert.Same(a, b);                      // cached
            Assert.Equal(1, calls);                 // factory ran once
        }

        [Fact]
        public void RegisterFactory_ReceivesProvider_ForDependencyResolution()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterInstance<IFoo>(new Foo());
            sp.RegisterFactory<string>(provider => provider.GetService<IFoo>() != null ? "ok" : "missing");

            Assert.Equal("ok", sp.GetService<string>());
        }

        [Fact]
        public void RegisterFactory_NullFactory_Throws()
        {
            var sp = new DefaultServiceProvider();
            Assert.Throws<ArgumentNullException>(() => sp.RegisterFactory<IFoo>(null));
        }
    }
}
