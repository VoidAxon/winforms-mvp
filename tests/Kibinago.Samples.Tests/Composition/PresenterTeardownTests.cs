using System;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    public class PresenterTeardownTests
    {
        private static readonly ViewAction Run = ViewAction.Create("Teardown.Run");

        public interface IRunView : IWindowView { }

        private sealed class RunView : IRunView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        private sealed class NoBinderView : IRunView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class RunPresenter : WindowPresenterBase<IRunView>
        {
            public int Runs;
            protected override void OnViewAttached() { }
            protected override void RegisterViewActions() => Dispatcher.Register(Run, () => Runs++);
        }

        [Fact]
        public void Dispose_UnbindsTheBinderItBound_LateDispatchDoesNothing()
        {
            var view = new RunView();
            var presenter = new RunPresenter();
            var host = PresenterTestHost.Start(TestServices.Create(), presenter, view);
            var captured = view.Binder.Dispatcher;

            host.Dispose();

            Assert.False(view.Binder.IsBound);
            view.Binder.Dispatch(Run);                      // a late click: inert
            Assert.False(view.Binder.CanDispatch(Run));
            captured.Dispatch(Run);                         // even through a retained dispatcher
            Assert.Equal(0, presenter.Runs);
        }

        [Fact]
        public async Task CapturingActionBinder_AfterUnbind_IsInert()
        {
            var view = new RunView();
            var presenter = new RunPresenter();
            using (PresenterTestHost.Start(TestServices.Create(), presenter, view))
            {
                view.Binder.Unbind();
                view.Binder.Unbind();                       // idempotent
                view.Binder.Dispatch(Run);
                await view.Binder.DispatchAsync(Run);
                Assert.Equal(0, presenter.Runs);
            }
        }

        [Fact]
        public void CapturingActionBinder_BeforeBind_StillThrowsWithAHint()
        {
            Assert.Throws<InvalidOperationException>(() => new CapturingActionBinder().Dispatch(Run));
        }

        [Fact]
        public void ViewWithoutBinder_DisposeIsFine()
        {
            var host = PresenterTestHost.Start(TestServices.Create(), new RunPresenter(), new NoBinderView());
            host.Dispose();
        }

        private sealed class SelfDisposingPresenter : WindowPresenterBase<IRunView>
        {
            public bool Registered;
            public bool Initialized;
            protected override void OnViewAttached() => Dispose();   // e.g. its control was disposed here
            protected override void RegisterViewActions() => Registered = true;
            protected override void OnInitialize() => Initialized = true;
        }

        [Fact]
        public void DisposedWhileAttaching_NoInitializationStageRuns()
        {
            var presenter = new SelfDisposingPresenter();
            try { PresenterTestHost.Start(TestServices.Create(), presenter, new RunView()); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }

            Assert.False(presenter.Registered);
            Assert.False(presenter.Initialized);
        }
    }
}
