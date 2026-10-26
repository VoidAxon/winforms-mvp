using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Kibinago.DependencyInjection;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Services;
using Xunit;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.DependencyInjection
{
    /// <summary>
    /// Tests for the <see cref="ServiceCollectionExtensions"/> entry points,
    /// <c>AddKibinago</c> and <c>RegisterModules</c>.
    /// </summary>
    public class ServiceCollectionExtensionsTests
    {
        private class RecordingModule : IModuleRegistrar
        {
            public int RegisterViewsCalls { get; private set; }
            public int RegisterServicesCalls { get; private set; }
            public List<IViewMappingRegister> SeenRegisters { get; } = new List<IViewMappingRegister>();
            public List<IServiceCollection> SeenServices { get; } = new List<IServiceCollection>();
            public Action<IServiceCollection> ServicesCallback { get; set; }

            public void RegisterViews(IViewMappingRegister registry)
            {
                RegisterViewsCalls++;
                SeenRegisters.Add(registry);
            }

            public void RegisterServices(IServiceCollection services)
            {
                RegisterServicesCalls++;
                SeenServices.Add(services);
                ServicesCallback?.Invoke(services);
            }
        }

        private interface IFakeService { }
        private class FakeService : IFakeService { }

        private interface IAnotherFakeService { }
        private class AnotherFakeService : IAnotherFakeService { }

        private class CustomPresenterFactory : IPresenterFactory
        {
            public TPresenter Create<TPresenter>() where TPresenter : Kibinago.Presenters.IPresenter
            {
                throw new NotImplementedException();
            }
        }

        #region AddKibinago

        [Fact]
        public void AddKibinago_RegistersIPresenterFactory_AsServiceProviderPresenterFactory()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            services.AddKibinago(registry);

            var provider = services.BuildServiceProvider();
            var factory = provider.GetService<IPresenterFactory>();

            Assert.NotNull(factory);
            Assert.IsType<ServiceProviderPresenterFactory>(factory);
        }

        [Fact]
        public void AddKibinago_RegistersChangeTrackingEngines()
        {
            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister());

            var provider = services.BuildServiceProvider();

            Assert.Same(Kibinago.Common.BuiltInObjectCloner.Instance, provider.GetService<Kibinago.Common.IObjectCloner>());
            Assert.Same(Kibinago.Common.BuiltInObjectContentComparer.Instance, provider.GetService<Kibinago.Common.IObjectContentComparer>());
        }

        [Fact]
        public void AddKibinago_RegistersSingletonEventAggregator()
        {
            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister());

            var provider = services.BuildServiceProvider();
            var first = provider.GetService<Kibinago.Events.IEventAggregator>();

            Assert.IsType<Kibinago.Events.EventAggregator>(first);
            Assert.Same(first, provider.GetService<Kibinago.Events.IEventAggregator>());
        }

        [Fact]
        public void AddKibinago_IToastService_ForwardsToTheWinFormsToastService()
        {
            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister());

            using var provider = services.BuildServiceProvider();

            Assert.IsType<WinFormsToastService>(provider.GetService<IWinFormsToastService>());
            var forwarder = Assert.IsType<ToastServiceForwarder>(provider.GetService<IToastService>());
            Assert.Same(provider.GetService<IWinFormsToastService>(), forwarder.Target);
        }

        [Fact]
        public void AddKibinago_RegistersViewMappingRegister_AsSpecifiedInstance()
        {
            // The very same instance must be retrievable — that's how WindowNavigator
            // and other components see the same map of registrations.
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            services.AddKibinago(registry);

            var provider = services.BuildServiceProvider();
            var resolved = provider.GetService<IViewMappingRegister>();

            Assert.Same(registry, resolved);
        }

        [Fact]
        public void AddKibinago_DoesNotOverridePreviousPresenterFactoryRegistration()
        {
            // TryAdd semantics: host applications must be able to plug in a custom
            // IPresenterFactory before calling AddKibinago.
            var services = new ServiceCollection();
            services.AddSingleton<IPresenterFactory, CustomPresenterFactory>();
            var registry = new ViewMappingRegister();

            services.AddKibinago(registry);

            var provider = services.BuildServiceProvider();
            var factory = provider.GetService<IPresenterFactory>();

            Assert.IsType<CustomPresenterFactory>(factory);
        }

        [Fact]
        public void AddKibinago_ReturnsServices_ForChaining()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            var returned = services.AddKibinago(registry);

            Assert.Same(services, returned);
        }

        [Fact]
        public void AddKibinago_WithNullServices_Throws()
        {
            IServiceCollection services = null;
            var registry = new ViewMappingRegister();

            Assert.Throws<ArgumentNullException>(() => services.AddKibinago(registry));
        }

        [Fact]
        public void AddKibinago_WithNullRegister_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(() => services.AddKibinago(null));
        }

        #endregion

        #region RegisterModules

        [Fact]
        public void RegisterModules_InvokesBothRegisterViewsAndRegisterServices_OnEveryModule()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();
            var module = new RecordingModule();

            services.RegisterModules(registry, module);

            Assert.Equal(1, module.RegisterViewsCalls);
            Assert.Equal(1, module.RegisterServicesCalls);
        }

        [Fact]
        public void RegisterModules_PassesSameRegistryAndServicesToEveryModule()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();
            var first = new RecordingModule();
            var second = new RecordingModule();

            services.RegisterModules(registry, first, second);

            Assert.Same(registry, first.SeenRegisters[0]);
            Assert.Same(services, first.SeenServices[0]);
            Assert.Same(registry, second.SeenRegisters[0]);
            Assert.Same(services, second.SeenServices[0]);
        }

        [Fact]
        public void RegisterModules_MultipleModules_ServicesAreCumulative()
        {
            // Each module adds its own services; the final container should contain
            // every service registered by every module.
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();
            var first = new RecordingModule
            {
                ServicesCallback = s => s.AddSingleton<IFakeService, FakeService>(),
            };
            var second = new RecordingModule
            {
                ServicesCallback = s => s.AddSingleton<IAnotherFakeService, AnotherFakeService>(),
            };

            services.RegisterModules(registry, first, second);
            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IFakeService>());
            Assert.NotNull(provider.GetService<IAnotherFakeService>());
        }

        [Fact]
        public void RegisterModules_ReturnsServices_ForChaining()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            var returned = services.RegisterModules(registry, new RecordingModule());

            Assert.Same(services, returned);
        }

        [Fact]
        public void RegisterModules_WithNullServices_Throws()
        {
            IServiceCollection services = null;
            var registry = new ViewMappingRegister();

            Assert.Throws<ArgumentNullException>(
                () => services.RegisterModules(registry, new RecordingModule()));
        }

        [Fact]
        public void RegisterModules_WithNullRegistry_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(
                () => services.RegisterModules(null, new RecordingModule()));
        }

        [Fact]
        public void RegisterModules_WithNullModulesArray_Throws()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();
            IModuleRegistrar[] modules = null;

            Assert.Throws<ArgumentNullException>(
                () => services.RegisterModules(registry, modules));
        }

        [Fact]
        public void RegisterModules_WithNullElement_Throws()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            Assert.Throws<ArgumentException>(
                () => services.RegisterModules(registry, new RecordingModule(), null));
        }

        #endregion
    }

    /// <summary>
    /// Tests for the framework built-in service registrations added by
    /// <see cref="ServiceCollectionExtensions.AddKibinago"/> and the
    /// <see cref="ServiceCollectionExtensions.UseKibinago"/> locator hand-off.
    /// Serialised via [Collection("ServiceLocator")] because they mutate the
    /// process-global <see cref="ServiceLocator.Current"/>.
    /// </summary>
    [Collection("ServiceLocator")]
    public class AddKibinagoBuiltInsTests : IDisposable
    {
        public AddKibinagoBuiltInsTests() => ServiceLocator.Reset();
        public void Dispose() => ServiceLocator.Reset();

        private static IServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddKibinago(new ViewMappingRegister());
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddKibinago_Provider_ResolvesIMessageService()
        {
            var provider = BuildProvider();
            Assert.NotNull(provider.GetService<IMessageService>());
        }

        [Fact]
        public void AddKibinago_Provider_ResolvesIWindowNavigator()
        {
            var provider = BuildProvider();
            Assert.NotNull(provider.GetService<IWindowNavigator>());
        }

        [Fact]
        public void AddKibinago_Provider_ResolvesIDialogProvider()
        {
            var provider = BuildProvider();
            Assert.NotNull(provider.GetService<IDialogProvider>());
        }

        [Fact]
        public void AddKibinago_Provider_ResolvesILoggerFactory()
        {
            var provider = BuildProvider();
            Assert.NotNull(provider.GetService<Kibinago.Logging.ILoggerFactory>());
        }

        [Fact]
        public void UseKibinago_SetsServiceLocatorCurrent()
        {
            var provider = BuildProvider();

            provider.UseKibinago();

            Assert.Same(provider, ServiceLocator.Current);
        }

        [Fact]
        public void UseKibinago_ReturnsProvider_ForChaining()
        {
            var provider = BuildProvider();

            var returned = provider.UseKibinago();

            Assert.Same(provider, returned);
        }

        [Fact]
        public void UseKibinago_WithNullProvider_Throws()
        {
            IServiceProvider provider = null;
            Assert.Throws<ArgumentNullException>(() => provider.UseKibinago());
        }

        [Fact]
        public void AddKibinago_DoesNotOverrideHostIMessageService_RegisteredBefore()
        {
            // TryAdd semantics: a host that registers IMessageService before calling
            // AddKibinago must keep its own implementation (host-override precedence).
            var services = new ServiceCollection();
            var customMessageService = new FakeMessageService();
            services.AddSingleton<IMessageService>(customMessageService);
            var registry = new ViewMappingRegister();

            services.AddKibinago(registry);

            var provider = services.BuildServiceProvider();
            var resolved = provider.GetService<IMessageService>();

            Assert.Same(customMessageService, resolved);
        }

    }
}
