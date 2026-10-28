using System;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>ShowBusy: what the presenter asks the busy indicator, and when the requests end.</summary>
    public class ShowBusyTests
    {
        public interface IBusyView : IWindowView { }

        private sealed class BusyView : IBusyView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        private static class Actions
        {
            public static readonly ViewAction Load = ViewAction.Create("BusyTest.Load");
            public static readonly ViewAction Nested = ViewAction.Create("BusyTest.Nested");
            public static readonly ViewAction Hold = ViewAction.Create("BusyTest.Hold");
        }

        private sealed class BusyPresenter : WindowPresenterBase<IBusyView>
        {
            public TaskCompletionSource<bool> Gate { get; } = new TaskCompletionSource<bool>();
            public IDisposable Held { get; private set; }

            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Actions.Load, async ct =>
                {
                    using (ShowBusy("Loading"))
                    {
                        await Gate.Task;
                    }
                });
                Dispatcher.Register(Actions.Nested, () =>
                {
                    var outer = ShowBusy("outer");
                    var inner = ShowBusy();
                    outer.Dispose();   // ends in any order
                    inner.Dispose();
                    inner.Dispose();   // again: no effect
                });
                Dispatcher.Register(Actions.Hold, () => Held = ShowBusy("held"));
            }
        }

        [Fact]
        public async Task ShowBusy_AroundAwaitedWork_IsBusyUntilTheWorkEnds()
        {
            var fakes = TestServices.CreateFakes();
            var view = new BusyView();
            var presenter = new BusyPresenter();
            using (PresenterTestHost.Start(fakes.Provider, presenter, view))
            {
                var run = view.Binder.DispatchAsync(Actions.Load);

                Assert.True(fakes.Busy.IsBusyFor(view));
                var call = Assert.Single(fakes.Busy.Calls);
                Assert.Same(view, call.View);
                Assert.Equal("Loading", call.Message);

                presenter.Gate.SetResult(true);
                await run;
                Assert.False(fakes.Busy.IsBusy);
            }
        }

        [Fact]
        public void ShowBusy_Nested_EndsInAnyOrder_AndASecondDisposeDoesNothing()
        {
            var fakes = TestServices.CreateFakes();
            var view = new BusyView();
            using (PresenterTestHost.Start(fakes.Provider, new BusyPresenter(), view))
            {
                view.Binder.Dispatch(Actions.Nested);

                Assert.Equal(2, fakes.Busy.BeginCount);
                Assert.Equal("outer", fakes.Busy.Calls[0].Message);
                Assert.Null(fakes.Busy.Calls[1].Message);
                Assert.False(fakes.Busy.IsBusy);
            }
        }

        [Fact]
        public void DisposingThePresenter_EndsRequestsStillOpen()
        {
            var fakes = TestServices.CreateFakes();
            var view = new BusyView();
            var presenter = new BusyPresenter();
            var host = PresenterTestHost.Start(fakes.Provider, presenter, view);
            view.Binder.Dispatch(Actions.Hold);
            Assert.True(fakes.Busy.IsBusy);

            host.Dispose();

            Assert.False(fakes.Busy.IsBusy);
            presenter.Held.Dispose();   // the caller's late dispose: no effect, no error
            Assert.Equal(1, fakes.Busy.BeginCount);
        }

        [Fact]
        public void ARequest_MayBeEndedOnAnotherThread()
        {
            var fakes = TestServices.CreateFakes();
            var view = new BusyView();
            var presenter = new BusyPresenter();
            using (PresenterTestHost.Start(fakes.Provider, presenter, view))
            {
                view.Binder.Dispatch(Actions.Hold);

                var worker = new Thread(() => presenter.Held.Dispose());
                worker.Start();
                worker.Join();

                Assert.False(fakes.Busy.IsBusy);
            }
        }

        private sealed class ReentrantIndicator : IBusyIndicator
        {
            public Action DuringBegin { get; set; }
            public bool Ended { get; private set; }

            public IDisposable Begin(IViewBase view, string message)
            {
                DuringBegin?.Invoke();
                return Disposable.Create(() => Ended = true);
            }
        }

        [Fact]
        public void APresenterDisposedWhileTheIndicatorBegins_EndsThatRequestToo()
        {
            var services = TestServices.Create();
            var indicator = new ReentrantIndicator();
            services.RegisterInstance<IBusyIndicator>(indicator);
            var view = new BusyView();
            var presenter = new BusyPresenter();
            var host = PresenterTestHost.Start(services, presenter, view);
            indicator.DuringBegin = host.Dispose;   // e.g. an indicator whose Begin closes the window

            view.Binder.Dispatch(Actions.Hold);

            Assert.True(indicator.Ended);
        }

        [Fact]
        public void WithTheMinimalTestServices_ShowBusyNeedsNoFake()
        {
            var view = new BusyView();
            var presenter = new BusyPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                view.Binder.Dispatch(Actions.Hold);
                presenter.Held.Dispose();
            }
        }
    }
}
