using WinformsMVP.Logging;
using WinformsMVP.Services;

namespace WinformsMVP.Testing
{
    /// <summary>
    /// Starting point for the services of one presenter under test, used with
    /// <c>PresenterTestHost.Start(services, presenter, view)</c>.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var messages = new FakeMessageService();
    /// var services = TestServices.Create();
    /// services.RegisterInstance&lt;IMessageService&gt;(messages);
    /// using (var host = PresenterTestHost.Start(services, new OrderPresenter(repo), view)) { ... }
    /// </code>
    /// </remarks>
    public static class TestServices
    {
        /// <summary>
        /// A new provider with only a silent <see cref="ILoggerFactory"/> registered (every presenter
        /// needs one). Register the fakes the presenter uses; registering an
        /// <see cref="ILoggerFactory"/> replaces the silent one, e.g. to assert log entries.
        /// </summary>
        public static DefaultServiceProvider Create()
        {
            var services = new DefaultServiceProvider();
            services.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            return services;
        }
    }
}
