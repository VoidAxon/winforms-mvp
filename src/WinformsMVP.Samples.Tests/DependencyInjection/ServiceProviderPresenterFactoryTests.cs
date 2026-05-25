using System;
using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.DependencyInjection;
using WinformsMVP.MVP.Presenters;
using Xunit;

namespace WinformsMVP.Samples.Tests.DependencyInjection
{
    /// <summary>
    /// Tests for <see cref="ServiceProviderPresenterFactory"/> — the bridge that lets a
    /// parent Presenter ask for a child Presenter by type without referencing any
    /// specific DI container.
    /// </summary>
    public class ServiceProviderPresenterFactoryTests
    {
        #region Test doubles

        public interface ITestDependency
        {
            string Tag { get; }
        }

        public class TestDependency : ITestDependency
        {
            public string Tag => "injected";
        }

        public class SimplePresenter : IPresenter
        {
            public Type ViewInterfaceType => typeof(object);
            public void Dispose() { }
        }

        public class PresenterWithDependency : IPresenter
        {
            public ITestDependency Dependency { get; }
            public PresenterWithDependency(ITestDependency dependency)
            {
                Dependency = dependency;
            }
            public Type ViewInterfaceType => typeof(object);
            public void Dispose() { }
        }

        public class UnregisteredPresenter : IPresenter
        {
            public Type ViewInterfaceType => typeof(object);
            public void Dispose() { }
        }

        #endregion

        [Fact]
        public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ServiceProviderPresenterFactory(null));
        }

        [Fact]
        public void Create_WhenPresenterRegistered_ReturnsResolvedInstance()
        {
            var services = new ServiceCollection();
            services.AddTransient<SimplePresenter>();
            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderPresenterFactory(provider);

            var presenter = factory.Create<SimplePresenter>();

            Assert.NotNull(presenter);
            Assert.IsType<SimplePresenter>(presenter);
        }

        [Fact]
        public void Create_WhenConstructorDependenciesPresent_InjectsThem()
        {
            // The whole point of going through the DI container: constructor params
            // are resolved automatically.
            var services = new ServiceCollection();
            services.AddSingleton<ITestDependency, TestDependency>();
            services.AddTransient<PresenterWithDependency>();
            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderPresenterFactory(provider);

            var presenter = factory.Create<PresenterWithDependency>();

            Assert.NotNull(presenter.Dependency);
            Assert.Equal("injected", presenter.Dependency.Tag);
        }

        [Fact]
        public void Create_WhenPresenterNotRegistered_ThrowsInvalidOperationException()
        {
            // GetRequiredService throws InvalidOperationException for unknown types;
            // we preserve that behaviour so failures are obvious instead of returning null.
            var services = new ServiceCollection();
            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderPresenterFactory(provider);

            Assert.Throws<InvalidOperationException>(
                () => factory.Create<UnregisteredPresenter>());
        }

        [Fact]
        public void Create_TransientLifetime_ReturnsNewInstanceEachCall()
        {
            // Sanity check: Presenters are typically transient; each modal open should
            // get its own instance.
            var services = new ServiceCollection();
            services.AddTransient<SimplePresenter>();
            var provider = services.BuildServiceProvider();
            var factory = new ServiceProviderPresenterFactory(provider);

            var first = factory.Create<SimplePresenter>();
            var second = factory.Create<SimplePresenter>();

            Assert.NotSame(first, second);
        }
    }
}
