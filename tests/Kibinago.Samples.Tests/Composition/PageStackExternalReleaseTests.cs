using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>A page disposed by someone else (its control disposed, or the presenter disposed directly).</summary>
    public class PageStackExternalReleaseTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeWorkspaceView _view = new FakeWorkspaceView();
        private readonly List<string> _log = new List<string>();

        private PresenterTestHost Start(out WorkspacePresenter workspace)
        {
            _view.Log = _log;
            workspace = new WorkspacePresenter(_log);
            return PresenterTestHost.Start(_fakes.Provider, workspace, _view);
        }

        [Fact]
        public void ExternalRelease_DropsEntryAndAbove_RemoveViewTopFirst_IncludingDisposed()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log);
                var c = new GuardedPage("C", _log);
                ws.Push(a);
                ws.Push(b);
                ws.Push(c);

                b.Dispose();

                Assert.Equal(1, ws.Stack.Count);
                Assert.Equal(new[] { "C", "B" }, _view.Removed);
                Assert.True(c.Disposed);
                Assert.True(b.Disposed);
                Assert.False(a.Disposed);
                Assert.Equal(0, a.AskCount + b.AskCount + c.AskCount);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public void RemoveView_ReceivesDisposedView_IsCalledAnyway()
        {
            using (Start(out var ws))
            {
                var a = new PlainPage("A", _log);
                ws.Push(a);

                a.Dispose();

                Assert.Equal(new[] { "A" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task ExternalRelease_DuringPendingCheck_CountsAsDiscarded()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log);
                var pending = new TaskCompletionSource<bool>();
                var c = new GuardedPage("C", _log) { Answer = () => pending.Task };
                ws.Push(a);
                ws.Push(b);
                ws.Push(c);

                var jump = ws.Stack.PopToCountAsync(0);
                b.Dispose();                       // B (and C above it) go while C's guard is deciding
                Assert.Equal(1, ws.Stack.Count);
                pending.SetResult(true);

                Assert.True(await jump);
                Assert.Equal(0, ws.Stack.Count);
                Assert.Equal(new[] { "C", "B", "A" }, _view.Removed);
                Assert.Equal(0, b.AskCount);
                Assert.Equal(1, a.AskCount);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task NormalPop_NoDoubleRemove()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log));

                Assert.True(await ws.Stack.PopAsync());

                Assert.Equal(new[] { "B" }, _view.Removed);
                Assert.Equal(1, ws.Stack.Count);
            }
        }

        [Fact]
        public void OwnerTeardown_NoRemoveView_AlsoForNestedStacks()
        {
            var removedInner = new List<string>();
            var host = Start(out var ws);
            var a = new PlainPage("A", _log, ownsStack: true);
            ws.Push(a);
            var inner = new PlainPage("A1", _log);
            a.PushInner(inner, removedInner);

            host.Dispose();

            Assert.True(inner.Disposed);
            Assert.Empty(_view.Removed);
            Assert.Empty(removedInner);
        }

        [Fact]
        public void ExternalRelease_RemovalFailure_PropagatesToTheDisposer_PageStillGone()
        {
            using (Start(out var ws))
            {
                var a = new PlainPage("A", _log);
                var b = new PlainPage("B", _log);
                ws.Push(a);
                ws.Push(b);
                var failure = new InvalidOperationException("remove B");
                _view.ThrowOnRemove = failure;
                _view.ThrowOnRemoveTag = "B";

                var ex = Assert.Throws<InvalidOperationException>(() => b.Dispose());

                Assert.Same(failure, ex);
                Assert.True(b.Disposed);
                Assert.Equal(1, ws.Stack.Count);
            }
        }

        [Fact]
        public void ExternalRelease_InANestedStack_IsHandledByThatStack()
        {
            var removedInner = new List<string>();
            using (Start(out var ws))
            {
                var a = new PlainPage("A", _log, ownsStack: true);
                ws.Push(a);
                var a1 = new PlainPage("A1", _log);
                var a2 = new PlainPage("A2", _log);
                a.PushInner(a1, removedInner);
                a.PushInner(a2, removedInner);

                a1.Dispose();

                Assert.Equal(0, a.Inner.Count);
                Assert.Equal(new[] { "remove:A2", "remove:A1" }, removedInner);
                Assert.Equal(1, ws.Stack.Count);
                Assert.Empty(_view.Removed);
            }
        }

        [Theory]
        [InlineData("refuses")]
        [InlineData("cancelled")]
        public async Task ExternalRelease_DuringPendingCheck_TheDiscardedPagesAnswerDoesNotVeto(string answer)
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log);
                var pending = new TaskCompletionSource<bool>();
                var c = new GuardedPage("C", _log) { Answer = () => pending.Task };
                ws.Push(a);
                ws.Push(b);
                ws.Push(c);

                var jump = ws.Stack.PopToCountAsync(0);
                b.Dispose();
                if (answer == "refuses") pending.SetResult(false); else pending.SetCanceled();

                Assert.True(await jump);
                Assert.Equal(0, ws.Stack.Count);
                Assert.Equal(1, a.AskCount);
            }
        }

        [Fact]
        public void PageDisposingItselfDuringPush_ItsViewIsRemovedOnce()
        {
            using (Start(out var ws))
            {
                var page = new PlainPage("A", _log);
                page.OnInit = p => p.Dispose();

                ws.Push(page);

                Assert.Equal(new[] { "A" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public void PushedPageDisposesALowerPage_ThenFails_ItsViewIsRemovedOnce()
        {
            using (Start(out var ws))
            {
                var a = new PlainPage("A", _log);
                ws.Push(a);
                var boom = new InvalidOperationException("boom");
                var b = new PlainPage("B", _log);
                b.OnInit = p => { a.Dispose(); throw boom; };

                var ex = Assert.Throws<InvalidOperationException>(() => ws.Push(b));

                Assert.Same(boom, ex);
                Assert.Equal(new[] { "B", "A" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public void PageDisposingItselfDuringPush_TheRemovalCallbackStillSeesTheStackBusy()
        {
            using (Start(out var ws))
            {
                Exception reentrant = null;
                _view.OnRemove = tag =>
                {
                    try { ws.Push(new PlainPage("again", _log)); }
                    catch (Exception ex) { reentrant = ex; }
                };
                var page = new PlainPage("A", _log);
                page.OnInit = p => p.Dispose();

                ws.Push(page);

                Assert.IsType<InvalidOperationException>(reentrant);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public void ExternalRelease_TheReleasedPagesViewIsRemovedAfterItsOwnTeardown()
        {
            using (Start(out var ws))
            {
                ws.Push(new PlainPage("A", _log));
                var b = new PlainPage("B", _log);
                ws.Push(b);
                ws.Push(new PlainPage("C", _log));
                _log.Clear();

                b.Dispose();

                Assert.Equal(new[] { "dispose:C", "remove:C", "dispose:B", "remove:B" }, _log);
            }
        }

        [Fact]
        public void ReleasedHandles_DoNotKeepTheirOwnerAlive()
        {
            using (Start(out var ws))
            {
                var page = new PlainPage("A", _log);
                ws.Push(page);
                var handle = ((Kibinago.Presenters.IComposablePresenter)ws).Children.Single();

                page.Dispose();

                Assert.Null(handle.Owner);
            }
        }
    }
}
