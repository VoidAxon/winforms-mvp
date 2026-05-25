using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Core.Views;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="ServiceProviderViewMappingRegister"/>, the decorator that
    /// adds an <see cref="IServiceProvider"/> fallback to an existing
    /// <see cref="IViewMappingRegister"/>.
    /// </summary>
    public class ServiceProviderViewMappingRegisterTests
    {
        #region Test Doubles

        public interface ITestView : IViewBase
        {
            string Marker { get; }
        }

        public class TestForm : Form, ITestView
        {
            public string Marker { get; set; } = "type-based";
        }

        public class FactoryTestForm : Form, ITestView
        {
            public string Marker { get; set; } = "factory";
        }

        public class ProviderTestForm : Form, ITestView
        {
            public string Marker { get; set; } = "provider";
        }

        public interface IUnregisteredView : IViewBase { }

        /// <summary>
        /// Minimal in-memory <see cref="IServiceProvider"/> for tests. The .NET BCL
        /// contract for <see cref="IServiceProvider.GetService(Type)"/> is "return the
        /// instance, or null if unknown" — this mirrors that exactly.
        /// </summary>
        private class StubServiceProvider : IServiceProvider
        {
            private readonly Dictionary<Type, Func<object>> _factories
                = new Dictionary<Type, Func<object>>();

            public StubServiceProvider Register<T>(Func<T> factory) where T : class
            {
                _factories[typeof(T)] = () => factory();
                return this;
            }

            public object GetService(Type serviceType)
                => _factories.TryGetValue(serviceType, out var factory) ? factory() : null;
        }

        #endregion

        #region Constructor

        [Fact]
        public void Constructor_WithNullInner_ThrowsArgumentNullException()
        {
            var provider = new StubServiceProvider();

            Assert.Throws<ArgumentNullException>(
                () => new ServiceProviderViewMappingRegister(null, provider));
        }

        [Fact]
        public void Constructor_WithNullProvider_ThrowsArgumentNullException()
        {
            var inner = new ViewMappingRegister();

            Assert.Throws<ArgumentNullException>(
                () => new ServiceProviderViewMappingRegister(inner, null));
        }

        #endregion

        #region CreateInstance — inner wins

        [Fact]
        public void CreateInstance_WhenInnerHasTypeMapping_UsesInnerWithoutCallingProvider()
        {
            var inner = new ViewMappingRegister();
            inner.Register<ITestView, TestForm>();

            var providerCalled = false;
            var provider = new StubServiceProvider().Register<ITestView>(() =>
            {
                providerCalled = true;
                return new ProviderTestForm();
            });

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            var instance = decorator.CreateInstance(typeof(ITestView));

            Assert.IsType<TestForm>(instance);
            Assert.False(providerCalled,
                "Inner-registered mappings must take precedence over the IServiceProvider fallback.");
        }

        [Fact]
        public void CreateInstance_WhenInnerHasFactoryMapping_UsesInnerFactoryWithoutCallingProvider()
        {
            var inner = new ViewMappingRegister();
            inner.Register<ITestView>(() => new FactoryTestForm());

            var providerCalled = false;
            var provider = new StubServiceProvider().Register<ITestView>(() =>
            {
                providerCalled = true;
                return new ProviderTestForm();
            });

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            var instance = decorator.CreateInstance(typeof(ITestView));

            Assert.IsType<FactoryTestForm>(instance);
            Assert.False(providerCalled);
        }

        #endregion

        #region CreateInstance — provider fallback

        [Fact]
        public void CreateInstance_WhenInnerNotRegistered_FallsBackToProvider()
        {
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider()
                .Register<ITestView>(() => new ProviderTestForm());

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            var instance = decorator.CreateInstance(typeof(ITestView));

            Assert.IsType<ProviderTestForm>(instance);
        }

        [Fact]
        public void CreateInstance_WhenNeitherRegistered_ThrowsKeyNotFoundException()
        {
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider();

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.Throws<KeyNotFoundException>(
                () => decorator.CreateInstance(typeof(IUnregisteredView)));
        }

        [Fact]
        public void CreateInstance_WhenProviderReturnsNull_ThrowsKeyNotFoundException()
        {
            // Provider exists but does not know this type — GetService returns null per BCL contract.
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider();  // no registrations

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.Throws<KeyNotFoundException>(
                () => decorator.CreateInstance(typeof(IUnregisteredView)));
        }

        #endregion

        #region Register delegation

        [Fact]
        public void Register_TypeMapping_ForwardsToInner()
        {
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider();
            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            decorator.Register<ITestView, TestForm>();

            // Inner now has the mapping; calling Create on either should produce TestForm.
            Assert.True(inner.IsRegistered(typeof(ITestView)));
            Assert.IsType<TestForm>(decorator.CreateInstance(typeof(ITestView)));
        }

        [Fact]
        public void Register_FactoryMapping_ForwardsToInner()
        {
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider();
            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            decorator.Register<ITestView>(() => new FactoryTestForm());

            Assert.True(inner.IsRegistered(typeof(ITestView)));
            Assert.IsType<FactoryTestForm>(decorator.CreateInstance(typeof(ITestView)));
        }

        #endregion

        #region WithServiceProvider extension

        [Fact]
        public void WithServiceProvider_OnRegister_ReturnsDecorator()
        {
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider()
                .Register<ITestView>(() => new ProviderTestForm());

            var decorated = inner.WithServiceProvider(provider);

            // The decorator surfaces the provider fallback even though inner is empty.
            Assert.IsType<ProviderTestForm>(decorated.CreateInstance(typeof(ITestView)));
        }

        [Fact]
        public void WithServiceProvider_WithNullRegister_ThrowsArgumentNullException()
        {
            IViewMappingRegister register = null;
            var provider = new StubServiceProvider();

            Assert.Throws<ArgumentNullException>(() => register.WithServiceProvider(provider));
        }

        [Fact]
        public void WithServiceProvider_WithNullProvider_ThrowsArgumentNullException()
        {
            var register = new ViewMappingRegister();

            Assert.Throws<ArgumentNullException>(() => register.WithServiceProvider(null));
        }

        [Fact]
        public void WithServiceProvider_PreservesSharedMappingTable_RegistrationsOnInnerVisibleViaDecorator()
        {
            // Spec: the decorator references the inner register; later Register calls on
            // inner must be visible through the decorator.
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider();
            var decorated = inner.WithServiceProvider(provider);

            // Register through inner after decoration:
            inner.Register<ITestView, TestForm>();

            Assert.True(decorated.IsRegistered(typeof(ITestView)));
            Assert.IsType<TestForm>(decorated.CreateInstance(typeof(ITestView)));
        }

        #endregion

        #region IsRegistered / GetViewImplementationType — inner only

        [Fact]
        public void IsRegistered_ReturnsTrue_WhenOnlyInnerRegistered()
        {
            var inner = new ViewMappingRegister();
            inner.Register<ITestView, TestForm>();
            var provider = new StubServiceProvider();

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.True(decorator.IsRegistered(typeof(ITestView)));
        }

        [Fact]
        public void IsRegistered_ReturnsFalse_WhenOnlyProviderCanResolve()
        {
            // Documented behaviour: IsRegistered reflects inner state only.
            // CreateInstance can still succeed via the provider fallback.
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider()
                .Register<ITestView>(() => new ProviderTestForm());

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.False(decorator.IsRegistered(typeof(ITestView)));
            // But CreateInstance still works via fallback:
            Assert.IsType<ProviderTestForm>(decorator.CreateInstance(typeof(ITestView)));
        }

        [Fact]
        public void GetViewImplementationType_ReturnsType_WhenInnerHasTypeMapping()
        {
            var inner = new ViewMappingRegister();
            inner.Register<ITestView, TestForm>();
            var provider = new StubServiceProvider();

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.Equal(typeof(TestForm), decorator.GetViewImplementationType(typeof(ITestView)));
        }

        [Fact]
        public void GetViewImplementationType_ThrowsKeyNotFoundException_WhenOnlyProviderCanResolve()
        {
            // GetViewImplementationType is a metadata query against inner. The provider
            // cannot reliably report a concrete type without instantiating, so this
            // intentionally does not consult the provider.
            var inner = new ViewMappingRegister();
            var provider = new StubServiceProvider()
                .Register<ITestView>(() => new ProviderTestForm());

            var decorator = new ServiceProviderViewMappingRegister(inner, provider);

            Assert.Throws<KeyNotFoundException>(
                () => decorator.GetViewImplementationType(typeof(ITestView)));
        }

        #endregion
    }
}
