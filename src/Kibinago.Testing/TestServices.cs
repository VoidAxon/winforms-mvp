using Kibinago.Logging;
using Kibinago.Services;

namespace Kibinago.Testing
{
    /// <summary>
    /// Starting point for the services of one presenter under test, used with
    /// <c>PresenterTestHost.Start(services, presenter, view)</c>.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var fakes = TestServices.CreateFakes();                 // every framework service faked
    /// fakes.Messages.EnqueueYesNo(true);
    /// using (var host = PresenterTestHost.Start(fakes.Provider, new OrderPresenter(repo), view)) { ... }
    ///
    /// var services = TestServices.Create();                   // or only what the test registers
    /// services.RegisterInstance&lt;IMessageService&gt;(myOwnFake);
    /// </code>
    /// </remarks>
    public static class TestServices
    {
        /// <summary>
        /// A new provider with only a silent <see cref="ILoggerFactory"/> (every presenter needs one) and a
        /// busy indicator that shows nothing (<c>ShowBusy</c> is cosmetic) registered. Register the fakes the
        /// presenter uses; registering an <see cref="ILoggerFactory"/> replaces the silent one, e.g. to assert
        /// log entries, and a <see cref="FakeBusyIndicator"/> records <c>ShowBusy</c>.
        /// </summary>
        public static DefaultServiceProvider Create()
        {
            var services = new DefaultServiceProvider();
            services.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
            services.RegisterInstance<IBusyIndicator>(NullBusyIndicator.Instance);   // cosmetic: ShowBusy needs no fake
            return services;
        }

        /// <summary>
        /// <see cref="Create"/> plus a default fake for every presenter-facing framework service:
        /// <see cref="FakeMessageService"/>, <see cref="FakeToastService"/>,
        /// <see cref="FakeWindowNavigator"/>, <see cref="FakeDialogProvider"/>, <see cref="FakeBusyIndicator"/>,
        /// <see cref="RecordingActionObserver"/> and <see cref="RecordingLoggerFactory"/> (replacing the
        /// silent logger). Replace any of them with your own through <c>Provider.RegisterInstance</c>
        /// before <c>PresenterTestHost.Start</c>.
        /// </summary>
        public static FakeServices CreateFakes()
        {
            var fakes = new FakeServices(Create());
            var sp = fakes.Provider;
            sp.RegisterInstance<IMessageService>(fakes.Messages);
            sp.RegisterInstance<IToastService>(fakes.Toasts);
            sp.RegisterInstance<IWindowNavigator>(fakes.Navigator);
            sp.RegisterInstance<IDialogProvider>(fakes.Dialogs);
            sp.RegisterInstance<ILoggerFactory>(fakes.Logs);
            sp.RegisterInstance<IBusyIndicator>(fakes.Busy);
            sp.RegisterInstance<Kibinago.ViewActions.IActionObserver>(fakes.Actions);
            return fakes;
        }
    }
}
