using System;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>RequestClose goes through CanClose once the window is open (V-28); during the opening it
    /// cancels the opening without asking.</summary>
    public class RequestCloseGateHostTests
    {
        private static readonly ViewAction Close = ViewAction.Create("GateHost.Close");
        private static readonly ViewAction Save = ViewAction.Create("GateHost.Save");

        public interface IGateView : IWindowView { }

        private sealed class GateView : IGateView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
        }

        private sealed class EditPresenter : WindowPresenterBase<IGateView>
        {
            public bool Dirty;
            public bool CancelOpening;
            public int GateCalls;
            public TaskCompletionSource<bool> AsyncGate;
            public object SaveInGate;

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Close, () => RequestClose(InteractionStatus.Cancel));
                Dispatcher.Register(Save, () => { Dirty = false; RequestClose("saved", InteractionStatus.Ok); });
            }

            protected override void OnInitialize()
            {
                if (CancelOpening)
                {
                    RequestClose("first", InteractionStatus.Cancel);
                    RequestClose("gone", InteractionStatus.Cancel);
                }
            }

            protected override Task<bool> CanCloseAsync(CloseReason reason)
            {
                GateCalls++;
                if (AsyncGate != null) return AsyncGate.Task;
                if (!Dirty) return Task.FromResult(true);
                var yes = Messages.ConfirmYesNo("破棄しますか？");
                if (yes && SaveInGate != null) RequestClose(SaveInGate, InteractionStatus.Ok);
                return Task.FromResult(yes);
            }
        }

        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly GateView _view = new GateView();

        [Fact]
        public void Push_OnACleanScreen_IsAllowed_AndRecorded()
        {
            var presenter = new EditPresenter();
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _view.Binder.Dispatch(Save);

                Assert.Equal(1, presenter.GateCalls);
                Assert.True(host.CloseRequested);
                Assert.Equal("saved", host.CloseResult);
            }
        }

        [Fact]
        public void Push_OnADirtyScreen_AsksThroughTheGate_NoKeepsItOpen()
        {
            var presenter = new EditPresenter { Dirty = true };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _fakes.Messages.EnqueueYesNo(false);

                _view.Binder.Dispatch(Close);

                Assert.Contains(_fakes.Messages.Calls, m => m.IsConfirmation);
                Assert.False(host.CloseRequested);
            }
        }

        [Fact]
        public void Push_OnADirtyScreen_YesCloses()
        {
            var presenter = new EditPresenter { Dirty = true };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _fakes.Messages.EnqueueYesNo(true);

                _view.Binder.Dispatch(Close);

                Assert.True(host.CloseRequested);
                Assert.Equal(InteractionStatus.Cancel, host.CloseStatus);
            }
        }

        [Fact]
        public void Push_TheGatesOwnRequestClose_ReplacesTheResult()
        {
            var presenter = new EditPresenter { Dirty = true, SaveInGate = 42 };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _fakes.Messages.EnqueueYesNo(true);

                _view.Binder.Dispatch(Close);

                Assert.Equal(1, host.CloseCount);
                Assert.Equal(42, host.CloseResult);
                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
            }
        }

        [Fact]
        public async Task Push_AsyncGate_DecidesLater_PushesMeanwhileUpdateTheResult()
        {
            var presenter = new EditPresenter { AsyncGate = new TaskCompletionSource<bool>() };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _view.Binder.Dispatch(Close);
                _view.Binder.Dispatch(Save);              // while the gate is pending: not a second close
                Assert.False(host.CloseRequested);
                Assert.Equal(1, presenter.GateCalls);

                presenter.AsyncGate.SetResult(true);
                await host.WaitForCloseDecisionAsync();

                Assert.Equal(1, host.CloseCount);
                Assert.Equal("saved", host.CloseResult);
            }
        }

        [Fact]
        public async Task Push_AsyncGate_Veto_DiscardsTheResult()
        {
            var presenter = new EditPresenter { AsyncGate = new TaskCompletionSource<bool>() };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _view.Binder.Dispatch(Save);
                presenter.AsyncGate.SetResult(false);
                await host.WaitForCloseDecisionAsync();

                Assert.False(host.CloseRequested);

                presenter.AsyncGate = null;               // a later close on a clean screen
                _view.Binder.Dispatch(Close);
                Assert.Equal(InteractionStatus.Cancel, host.CloseStatus);
                Assert.Null(host.CloseResult);            // the vetoed "saved" did not leak in
            }
        }

        [Fact]
        public void DuringTheOpening_RequestClose_CancelsWithoutAsking_LastResultWins()
        {
            var presenter = new EditPresenter { CancelOpening = true, Dirty = true };
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                Assert.Equal(0, presenter.GateCalls);
                Assert.Equal(1, host.CloseCount);
                Assert.Equal("gone", host.CloseResult);
                Assert.Empty(_fakes.Messages.Calls);
            }
        }

        [Fact]
        public async Task WaitForCloseDecisionAsync_WithNothingPending_CompletesAtOnce()
        {
            using (var host = PresenterTestHost.Start(_fakes.Provider, new EditPresenter(), _view))
                await host.WaitForCloseDecisionAsync();
        }

        [Fact]
        public void Push_GateAsksAnUnscriptedQuestion_TheTestFails()
        {
            var presenter = new EditPresenter { Dirty = true };   // the gate asks; the test scripted no answer
            using (PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                Assert.Throws<UnscriptedInteractionException>(() => _view.Binder.Dispatch(Close));
            }
        }

        [Fact]
        public void AfterAnAcceptedClose_FurtherRequestsAreIgnored_LikeAClosedWindow()
        {
            var presenter = new EditPresenter();
            using (var host = PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                _view.Binder.Dispatch(Save);              // accepted
                presenter.Dirty = true;
                _view.Binder.Dispatch(Close);             // the window is gone: not asked, not recorded

                Assert.Equal(1, presenter.GateCalls);
                Assert.Equal(1, host.CloseCount);
                Assert.Equal("saved", host.CloseResult);
            }
        }
    }
}
