using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace MvpApp.Tests
{
    /// <summary>
    /// How a presenter test looks. Replace the example presenter and view with your own:
    /// a fake view implements the view interface with plain properties and exposes a
    /// CapturingActionBinder; PresenterTestHost runs the real initialization;
    /// TestServices.CreateFakes() gives the presenter fake framework services (messages,
    /// toasts, dialogs, files, navigator, logger) that record calls and answer from scripts.
    /// </summary>
    public class ExamplePresenterTests
    {
        [Fact]
        public async Task Ok_GreetsAndClosesWithTheEnteredName()
        {
            var fakes = TestServices.CreateFakes();
            var view = new FakeGreetingView();
            using (var host = PresenterTestHost.Start(fakes.Provider, new GreetingPresenter(), view))
            {
                await host.Initialized;
                view.Name = "Ada";

                await view.Binder.DispatchAsync(GreetingActions.Ok);

                Assert.Equal("Hello, Ada!", Assert.Single(fakes.Messages.Calls).Text);
                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
                Assert.Equal("Ada", host.CloseResult);
            }
        }

        [Fact]
        public void Ok_IsDisabledUntilANameIsEntered()
        {
            var fakes = TestServices.CreateFakes();
            var view = new FakeGreetingView();
            using (PresenterTestHost.Start(fakes.Provider, new GreetingPresenter(), view))
            {
                Assert.False(view.Binder.CanDispatch(GreetingActions.Ok));
                view.Name = "Ada";
                Assert.True(view.Binder.CanDispatch(GreetingActions.Ok));
            }
        }

        // ---- example code under test (normally in the application project) ----

        public interface IGreetingView : IWindowView
        {
            string Name { get; }
        }

        public static class GreetingActions
        {
            private static readonly ViewActionFactory Factory = ViewAction.Factory.WithQualifier("Greeting");

            public static readonly ViewAction Ok = Factory.Create(nameof(Ok));
        }

        public sealed class GreetingPresenter : WindowPresenterBase<IGreetingView>
        {
            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
                => Dispatcher.Register(GreetingActions.Ok, OnOkAsync, canExecute: () => !string.IsNullOrEmpty(View.Name));

            private async Task OnOkAsync()
            {
                await Task.Yield();
                Messages.ShowInfo($"Hello, {View.Name}!");
                RequestClose(View.Name, InteractionStatus.Ok);
            }
        }

        private sealed class FakeGreetingView : IGreetingView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public string Name { get; set; }
        }
    }
}
