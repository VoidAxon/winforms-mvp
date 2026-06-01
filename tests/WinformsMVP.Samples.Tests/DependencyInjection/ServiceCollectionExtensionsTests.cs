using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.DependencyInjection;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.DependencyInjection
{
    /// <summary>
    /// Tests for the <see cref="ServiceCollectionExtensions"/> entry points,
    /// <c>AddWinformsMVP</c> and <c>RegisterModules</c>.
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
            public TPresenter Create<TPresenter>() where TPresenter : WinformsMVP.MVP.Presenters.IPresenter
            {
                throw new NotImplementedException();
            }
        }

        #region AddWinformsMVP

        [Fact]
        public void AddWinformsMVP_RegistersIPresenterFactory_AsServiceProviderPresenterFactory()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            services.AddWinformsMVP(registry);

            var provider = services.BuildServiceProvider();
            var factory = provider.GetService<IPresenterFactory>();

            Assert.NotNull(factory);
            Assert.IsType<ServiceProviderPresenterFactory>(factory);
        }

        [Fact]
        public void AddWinformsMVP_RegistersViewMappingRegister_AsSpecifiedInstance()
        {
            // The very same instance must be retrievable — that's how WindowNavigator
            // and other components see the same map of registrations.
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            services.AddWinformsMVP(registry);

            var provider = services.BuildServiceProvider();
            var resolved = provider.GetService<IViewMappingRegister>();

            Assert.Same(registry, resolved);
        }

        [Fact]
        public void AddWinformsMVP_DoesNotOverridePreviousPresenterFactoryRegistration()
        {
            // TryAdd semantics: host applications must be able to plug in a custom
            // IPresenterFactory before calling AddWinformsMVP.
            var services = new ServiceCollection();
            services.AddSingleton<IPresenterFactory, CustomPresenterFactory>();
            var registry = new ViewMappingRegister();

            services.AddWinformsMVP(registry);

            var provider = services.BuildServiceProvider();
            var factory = provider.GetService<IPresenterFactory>();

            Assert.IsType<CustomPresenterFactory>(factory);
        }

        [Fact]
        public void AddWinformsMVP_ReturnsServices_ForChaining()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            var returned = services.AddWinformsMVP(registry);

            Assert.Same(services, returned);
        }

        [Fact]
        public void AddWinformsMVP_WithNullServices_Throws()
        {
            IServiceCollection services = null;
            var registry = new ViewMappingRegister();

            Assert.Throws<ArgumentNullException>(() => services.AddWinformsMVP(registry));
        }

        [Fact]
        public void AddWinformsMVP_WithNullRegister_Throws()
        {
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(() => services.AddWinformsMVP(null));
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
}
