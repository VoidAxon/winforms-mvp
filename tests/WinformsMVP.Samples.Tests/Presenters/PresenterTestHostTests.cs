using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class PresenterTestHostTests
    {
        private static readonly ViewAction Save = ViewAction.Create("HostTests.Save");

        public interface IEditView : IWindowView { }

        private sealed class EditView : IEditView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        private sealed class EditPresenter : WindowPresenterBase<IEditView>
        {
            public bool Dirty;
            public bool CloseInInit;
            public Action<Action<bool>> AsyncGate;
            public bool Disposed;

            protected override void OnViewAttached() { }
            protected override void RegisterViewActions()
                => Dispatcher.Register(Save, () => RequestClose("saved", InteractionStatus.Ok));
            protected override void OnInitialize()
            {
                if (CloseInInit) RequestClose(InteractionStatus.Cancel);
            }
            protected override bool CanClose(CloseReason reason) => !Dirty || reason == CloseReason.SystemShutdown;
            protected override void CanClose(CloseReason reason, Action<bool> proceed)
            {
                if (AsyncGate != null) AsyncGate(proceed); else base.CanClose(reason, proceed);
            }
            protected override void Cleanup() => Disposed = true;
        }

        private sealed class ParamPresenter : WindowPresenterBase<IEditView, int>
        {
            public int Received;
            protected override void OnViewAttached() { }
            protected override void OnInitialize(int parameters) => Received = parameters;
        }

        [Fact]
        public void Start_InitializesAndBindsTheViewsActionBinder()
        {
            var view = new EditView();
            using (var host = PresenterTestHost.Start(new EditPresenter(), view))
            {
                view.Binder.Dispatch(Save);

                Assert.True(host.CloseRequested);
                Assert.Equal(1, host.CloseCount);
                Assert.Equal("saved", host.CloseResult);
                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
            }
        }

        [Fact]
        public void Start_WithParameters_PassesThem()
        {
            var presenter = new ParamPresenter();
            PresenterTestHost.Start(presenter, new EditView(), 42);
            Assert.Equal(42, presenter.Received);
        }

        [Fact]
        public void CloseRequestedInOnInitialize_IsRecorded()
        {
            var host = PresenterTestHost.Start(new EditPresenter { CloseInInit = true }, new EditView());
            Assert.Equal(InteractionStatus.Cancel, host.CloseStatus);
        }

        [Fact]
        public void CanClose_RunsThePresentersGate()
        {
            var presenter = new EditPresenter { Dirty = true };
            var host = PresenterTestHost.Start(presenter, new EditView());

            Assert.False(host.CanClose());
            Assert.True(host.CanClose(CloseReason.SystemShutdown));
        }

        [Fact]
        public void CanClose_PendingAsyncAnswer_ReturnsNull()
        {
            var host = PresenterTestHost.Start(new EditPresenter { AsyncGate = _ => { } }, new EditView());
            Assert.Null(host.CanClose());
        }

        [Fact]
        public void Dispose_DisposesThePresenter()
        {
            var presenter = new EditPresenter();
            PresenterTestHost.Start(presenter, new EditView()).Dispose();
            Assert.True(presenter.Disposed);
        }

        public interface IPanelView : IViewBase { }
        private sealed class PanelView : IPanelView { }

        private sealed class PanelPresenter : ControlPresenterBase<IPanelView>
        {
            public bool Initialized;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => Initialized = true;
        }

        [Fact]
        public void ControlPresenter_StartsButHasNoCloseGate()
        {
            var presenter = new PanelPresenter();
            var host = PresenterTestHost.Start(presenter, new PanelView());

            Assert.True(presenter.Initialized);
            Assert.Throws<InvalidOperationException>(() => host.CanClose());
        }

        private sealed class FailingPresenter : ControlPresenterBase<IPanelView>
        {
            public bool Disposed;
            protected override void OnViewAttached() { }
            protected override void OnInitialize() => throw new InvalidOperationException("boom");
            protected override void Cleanup() => Disposed = true;
        }

        [Fact]
        public void Start_InitializeThrows_DisposesAndRethrows()
        {
            var presenter = new FailingPresenter();
            Assert.Throws<InvalidOperationException>(() => PresenterTestHost.Start(presenter, new PanelView()));
            Assert.True(presenter.Disposed);
        }
    }
}
