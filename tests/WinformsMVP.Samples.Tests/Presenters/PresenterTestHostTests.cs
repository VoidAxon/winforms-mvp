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
            public System.Threading.Tasks.Task<bool> AsyncGate;
            public bool Disposed;

            protected override void OnViewAttached() { }
            protected override void RegisterViewActions()
                => Dispatcher.Register(Save, () => RequestClose("saved", InteractionStatus.Ok));
            protected override void OnInitialize()
            {
                if (CloseInInit) RequestClose(InteractionStatus.Cancel);
            }
            protected override bool CanClose(CloseReason reason) => !Dirty || reason == CloseReason.SystemShutdown;
            protected override System.Threading.Tasks.Task<bool> CanCloseAsync(CloseReason reason)
                => AsyncGate ?? base.CanCloseAsync(reason);
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
            var host = PresenterTestHost.Start(new EditPresenter { AsyncGate = new System.Threading.Tasks.TaskCompletionSource<bool>().Task }, new EditView());
            Assert.Null(host.CanClose());
        }

        // "Save changes?" -> Yes -> saved: the presenter pushes the result inside the gate. The host
        // records it only if the gate allows, as the real window only closes then.
        private sealed class SaveOnClosePresenter : WindowPresenterBase<IEditView>
        {
            public bool Allow = true;
            public System.Threading.Tasks.Task SaveTask;   // null => the gate answers synchronously

            protected override void OnViewAttached() { }
            protected override bool CanClose(CloseReason reason)
            {
                RequestClose("saved", InteractionStatus.Ok);
                return Allow;
            }
            protected override async System.Threading.Tasks.Task<bool> CanCloseAsync(CloseReason reason)
            {
                if (SaveTask == null) return CanClose(reason);
                await SaveTask;
                RequestClose("saved", InteractionStatus.Ok);
                return Allow;
            }
        }

        [Fact]
        public void PushInsideCanClose_Allowed_IsRecordedAsTheCloseResult()
        {
            var host = PresenterTestHost.Start(new SaveOnClosePresenter(), new EditView());

            Assert.True(host.CanClose());
            Assert.Equal(1, host.CloseCount);
            Assert.Equal("saved", host.CloseResult);
            Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
        }

        [Fact]
        public void PushInsideCanClose_Blocked_IsNotRecorded()
        {
            var host = PresenterTestHost.Start(new SaveOnClosePresenter { Allow = false }, new EditView());

            Assert.False(host.CanClose());
            Assert.False(host.CloseRequested);
            Assert.Null(host.CloseResult);
        }

        [Fact]
        public async System.Threading.Tasks.Task PushInsideCanCloseAsync_IsRecordedWhenTheGateAllows()
        {
            var save = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var host = PresenterTestHost.Start(new SaveOnClosePresenter { SaveTask = save.Task }, new EditView());

            var answer = host.CanCloseAsync();
            Assert.False(host.CloseRequested);              // still saving

            save.SetResult(true);
            Assert.True(await answer);
            Assert.Equal("saved", host.CloseResult);
        }

        [Fact]
        public async System.Threading.Tasks.Task PushInsideCanCloseAsync_Blocked_IsNotRecorded()
        {
            var save = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var host = PresenterTestHost.Start(new SaveOnClosePresenter { SaveTask = save.Task, Allow = false }, new EditView());

            var answer = host.CanCloseAsync();
            save.SetResult(true);

            Assert.False(await answer);
            Assert.False(host.CloseRequested);
        }

        [Fact]
        public async System.Threading.Tasks.Task CloseAttemptWhileAsyncGatePending_IsHeldOff_NotAskedAgain()
        {
            var save = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var presenter = new CountingSaveOnClosePresenter { SaveTask = save.Task, Allow = false };
            var host = PresenterTestHost.Start(presenter, new EditView());

            var first = host.CanCloseAsync();
            var second = host.CanCloseAsync();   // a real window holds this off instead of re-asking
            Assert.Null(host.CanClose());
            Assert.Equal(1, presenter.GateRuns);

            save.SetResult(true);
            Assert.False(await first);
            Assert.False(await second);
            Assert.False(host.CloseRequested);   // the blocked close's result is not recorded
        }

        [Fact]
        public void SystemCloseWhileAsyncGatePending_IsAllowedWithoutAsking()
        {
            var presenter = new CountingSaveOnClosePresenter { SaveTask = new System.Threading.Tasks.TaskCompletionSource<bool>().Task };
            var host = PresenterTestHost.Start(presenter, new EditView());

            Assert.Null(host.CanClose());
            Assert.True(host.CanClose(CloseReason.SystemShutdown));
            Assert.Equal(1, presenter.GateRuns);
        }

        private sealed class CountingSaveOnClosePresenter : WindowPresenterBase<IEditView>
        {
            public bool Allow = true;
            public System.Threading.Tasks.Task SaveTask;
            public int GateRuns;

            protected override void OnViewAttached() { }
            protected override async System.Threading.Tasks.Task<bool> CanCloseAsync(CloseReason reason)
            {
                GateRuns++;
                await SaveTask;
                RequestClose("saved", InteractionStatus.Ok);
                return Allow;
            }
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

        private sealed class FailingAttachPresenter : ControlPresenterBase<IPanelView>
        {
            public bool Disposed;
            protected override void OnViewAttached() => throw new InvalidOperationException("attach");
            protected override void Cleanup() => Disposed = true;
        }

        [Fact]
        public void Start_OnViewAttachedThrows_DisposesAndRethrows()
        {
            var presenter = new FailingAttachPresenter();
            Assert.Throws<InvalidOperationException>(() => PresenterTestHost.Start(presenter, new PanelView()));
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void Start_AlreadyHostedPresenter_ThrowsWithoutDisposing()
        {
            var presenter = new EditPresenter();
            PresenterTestHost.Start(presenter, new EditView());

            Assert.Throws<InvalidOperationException>(() => PresenterTestHost.Start(presenter, new EditView()));
            Assert.False(presenter.Disposed);
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
