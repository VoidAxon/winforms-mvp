using System;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class CapturingActionBinderTests
    {
        private static readonly ViewAction Run = ViewAction.Create("BinderTests.Run");
        private static readonly ViewAction RunAsync = ViewAction.Create("BinderTests.RunAsync");
        private static readonly ViewAction<int> Add = ViewAction.Create<int>("BinderTests.Add");

        private sealed class CounterPresenter : WindowPresenterBase<IFixtureView>
        {
            public bool Enabled = true;
            public int Count;
            public TaskCompletionSource<bool> Gate = new TaskCompletionSource<bool>();

            protected override void OnViewAttached() { }
            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Run, () => Count++, canExecute: () => Enabled);
                Dispatcher.Register(RunAsync, async () => { await Gate.Task; Count += 10; });
                Dispatcher.Register(Add, (int n) => Count += n);
            }
        }

        [Fact]
        public void BindAndUnbindCounts_ShowAViewHandedToANewPresenter()
        {
            var view = new FixtureView();
            Assert.Equal(0, view.Binder.BindCount);

            var first = PresenterTestHost.Start(TestServices.Create(), new CounterPresenter(), view);
            var firstDispatcher = view.Binder.Dispatcher;
            first.Dispose();                                    // the old presenter releases the view
            using (PresenterTestHost.Start(TestServices.Create(), new CounterPresenter(), view))
            {
                Assert.Equal(2, view.Binder.BindCount);
                Assert.Equal(1, view.Binder.UnbindCount);
                Assert.True(view.Binder.IsBound);
                Assert.NotSame(firstDispatcher, view.Binder.Dispatcher);
            }
            Assert.Equal(2, view.Binder.UnbindCount);
        }

        [Fact]
        public void Dispatch_BeforeBind_ThrowsWithAHint()
        {
            var binder = new CapturingActionBinder();
            var ex = Assert.Throws<InvalidOperationException>(() => binder.Dispatch(Run));
            Assert.Contains("PresenterTestHost", ex.Message);
        }

        [Fact]
        public void Dispatch_RunsTheRegisteredHandler_AndHonorsCanExecute()
        {
            var view = new FixtureView();
            var presenter = new CounterPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                view.Binder.Dispatch(Run);
                Assert.Equal(1, presenter.Count);

                presenter.Enabled = false;
                Assert.False(view.Binder.CanDispatch(Run));
                view.Binder.Dispatch(Run);
                Assert.Equal(1, presenter.Count);
            }
        }

        [Fact]
        public async Task DispatchAsync_AwaitsAnAsyncHandler()
        {
            var view = new FixtureView();
            var presenter = new CounterPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                var task = view.Binder.DispatchAsync(RunAsync);
                Assert.False(task.IsCompleted);
                presenter.Gate.SetResult(true);
                await task;
                Assert.Equal(10, presenter.Count);
            }
        }

        [Fact]
        public void TypedOverloads_PassThePayload()
        {
            var view = new FixtureView();
            var presenter = new CounterPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                Assert.True(view.Binder.CanDispatch(Add));
                view.Binder.Dispatch(Add, 5);
                Assert.Equal(5, presenter.Count);
            }
        }
    }
}
