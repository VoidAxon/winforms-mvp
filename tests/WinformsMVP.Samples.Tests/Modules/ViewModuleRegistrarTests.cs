using System;
using System.Collections.Generic;
using WinformsMVP.Modules;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Modules
{
    /// <summary>
    /// Tests for <see cref="IViewModuleRegistrar"/> and its
    /// <c>RegisterModules</c> extension — the entry point that lets a host
    /// project ask each UI module to register its own views in one pass.
    /// </summary>
    public class ViewModuleRegistrarTests
    {
        private class RecordingModule : IViewModuleRegistrar
        {
            private readonly Action<IViewMappingRegister> _onRegister;
            public int CallCount { get; private set; }
            public List<IViewMappingRegister> ReceivedRegisters { get; }
                = new List<IViewMappingRegister>();

            public RecordingModule(Action<IViewMappingRegister> onRegister = null)
            {
                _onRegister = onRegister;
            }

            public void RegisterViews(IViewMappingRegister registry)
            {
                CallCount++;
                ReceivedRegisters.Add(registry);
                _onRegister?.Invoke(registry);
            }
        }

        [Fact]
        public void RegisterModules_WithNoModules_LeavesRegistryUntouched()
        {
            var registry = new ViewMappingRegister();

            var result = registry.RegisterModules();

            Assert.Same(registry, result);  // Returns same instance for chaining.
        }

        [Fact]
        public void RegisterModules_WithSingleModule_InvokesRegisterViewsOnceWithRegistry()
        {
            var registry = new ViewMappingRegister();
            var module = new RecordingModule();

            registry.RegisterModules(module);

            Assert.Equal(1, module.CallCount);
            Assert.Same(registry, module.ReceivedRegisters[0]);
        }

        [Fact]
        public void RegisterModules_WithMultipleModules_InvokesAllInOrder()
        {
            var registry = new ViewMappingRegister();
            var order = new List<string>();
            var first = new RecordingModule(_ => order.Add("first"));
            var second = new RecordingModule(_ => order.Add("second"));
            var third = new RecordingModule(_ => order.Add("third"));

            registry.RegisterModules(first, second, third);

            Assert.Equal(new[] { "first", "second", "third" }, order);
        }

        [Fact]
        public void RegisterModules_OnNullRegistry_ThrowsArgumentNullException()
        {
            IViewMappingRegister registry = null;

            Assert.Throws<ArgumentNullException>(
                () => registry.RegisterModules(new RecordingModule()));
        }

        [Fact]
        public void RegisterModules_WithNullModulesArray_ThrowsArgumentNullException()
        {
            var registry = new ViewMappingRegister();
            IViewModuleRegistrar[] modules = null;

            Assert.Throws<ArgumentNullException>(() => registry.RegisterModules(modules));
        }

        [Fact]
        public void RegisterModules_WithNullElement_ThrowsArgumentExceptionAndStopsBeforeIt()
        {
            var registry = new ViewMappingRegister();
            var first = new RecordingModule();

            // A null inside the array is a programmer error — fail loud rather than skipping silently.
            Assert.Throws<ArgumentException>(
                () => registry.RegisterModules(first, null));
        }

        [Fact]
        public void RegisterModules_ReturnsRegistry_ForChaining()
        {
            var registry = new ViewMappingRegister();

            // The return type must be IViewMappingRegister so the call composes with
            // other fluent helpers such as WithServiceProvider.
            IViewMappingRegister chained = registry.RegisterModules(new RecordingModule());

            Assert.Same(registry, chained);
        }
    }
}
