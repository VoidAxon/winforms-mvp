using WinformsMVP.Services;

namespace WinformsMVP.Testing
{
    /// <summary>
    /// The provider and default fakes created by <see cref="TestServices.CreateFakes"/>. The
    /// properties are the instances registered in <see cref="Provider"/>; to use your own
    /// implementation of a service, register it in <see cref="Provider"/> before
    /// <c>PresenterTestHost.Start</c> (presenters resolve services once) — the property then still
    /// returns the unused default fake.
    /// </summary>
    public sealed class FakeServices
    {
        internal FakeServices(DefaultServiceProvider provider)
        {
            Provider = provider;
        }

        /// <summary>The provider to pass to <c>PresenterTestHost.Start(services, ...)</c>; owned by the test.</summary>
        public DefaultServiceProvider Provider { get; }

        /// <summary>The registered <see cref="IMessageService"/>.</summary>
        public FakeMessageService Messages { get; } = new FakeMessageService();

        /// <summary>The registered <see cref="IToastService"/>.</summary>
        public FakeToastService Toasts { get; } = new FakeToastService();

        /// <summary>The registered <see cref="IWindowNavigator"/>.</summary>
        public FakeWindowNavigator Navigator { get; } = new FakeWindowNavigator();

        /// <summary>The registered <see cref="IDialogProvider"/>.</summary>
        public FakeDialogProvider Dialogs { get; } = new FakeDialogProvider();

        /// <summary>The registered <see cref="IFileService"/>.</summary>
        public InMemoryFileService Files { get; } = new InMemoryFileService();

        /// <summary>The registered <c>ILoggerFactory</c> (replaces the silent one).</summary>
        public RecordingLoggerFactory Logs { get; } = new RecordingLoggerFactory();
    }
}
