using System;
using System.Threading.Tasks;
using Kibinago.Services;
using Xunit;

namespace Kibinago.Samples.Tests.Services
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
            sp.RegisterSingleton<IFoo>(_ => { calls++; return new Foo(); });

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
            sp.RegisterSingleton<string>(provider => provider.Resolve<IFoo>() != null ? "ok" : "missing");

            Assert.Equal("ok", sp.Resolve<string>());
        }

        [Fact]
        public void RegisterFactory_NullFactory_Throws()
        {
            var sp = new DefaultServiceProvider();
            Assert.Throws<ArgumentNullException>(() => sp.RegisterSingleton<IFoo>(null));
        }

        [Fact]
        public void ReRegisterInstance_AfterResolve_ReplacesCachedSingleton()
        {
            var sp = new DefaultServiceProvider();
            sp.RegisterSingleton<IFoo>(_ => new Foo());
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
            sp.RegisterSingleton<IFoo>(_ =>
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
            sp.RegisterSingleton<IFoo>(_ =>
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
    
        public interface IDisposableProbe { bool Disposed { get; } }
        internal sealed class DisposableProbe : IDisposableProbe, IDisposable
        {
            public bool Disposed { get; private set; }
            public int DisposeCount { get; private set; }
            public void Dispose() { Disposed = true; DisposeCount++; }
        }

        [Fact]
        public void Dispose_DisposesFactoryCreatedSingletons()
        {
            var sp = new DefaultServiceProvider();
            var probe = new DisposableProbe();
            sp.RegisterSingleton<IDisposableProbe>(_ => probe);
            sp.Resolve<IDisposableProbe>();

            sp.Dispose();

            Assert.True(probe.Disposed);
        }

        [Fact]
        public void Dispose_DoesNotDisposeRegisteredInstances()
        {
            var sp = new DefaultServiceProvider();
            var probe = new DisposableProbe();
            sp.RegisterInstance<IDisposableProbe>(probe);
            sp.Resolve<IDisposableProbe>();

            sp.Dispose();

            Assert.False(probe.Disposed);   // the caller owns what it passed in
        }

        [Fact]
        public void Dispose_SkipsFactoriesNeverResolved()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterSingleton<IDisposableProbe>(_ => { calls++; return new DisposableProbe(); });

            sp.Dispose();

            Assert.Equal(0, calls);
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            var sp = new DefaultServiceProvider();
            var probe = new DisposableProbe();
            sp.RegisterSingleton<IDisposableProbe>(_ => probe);
            sp.Resolve<IDisposableProbe>();

            sp.Dispose();
            sp.Dispose();

            Assert.Equal(1, probe.DisposeCount);
        }

        [Fact]
        public void Dispose_DoesNotDisposeCallerInstance_AfterItsRegistrationWasReplaced()
        {
            var sp = new DefaultServiceProvider();
            var callerOwned = new DisposableProbe();
            sp.RegisterInstance<IDisposableProbe>(callerOwned);
            sp.RegisterSingleton<object>(p => p.GetService(typeof(IDisposableProbe)));   // alias
            sp.Resolve<object>();
            sp.RegisterInstance<IDisposableProbe>(new DisposableProbe());              // replaced

            sp.Dispose();

            Assert.False(callerOwned.Disposed);
        }

        private sealed class ThrowingDisposable : IDisposable
        {
            public void Dispose() => throw new InvalidOperationException("boom");
        }

        [Fact]
        public void Dispose_DisposesEverySingleton_EvenWhenOneThrows_ThenRethrows()
        {
            var sp = new DefaultServiceProvider();
            var probe = new DisposableProbe();
            sp.RegisterSingleton<IDisposableProbe>(_ => probe);
            sp.RegisterSingleton<IDisposable>(_ => new ThrowingDisposable());
            sp.Resolve<IDisposableProbe>();
            sp.Resolve<IDisposable>();   // created last, so disposed first

            var ex = Assert.Throws<InvalidOperationException>(() => sp.Dispose());

            Assert.Equal("boom", ex.Message);
            Assert.True(probe.Disposed);
        }

        [Fact]
        public void GetService_AfterDispose_Throws()
        {
            var sp = new DefaultServiceProvider();
            sp.Dispose();

            Assert.Throws<ObjectDisposedException>(() => sp.GetService(typeof(IFoo)));
        }

        [Fact]
        public void NonGenericRegister_ResolvesByType()
        {
            var sp = new DefaultServiceProvider();
            var foo = new Foo();
            sp.Register(typeof(IFoo), foo);

            Assert.Same(foo, sp.GetService(typeof(IFoo)));
        }

        [Fact]
        public void NonGenericRegisterFactory_IsLazySingleton()
        {
            var sp = new DefaultServiceProvider();
            int calls = 0;
            sp.RegisterSingleton(typeof(IFoo), _ => { calls++; return new Foo(); });

            Assert.Equal(0, calls);
            Assert.Same(sp.GetService(typeof(IFoo)), sp.GetService(typeof(IFoo)));
            Assert.Equal(1, calls);
        }
}
}
