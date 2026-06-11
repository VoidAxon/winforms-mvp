using System;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    /// <summary>
    /// Extension methods for testing presenters with a mock service provider.
    /// Provides a fluent API for injecting a provider before presenter initialization.
    /// </summary>
    public static class PresenterServiceProviderExtensions
    {
        /// <summary>Injects a service provider for testing. Call before AttachView()/Initialize().</summary>
        /// <typeparam name="T">The presenter type.</typeparam>
        /// <param name="presenter">The presenter instance.</param>
        /// <param name="provider">The service provider to inject.</param>
        /// <returns>The presenter for method chaining.</returns>
        public static T WithServiceProvider<T>(this T presenter, IServiceProvider provider) where T : class
        {
            dynamic dynamicPresenter = presenter;
            dynamicPresenter.SetServiceProvider(provider);  // internal, reachable via InternalsVisibleTo + dynamic
            return presenter;
        }
    }
}
