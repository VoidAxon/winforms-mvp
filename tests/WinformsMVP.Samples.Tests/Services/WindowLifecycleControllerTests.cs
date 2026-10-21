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
    public class WindowLifecycleControllerTests
    {
        // A Form that also satisfies IWindowView so the controller's `view is Form` check passes.
        // The controller talks to ICloseParticipant, not the view, so the view stays slim.
        private sealed class FakeWindow : Form, IWindowView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class FakeParticipant : ICloseParticipant, IDisposable
        {
            public Func<CloseReason, bool> SyncDecision;          // null => allow
            public Action<CloseReason, Action<bool>> AsyncGate;   // overrides SyncDecision when set
            public ICloseSink BoundSink;
            public bool Disposed;

            public void BindCloseSink(ICloseSink sink) => BoundSink = sink;
            public void Push(object result, InteractionStatus status) => BoundSink.Close(result, status);
            public void CanCloseGate(CloseReason reason, Action<bool> proceed)
            {
                if (AsyncGate != null) { AsyncGate(reason, proceed); return; }
                proceed(SyncDecision?.Invoke(reason) ?? true);
            }
            public void Dispose() => Disposed = true;
        }

        private static (FakeWindow form, FakeParticipant presenter, List<(object, InteractionStatus)> results, WindowLifecycleController controller)
            Build(bool disposeForm = true)
        {
            var form = new FakeWindow();
            var presenter = new FakeParticipant();
            var results = new List<(object, InteractionStatus)>();
            var controller = new WindowLifecycleController(
                form, presenter, (r, s) => results.Add((r, s)), disposeForm);
            controller.BindSink();
            controller.WireFormEvents();
            // These tests rely on Form.Show()/Close() raising FormClosing/FormClosed synchronously on the test thread (no message pump). That is exactly the contract the controller depends on.
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
            presenter.Push("payload", InteractionStatus.Ok);

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

            presenter.Push("early", InteractionStatus.Ok); // no Show() yet

            Assert.True(controller.CloseRequestedBeforeShow);
            controller.ConvergeWithoutShow();
            Assert.Single(results);
            Assert.Equal("early", results[0].Item1);
            Assert.True(presenter.Disposed);
        }

        // A push made while the gate is deciding (e.g. "Save changes?" -> Yes -> saved) is the
        // result of THAT close: it does not close the window by itself, the gate's answer does.

        [Fact]
        public void PushInsideSyncGate_ThenAllow_ConvergesOnceWithPushedResult()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => { presenter.Push("saved", InteractionStatus.Ok); return true; };

            form.Show();
            form.Close();

            Assert.False(form.Visible);
            Assert.Single(results);
            Assert.Equal("saved", results[0].Item1);
            Assert.Equal(InteractionStatus.Ok, results[0].Item2);
        }

        [Fact]
        public void PushInsideSyncGate_ThenBlock_StaysOpenAndDiscardsResult()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => { presenter.Push("saved", InteractionStatus.Ok); return false; };

            form.Show();
            form.Close();

            Assert.True(form.Visible);
            Assert.Empty(results);

            presenter.SyncDecision = _ => true;   // a later plain user close
            form.Close();

            Assert.Single(results);
            Assert.Null(results[0].Item1);        // the blocked close's result is not reused
            Assert.Equal(InteractionStatus.Cancel, results[0].Item2);
        }

        [Fact]
        public void PushInsideGate_CloseCancelledElsewhere_ResultDoesNotLeakIntoNextClose()
        {
            var (form, presenter, results, _) = Build();
            presenter.SyncDecision = _ => { presenter.Push("saved", InteractionStatus.Ok); return true; };
            bool vetoOnce = true;
            form.FormClosing += (s, e) => { if (vetoOnce) { e.Cancel = true; vetoOnce = false; } };

            form.Show();
            form.Close();                         // the gate allows, another handler cancels

            Assert.True(form.Visible);
            Assert.Empty(results);

            presenter.SyncDecision = _ => true;   // a later plain user close
            form.Close();

            Assert.Single(results);
            Assert.Null(results[0].Item1);
            Assert.Equal(InteractionStatus.Cancel, results[0].Item2);
        }

        // ---- owned windows ----

        [Fact]
        public void OwnedChildVeto_IsNotOverriddenByTheOwnersGate()
        {
            var (owner, presenter, results, _) = Build();
            int ownerGateRuns = 0;
            presenter.SyncDecision = _ => { ownerGateRuns++; return true; };
            var child = new Form();
            child.FormClosing += (s, e) => e.Cancel = true;   // e.g. "Save changes?" -> Cancel

            owner.Show();
            child.Show(owner);
            owner.Close();

            Assert.True(owner.Visible);
            Assert.True(child.Visible);
            Assert.Empty(results);
            Assert.Equal(0, ownerGateRuns);   // the close was already refused: nothing to ask
            child.Dispose();
            owner.Dispose();
        }

        [Fact]
        public void SessionEnding_OwnedChildIsTreatedAsShutdown_NotAsked()
        {
            var (owner, ownerPresenter, _, _) = Build();
            var (child, childPresenter, _, _) = Build();
            var asked = new List<CloseReason>();
            childPresenter.SyncDecision = r => { asked.Add(r); return r != CloseReason.Normal && r != CloseReason.ParentClosing; };

            owner.Show();
            child.Show(owner);
            var answer = SendMessage(owner.Handle, WM_QUERYENDSESSION, IntPtr.Zero, IntPtr.Zero);

            Assert.DoesNotContain(CloseReason.ParentClosing, asked);   // WinForms says FormOwnerClosing; it is a shutdown
            Assert.NotEqual(IntPtr.Zero, answer);                       // the session may end
            child.Dispose();
            owner.Dispose();
        }

        [Fact]
        public void OwnerClosedByTheUser_OwnedChildIsAskedAsParentClosing()
        {
            var (owner, _, _, _) = Build();
            var (child, childPresenter, childResults, _) = Build();
            var asked = new List<CloseReason>();
            childPresenter.SyncDecision = r => { asked.Add(r); return true; };

            owner.Show();
            child.Show(owner);
            owner.Close();

            Assert.Contains(CloseReason.ParentClosing, asked);
            Assert.Single(childResults);
        }

        private const int WM_QUERYENDSESSION = 0x0011;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [Fact]
        public void PushWhileAsyncGatePending_WaitsForAllow_ThenConvergesWithPushedResult()
        {
            var (form, presenter, results, _) = Build();
            Action<bool> stored = null;
            presenter.AsyncGate = (reason, proceed) => stored = proceed;

            form.Show();
            form.Close();                                   // deferred
            presenter.Push("saved", InteractionStatus.Ok);  // e.g. after awaiting the save

            Assert.True(form.Visible);                      // the gate has not answered yet
            Assert.Empty(results);

            stored(true);

            Assert.False(form.Visible);
            Assert.Single(results);
            Assert.Equal("saved", results[0].Item1);
            Assert.Equal(InteractionStatus.Ok, results[0].Item2);
        }

        [Fact]
        public void PushWhileAsyncGatePending_ThenBlock_StaysOpenAndDiscardsResult()
        {
            var (form, presenter, results, _) = Build();
            Action<bool> stored = null;
            presenter.AsyncGate = (reason, proceed) => stored = proceed;

            form.Show();
            form.Close();
            presenter.Push("saved", InteractionStatus.Ok);
            stored(false);

            Assert.True(form.Visible);
            Assert.Empty(results);
            form.Dispose();
        }
    }
}
