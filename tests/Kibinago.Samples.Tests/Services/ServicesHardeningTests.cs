using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Kibinago.DependencyInjection;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Samples.Tests.Mocks;
using Kibinago.Services;
using Xunit;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// Regression tests for review findings in the DI integration, view resolution and logging.
    /// </summary>
    public class ServicesHardeningTests
    {
        #region Presenter factory scopes

        public sealed class ScopedDependency : IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        public interface IScopeView : IControlView { }

        public sealed class ScopedPresenter : ControlPresenterBase<IScopeView>
        {
            public ScopedDependency Dependency { get; }
            public ScopedPresenter(ScopedDependency dependency) => Dependency = dependency;
        }

        private static IServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedDependency>();
            services.AddTransient<ScopedPresenter>();
            services.AddSingleton<IPresenterFactory, ServiceProviderPresenterFactory>();
            return services.BuildServiceProvider();
        }

        [Fact]
        public void PresenterFactory_DisposingPresenter_DisposesItsScope()
        {
            var factory = BuildProvider().GetRequiredService<IPresenterFactory>();

            var presenter = factory.Create<ScopedPresenter>();
            presenter.Dispose();

            Assert.True(presenter.Dependency.Disposed);
        }

        [Fact]
        public void PresenterFactory_EachPresenterGetsItsOwnScope()
        {
            var factory = BuildProvider().GetRequiredService<IPresenterFactory>();

            var first = factory.Create<ScopedPresenter>();
            var second = factory.Create<ScopedPresenter>();

            Assert.NotSame(first.Dependency, second.Dependency);
            first.Dispose();
            Assert.False(second.Dependency.Disposed);
            second.Dispose();
        }

        public sealed class CustomPresenter : IPresenter
        {
            public ScopedDependency Dependency { get; }
            public CustomPresenter(ScopedDependency dependency) => Dependency = dependency;
            public Type ViewInterfaceType => typeof(IScopeView);
            public void Dispose() { }
        }

        [Fact]
        public void PresenterFactory_CustomPresenter_ResolvesFromProviderSoContainerOwnsDependencies()
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedDependency>();
            services.AddTransient<CustomPresenter>();
            services.AddSingleton<IPresenterFactory, ServiceProviderPresenterFactory>();
            var provider = services.BuildServiceProvider();

            var presenter = provider.GetRequiredService<IPresenterFactory>().Create<CustomPresenter>();
            provider.Dispose();

            Assert.True(presenter.Dependency.Disposed);
        }

        #endregion

        #region AddKibinago view registry

        [Fact]
        public void AddKibinago_WithDifferentPreRegisteredViewRegistry_Throws()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IViewMappingRegister>(new ViewMappingRegister());

            var ex = Assert.Throws<InvalidOperationException>(
                () => services.AddKibinago(new ViewMappingRegister()));
            Assert.Contains("IViewMappingRegister", ex.Message);
        }

        [Fact]
        public void AddKibinago_SameRegistryTwice_IsAllowed()
        {
            var services = new ServiceCollection();
            var registry = new ViewMappingRegister();

            services.AddKibinago(registry);
            services.AddKibinago(registry);
        }

        #endregion

        #region Provider-resolved views

        public interface IProvidedView : IWindowView { }

        private sealed class ProvidedForm : Form, IProvidedView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class SingleInstanceProvider : IServiceProvider
        {
            public readonly ProvidedForm Form = new ProvidedForm();
            public object GetService(Type serviceType) => serviceType == typeof(IProvidedView) ? Form : null;
        }

        [Fact]
        public void ViewFallback_ProviderReturnsDisposedForm_ThrowsClearError()
        {
            var provider = new SingleInstanceProvider();
            var register = new ServiceProviderViewMappingRegister(new ViewMappingRegister(), provider);
            Assert.Same(provider.Form, register.CreateInstance(typeof(IProvidedView)));

            provider.Form.Dispose();   // the first window closed

            var ex = Assert.Throws<InvalidOperationException>(() => register.CreateInstance(typeof(IProvidedView)));
            Assert.Contains("transient", ex.Message);
        }

        #endregion

        #region Logging

        [Theory]
        [InlineData("User {@User} logged in", "User alice logged in")]
        [InlineData("User {$User} logged in", "User alice logged in")]
        public void MessageFormatter_CaptureHints_AreTreatedAsNames(string template, string expected)
        {
            Assert.Equal(expected, MessageFormatter.Format(template, new object[] { "alice" }));
        }

        [Fact]
        public void MessageFormatter_RepeatedName_ReusesTheArgument()
        {
            Assert.Equal("7 then 7", MessageFormatter.Format("{Id} then {Id}", new object[] { 7 }));
        }

        private sealed class CapturingListener : TraceListener
        {
            public readonly System.Text.StringBuilder Text = new System.Text.StringBuilder();
            public override void Write(string message) => Text.Append(message);
            public override void WriteLine(string message) => Text.AppendLine(message);
        }

        [Fact]
        public void DebugLogger_WritesThroughTrace()
        {
            // Debug.WriteLine is compiled out of Release builds of the library; Trace is not.
            var listener = new CapturingListener();
            Trace.Listeners.Add(listener);
            try
            {
                new DebugLoggerFactory().CreateLogger("Cat").LogInformation("hello {Name}", "world");
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }

            Assert.Contains("hello world", listener.Text.ToString());
        }

        #endregion
    }
}
