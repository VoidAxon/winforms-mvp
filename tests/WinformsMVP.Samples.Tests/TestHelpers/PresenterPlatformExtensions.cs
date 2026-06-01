using WinformsMVP.Core.Presenters;
using WinformsMVP.Core.Views;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.Tests.TestHelpers
{
    /// <summary>
    /// Extension methods for testing presenters with mock platform services.
    /// Provides a Fluent API for injecting mock services before presenter initialization.
    /// </summary>
    public static class PresenterPlatformExtensions
    {
        /// <summary>
        /// Injects mock platform services for testing.
        /// Must be called before AttachView() or Initialize().
        /// </summary>
        /// <typeparam name="T">The presenter type</typeparam>
        /// <param name="presenter">The presenter instance</param>
        /// <param name="platform">The mock platform services</param>
        /// <returns>The presenter for method chaining</returns>
        /// <example>
        /// <code>
        /// var mockServices = new MockPlatformServices();
        /// var presenter = new MyPresenter()
        ///     .WithPlatformServices(mockServices);  // Before initialization!
        ///
        /// presenter.AttachView(mockView);
        /// presenter.Initialize();
        /// </code>
        /// </example>
        public static T WithPlatformServices<T>(this T presenter, IPlatformServices platform)
            where T : class
        {
            // InternalsVisibleTo attribute allows access to internal methods
            // Use dynamic to call SetPlatformServices
            dynamic dynamicPresenter = presenter;
            dynamicPresenter.SetPlatformServices(platform);
            return presenter;
        }
    }
}
