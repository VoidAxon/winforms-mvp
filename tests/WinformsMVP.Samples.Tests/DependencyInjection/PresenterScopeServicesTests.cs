using System;
using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.DependencyInjection;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;

namespace WinformsMVP.Samples.Tests.DependencyInjection
{
    /// <summary>
    /// A presenter created by <see cref="ServiceProviderPresenterFactory"/> resolves its framework
    /// services (Messages, Logger, ...) from its own DI scope — the same scope as its constructor
    /// dependencies — instead of <c>ServiceLocator.Current</c>.
    /// </summary>
    public class PresenterScopeServicesTests
    {
        public interface IScopeView : IViewBase { }

        public sealed class ScopedDependency : IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        public sealed class MessagesPresenter : ControlPresenterBase<IScopeView>
        {
            public IMessageService Injected { get; }

            // The test double's view of the protected accessors (presenters keep no public methods: MVP006).
            public readonly Func<IMessageService> FrameworkMessages;
            public readonly Func<ILogger> FrameworkLogger;

            public MessagesPresenter(IMessageService injected)
            {
                Injected = injected;
                FrameworkMessages = () => Messages;
                FrameworkLogger = () => Logger;
            }

            protected override void OnViewAttached() { }
        }

        /// <summary>Uses a framework service in its constructor, before the factory can bind the scope.</summary>
        public sealed class EagerPresenter : ControlPresenterBase<IScopeView>
        {
            public EagerPresenter(ScopedDependency dependency)
            {
                Dependency = dependency;
                Resolved = Messages;   // resolved only (showing a real message box would block the test run)
            }

            public ScopedDependency Dependency { get; }
            public IMessageService Resolved { get; }
            protected override void OnViewAttached() { }
        }

        private static IPresenterFactory Factory(Action<IServiceCollection> configure)
        {
            var services = new ServiceCollection();
            services.AddScoped<IMessageService, MockMessageService>();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddTransient<MessagesPresenter>();
            configure?.Invoke(services);
            return new ServiceProviderPresenterFactory(services.BuildServiceProvider());
        }

        [Fact]
        public void FrameworkServices_ComeFromThePresentersScope()
        {
            var factory = Factory(null);

            var first = factory.Create<MessagesPresenter>();
            var second = factory.Create<MessagesPresenter>();

            Assert.Same(first.Injected, first.FrameworkMessages());   // same scope as the constructor
            Assert.Same(second.Injected, second.FrameworkMessages());
            Assert.NotSame(first.FrameworkMessages(), second.FrameworkMessages());   // one scope per presenter
        }

        [Fact]
        public void MissingFrameworkService_Throws_WithoutFallingBackToTheGlobalProvider()
        {
            var services = new ServiceCollection();
            services.AddScoped<IMessageService, MockMessageService>();
            services.AddTransient<MessagesPresenter>();   // no ILoggerFactory registered
            var factory = new ServiceProviderPresenterFactory(services.BuildServiceProvider());

            var presenter = factory.Create<MessagesPresenter>();

            Assert.Throws<InvalidOperationException>(() => presenter.FrameworkLogger());
        }

        [Fact]
        public void PresenterUsingFrameworkServicesInItsConstructor_IsRejected_AndItsScopeDisposed()
        {
            ScopedDependency dependency = null;
            var factory = Factory(s =>
            {
                s.AddScoped(_ => dependency = new ScopedDependency());
                s.AddTransient<EagerPresenter>();
            });

            var ex = Assert.Throws<InvalidOperationException>(() => factory.Create<EagerPresenter>());

            Assert.Contains("OnViewAttached", ex.Message);
            Assert.NotNull(dependency);
            Assert.True(dependency.Disposed);
        }
    }
}
