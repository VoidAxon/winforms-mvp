using System;
using System.Collections.Generic;
using WinformsMVP.Logging;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Smoke tests for <see cref="DefaultPlatformServices"/> — particularly the new
    /// three-argument constructor that accepts an <see cref="IServiceProvider"/>.
    /// The decoration behaviour itself is covered exhaustively in
    /// <see cref="ServiceProviderViewMappingRegisterTests"/>; the goal here is to
    /// verify that the platform services class wires the pieces together correctly
    /// and that null arguments fall back to the documented defaults.
    /// </summary>
    public class DefaultPlatformServicesTests
    {
        private class EmptyServiceProvider : IServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }

        [Fact]
        public void Constructor_TwoArg_BackwardCompatible_DoesNotThrow()
        {
            var registry = new ViewMappingRegister();

            var platform = new DefaultPlatformServices(registry, loggerFactory: null);

            Assert.NotNull(platform.WindowNavigator);
            Assert.Same(NullLoggerFactory.Instance, platform.LoggerFactory);
        }

        [Fact]
        public void Constructor_ThreeArg_WithProvider_AllServicesAvailable()
        {
            var registry = new ViewMappingRegister();
            var provider = new EmptyServiceProvider();

            var platform = new DefaultPlatformServices(registry, null, provider);

            Assert.NotNull(platform.DialogProvider);
            Assert.NotNull(platform.MessageService);
            Assert.NotNull(platform.FileService);
            Assert.NotNull(platform.WindowNavigator);
            Assert.NotNull(platform.LoggerFactory);
        }

        [Fact]
        public void Constructor_ThreeArg_NullProvider_BehavesLikeTwoArg()
        {
            // Passing serviceProvider: null is the explicit way to say "no DI fallback".
            // The Lazy-initialized WindowNavigator must still come up fine.
            var registry = new ViewMappingRegister();

            var platform = new DefaultPlatformServices(registry, null, serviceProvider: null);

            Assert.NotNull(platform.WindowNavigator);
        }

        [Fact]
        public void Constructor_ThreeArg_NullRegistry_CreatesDefaultRegistry()
        {
            // Documented behaviour: passing null for the registry creates a fresh one.
            var provider = new EmptyServiceProvider();

            var platform = new DefaultPlatformServices(
                viewMappingRegister: null,
                loggerFactory: null,
                serviceProvider: provider);

            Assert.NotNull(platform.WindowNavigator);
        }
    }
}
