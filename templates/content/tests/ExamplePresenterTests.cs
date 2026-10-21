using System.Threading.Tasks;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Testing;
using Xunit;

namespace MvpApp.Tests
{
    /// <summary>
    /// How a presenter test looks. Replace the example presenter and view with your own:
    /// a fake view implements the view interface with plain properties and exposes a
    /// CapturingActionBinder; PresenterTestHost runs the real initialization.
    /// </summary>
    public class ExamplePresenterTests
    {
        [Fact]
        public async Task Ok_ClosesWithTheEnteredName()
        {
            var view = new FakeGreetingView();
            using (var host = PresenterTestHost.Start(new GreetingPresenter(), view))
            {
                await host.Initialized;
                view.Name = "Ada";

                await view.Binder.DispatchAsync(GreetingActions.Ok);

                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
                Assert.Equal("Ada", host.CloseResult);
            }
        }

        [Fact]
        public void Ok_IsDisabledUntilANameIsEntered()
        {
            var view = new FakeGreetingView();
            PresenterTestHost.Start(new GreetingPresenter(), view);

            Assert.False(view.Binder.CanDispatch(GreetingActions.Ok));
            view.Name = "Ada";
            Assert.True(view.Binder.CanDispatch(GreetingActions.Ok));
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
