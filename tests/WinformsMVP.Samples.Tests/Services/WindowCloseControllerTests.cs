using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services.Implementations;
using Xunit;
using CloseReason = WinformsMVP.Common.CloseReason;

namespace WinformsMVP.Samples.Tests.Services
{
    public class WindowCloseControllerTests
    {
        // A Form that also satisfies IWindowView so the controller's `view is Form` check passes.
        // NOTE: IWindowView on this branch still declares Closing/OnClosing, implement them
        // (they are unused by the controller, which talks to ICloseParticipant, not the view).
        private sealed class FakeWindow : Form, IWindowView
        {
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            bool IWindowView.IsDisposed => base.IsDisposed;
            void IWindowView.Activate() => base.Activate();
            public event EventHandler<WinformsMVP.Common.Events.WindowClosingEventArgs> Closing;
            public void OnClosing(WinformsMVP.Common.Events.WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        }

        private sealed class FakeParticipant : ICloseParticipant, IDisposable
        {
            public Func<CloseReason, bool> SyncDecision;          // null => allow
            public Action<CloseReason, Action<bool>> AsyncGate;   // overrides SyncDecision when set
            public ICloseSink BoundSink;
            public bool Disposed;

            public void BindCloseSink(ICloseSink sink) => BoundSink = sink;
            public void RequestCloseCore(object result, InteractionStatus status)
                => BoundSink.Close(result, status);
            public void CanCloseGate(CloseReason reason, Action<bool> proceed)
            {
                if (AsyncGate != null) { AsyncGate(reason, proceed); return; }
                proceed(SyncDecision?.Invoke(reason) ?? true);
            }
            public void Dispose() => Disposed = true;
        }

        private static (FakeWindow form, FakeParticipant presenter, List<(object, InteractionStatus)> results, WindowCloseController controller)
            Build(bool disposeForm = true)
        {
            var form = new FakeWindow();
            var presenter = new FakeParticipant();
            var results = new List<(object, InteractionStatus)>();
            var controller = new WindowCloseController(
                form, presenter, (r, s) => results.Add((r, s)), disposeForm);
            controller.BindSink();
            controller.WireFormEvents();
            return (form, presenter, results, controller);
        }

        [Fact]
        public void UserClose_GateAllows_FormClosesAndConverges()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => true;

            form.Show();
            form.Close();

            Assert.Single(results);
            Assert.Equal(InteractionStatus.Cancel, results[0].Item2); // user close => default Cancel
            Assert.True(presenter.Disposed);
        }

        [Fact]
        public void UserClose_GateBlocks_StaysOpen()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = r => r != CloseReason.Normal;

            form.Show();
            form.Close();

            Assert.True(form.Visible);
            Assert.Empty(results);
            form.Dispose();
        }

        [Fact]
        public void PresenterPush_SkipsGate_ConvergesWithResult()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => false; // gate would block, push must bypass it

            form.Show();
            presenter.RequestCloseCore("payload", InteractionStatus.Ok);

            Assert.Single(results);
            Assert.Equal("payload", results[0].Item1);
            Assert.Equal(InteractionStatus.Ok, results[0].Item2);
        }

        [Fact]
        public void AsyncGate_DeferThenAllow_ClosesWithoutReRunningGate()
        {
            var (form, presenter, results, _) = Build();
            Action<bool> stored = null;
            int gateRuns = 0;
            presenter.AsyncGate = (reason, proceed) => { gateRuns++; stored = proceed; };

            form.Show();
            form.Close();                 // deferred: cancelled, window stays open
            Assert.True(form.Visible);

            stored(true);                 // async allow => controller re-closes with suppress

            Assert.False(form.Visible);
            Assert.Equal(1, gateRuns);    // the suppressed re-close must NOT run the gate again
            Assert.Single(results);
        }

        [Fact]
        public void AsyncGate_DeferThenBlock_StaysOpen_NextCloseRunsGateAgain()
        {
            var (form, presenter, results, _) = Build();
            var stored = new List<Action<bool>>();
            int gateRuns = 0;
            presenter.AsyncGate = (reason, proceed) => { gateRuns++; stored.Add(proceed); };

            form.Show();
            form.Close();
            stored[stored.Count - 1](false);  // async block
            Assert.True(form.Visible);
            Assert.Equal(1, gateRuns);

            form.Close();                      // second close must run the gate again
            Assert.Equal(2, gateRuns);
            form.Dispose();
        }

        [Fact]
        public void GateThrows_BlocksClose_AndDoesNotConverge()
        {
            var (form, presenter, results, _) = Build();
            presenter.AsyncGate = (reason, proceed) => throw new InvalidOperationException("boom");

            form.Show();
            form.Close();

            Assert.True(form.Visible);
            Assert.Empty(results);
            form.Dispose();
        }

        [Fact]
        public void CloseBeforeShow_DoesNotTouchForm_SetsFlag()
        {
            var (form, presenter, results, controller) = Build();

            presenter.RequestCloseCore("early", InteractionStatus.Ok); // no Show() yet

            Assert.True(controller.CloseRequestedBeforeShow);
            controller.ConvergeWithoutShow();
            Assert.Single(results);
            Assert.Equal("early", results[0].Item1);
        }
    }
}
