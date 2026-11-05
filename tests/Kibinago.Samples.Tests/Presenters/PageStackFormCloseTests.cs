using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.Tests.Composition;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// A real Form hosting a window presenter with a page stack: the reservation a close check takes is kept from
    /// approval until the window closes, and released when the approved close is abandoned or refused.
    /// </summary>
    public class PageStackFormCloseTests : IDisposable
    {
        private readonly List<string> _log = new List<string>();
        private readonly PagesForm _form = new PagesForm();
        private readonly WorkspacePresenter _workspace;
        private bool _closed;

        public PageStackFormCloseTests()
        {
            _workspace = new WorkspacePresenter(_log);
            _workspace.Connect<IWorkspaceView>(_form);
            _form.FormClosed += (s, e) => _closed = true;
            _form.Show();
        }

        public void Dispose()
        {
            if (!_form.IsDisposed) _form.Dispose();
        }

        private sealed class PagesForm : Form, IWorkspaceView
        {
            public readonly List<string> Removed = new List<string>();
            public IViewActionBinder ActionBinder => null;
            public void RemovePage(IPageView page) => Removed.Add(page.Tag);
        }

        // DoEvents does not raise Application.Idle (a real message loop does when its queue is empty).
        private static void PumpUntil(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition() && watch.ElapsedMilliseconds < 5000)
            {
                Application.DoEvents();
                Application.RaiseIdle(EventArgs.Empty);
                System.Threading.Thread.Sleep(1);
            }
            Assert.True(condition());
        }

        private static void Pump()
        {
            for (int i = 0; i < 20; i++)
            {
                Application.DoEvents();
                Application.RaiseIdle(EventArgs.Empty);
            }
        }

        [Fact]
        public void ApprovedClose_Closes_PagesAskedOnce()
        {
            var a = new GuardedPage("A", _log);
            _workspace.Push(a);

            _form.Close();
            Pump();

            Assert.True(_closed);
            Assert.Equal(1, a.AskCount);
            Assert.Empty(_form.Removed);   // owner disposal removes no views
        }

        [Fact]
        public async Task ApprovedThenAbandoned_ByOtherFormClosingHandler_ReleasesStacks()
        {
            _workspace.Push(new GuardedPage("A", _log));
            _workspace.Push(new GuardedPage("B", _log));
            _form.FormClosing += (s, e) => e.Cancel = true;   // subscribed after the framework's handler

            _form.Close();
            PumpUntil(() => !_workspace.Stack.IsBusy);

            Assert.False(_closed);
            Assert.True(await _workspace.Stack.PopAsync());
            Assert.Equal(1, _workspace.Stack.Count);
        }

        [Fact]
        public void AsyncApproval_NoNavigationBetweenApprovalAndClose()
        {
            var pending = new TaskCompletionSource<bool>();
            _workspace.Push(new GuardedPage("A", _log));
            _workspace.Push(new GuardedPage("B", _log) { Answer = () => pending.Task });
            bool? busyDuringReplay = null;
            Task<bool> jumpDuringReplay = null;
            // A later handler sees the close the approval re-issues: the window is still open there.
            _form.FormClosing += (s, e) =>
            {
                if (!pending.Task.IsCompleted) return;
                busyDuringReplay = _workspace.Stack.IsBusy;
                jumpDuringReplay = _workspace.Stack.PopAsync();
            };

            _form.Close();
            Assert.False(_closed);
            Assert.True(_workspace.Stack.IsBusy);

            pending.SetResult(true);
            PumpUntil(() => _closed);

            Assert.True(busyDuringReplay);
            Assert.True(jumpDuringReplay.IsCompleted);
            Assert.False(jumpDuringReplay.Result);
        }

        [Fact]
        public async Task AsyncApproval_ReplayCancelledByAnotherHandler_ReleasesStacks()
        {
            var pending = new TaskCompletionSource<bool>();
            _workspace.Push(new GuardedPage("A", _log));
            var b = new GuardedPage("B", _log) { Answer = () => pending.Task };
            _workspace.Push(b);
            _form.FormClosing += (s, e) => { if (pending.Task.IsCompleted) e.Cancel = true; };

            _form.Close();
            pending.SetResult(true);
            PumpUntil(() => !_workspace.Stack.IsBusy);

            Assert.False(_closed);
            b.Answer = () => Task.FromResult(true);
            Assert.True(await _workspace.Stack.PopAsync());
        }

        [Fact]
        public void ApprovedClose_ALaterHandlerPumpsMessages_StacksStayHeldUntilTheAttemptEnds()
        {
            _workspace.Push(new GuardedPage("A", _log));
            bool? busyWhilePumping = null;
            _form.FormClosing += (s, e) =>
            {
                Application.DoEvents();                  // a legacy handler that pumps
                busyWhilePumping = _workspace.Stack.IsBusy;
                e.Cancel = true;
            };

            _form.Close();
            Assert.True(busyWhilePumping);
            PumpUntil(() => !_workspace.Stack.IsBusy);
            Assert.False(_closed);
        }

        [Fact]
        public async Task RefusedClose_ReleasesStacks()
        {
            _workspace.Push(new GuardedPage("A", _log));
            var b = new GuardedPage("B", _log) { Answer = () => Task.FromResult(false) };
            _workspace.Push(b);

            _form.Close();
            Pump();

            Assert.False(_closed);
            Assert.False(_workspace.Stack.IsBusy);
            Assert.False(await _workspace.Stack.PopToCountAsync(1));
            Assert.Equal(2, b.AskCount);   // asked again by the jump: the stack was not left busy
            Assert.Equal(2, _workspace.Stack.Count);
        }

        [Fact]
        public void WindowWithoutStacks_ApprovedThenAbandoned_PostsNothing()
        {
            var form = new PagesForm();
            var plain = new PlainWindowPresenter();
            plain.Connect<IWorkspaceView>(form);
            form.FormClosing += (s, e) => e.Cancel = true;
            form.Show();
            try
            {
                form.Close();
                Pump();
                Assert.Equal(1, plain.Asked);
                Assert.False(form.IsDisposed);
            }
            finally { form.Dispose(); }
        }

        private sealed class PlainWindowPresenter : WindowPresenterBase<IWorkspaceView>
        {
            public int Asked;
            protected override bool CanClose(Kibinago.Common.CloseReason reason) { Asked++; return true; }
        }
    }
}
