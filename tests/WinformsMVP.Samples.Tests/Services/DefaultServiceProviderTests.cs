using System;
using System.Threading.Tasks;
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

            Assert.Same(foo, sp.Resolve<IFoo>());
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
            Assert.Same(second, sp.Resolve<IFoo>());
        }

        [Fact]
        public void RegisterFactory_ResolvesLazily_AndCachesSingleton()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterFactory<IFoo>(_ => { calls++; return new Foo(); });

            Assert.Equal(0, calls);                 // not built until resolved
            var a = sp.Resolve<IFoo>();
            var b = sp.Resolve<IFoo>();
            Assert.Same(a, b);                      // cached
            Assert.Equal(1, calls);                 // factory ran once
        }

        [Fact]
        public void RegisterFactory_ReceivesProvider_ForDependencyResolution()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterInstance<IFoo>(new Foo());
            sp.RegisterFactory<string>(provider => provider.Resolve<IFoo>() != null ? "ok" : "missing");

            Assert.Equal("ok", sp.Resolve<string>());
        }

        [Fact]
        public void RegisterFactory_NullFactory_Throws()
        {
            var sp = new DefaultServiceProvider();
            Assert.Throws<ArgumentNullException>(() => sp.RegisterFactory<IFoo>(null));
        }

        [Fact]
        public void ReRegisterInstance_AfterResolve_ReplacesCachedSingleton()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterFactory<IFoo>(_ => new Foo());
            var cached = sp.Resolve<IFoo>();   // builds and caches the factory result

            var replacement = new Foo();
            sp.RegisterInstance<IFoo>(replacement);

            var resolved = sp.Resolve<IFoo>();
            Assert.Same(replacement, resolved);   // the new registration wins
            Assert.NotSame(cached, resolved);
        }

        [Fact]
        public void RegisterFactory_ThatThrows_PropagatesAndStaysRetryable()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterFactory<IFoo>(_ =>
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("boom");
                return new Foo();
            });

            // First resolution: the factory throws, and the exception propagates.
            Assert.Throws<InvalidOperationException>(() => sp.Resolve<IFoo>());

            // The entry was not marked built, so a later resolution re-runs the factory.
            var resolved = sp.Resolve<IFoo>();
            Assert.NotNull(resolved);
            Assert.Equal(2, calls);
        }

        [Fact]
        public void ConcurrentResolve_RunsFactoryExactlyOnce()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterFactory<IFoo>(_ =>
            {
                System.Threading.Interlocked.Increment(ref calls);
                System.Threading.Thread.SpinWait(1000); // tiny bit of work to widen the race window
                return new Foo();
            });

            var results = new IFoo[100];
            Parallel.For(0, 100, i => results[i] = sp.Resolve<IFoo>());

            Assert.Equal(1, calls);                // factory ran exactly once
            var first = results[0];
            Assert.NotNull(first);
            foreach (var r in results)
                Assert.Same(first, r);             // all callers got the same singleton
        }
    }
}
