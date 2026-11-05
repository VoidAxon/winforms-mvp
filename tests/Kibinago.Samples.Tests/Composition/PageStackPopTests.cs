using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    public class PageStackPopTests
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

        private static Exception ThrownAtCall(Func<Task<bool>> call)
        {
            try { call(); return null; }
            catch (Exception ex) { return ex; }
        }

        [Fact]
        public async Task PopTo_Bounds()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                ws.Push(a);

                // Argument errors are thrown at the call, not through the task.
                Assert.IsType<ArgumentOutOfRangeException>(ThrownAtCall(() => ws.Stack.PopToCountAsync(-1)));
                Assert.IsType<ArgumentOutOfRangeException>(ThrownAtCall(() => ws.Stack.PopToCountAsync(2)));
                Assert.True(await ws.Stack.PopToCountAsync(1));
                Assert.Equal(0, a.AskCount);
                Assert.Equal(1, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task PopAsync_Empty_ReturnsTrue()
        {
            using (Start(out var ws))
                Assert.True(await ws.Stack.PopAsync());
        }

        [Fact]
        public async Task Busy_Or_Dead_BeatsNoOp()
        {
            var host = Start(out var ws);
            var pending = new TaskCompletionSource<bool>();
            ws.Push(new GuardedPage("A", _log) { Answer = () => pending.Task });

            var jump = ws.Stack.PopAsync();
            Assert.False(await ws.Stack.PopToCountAsync(ws.Stack.Count));
            Assert.False(await ws.Stack.PopToCountAsync(-1));   // busy is answered before the bounds
            pending.SetResult(false);
            Assert.False(await jump);

            host.Dispose();
            Assert.False(await ws.Stack.PopAsync());   // dead, although the stack is empty now
            Assert.False(await ws.Stack.PopToCountAsync(0));
        }

        [Fact]
        public async Task Pop_DisposesThenRemoves_TopFirst()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log));
                ws.Push(new GuardedPage("C", _log));
                _log.Clear();

                Assert.True(await ws.Stack.PopToCountAsync(1));

                Assert.Equal(new[] { "ask:C", "ask:B", "dispose:C", "remove:C", "dispose:B", "remove:B" }, _log);
                Assert.Equal(1, ws.Stack.Count);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task Pop_RemovalFailures_ContinueAndAggregate()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log);
                var cleanupFailure = new InvalidOperationException("cleanup C");
                var c = new GuardedPage("C", _log) { ThrowOnCleanup = cleanupFailure };
                ws.Push(a);
                ws.Push(b);
                ws.Push(c);
                var removeFailure = new InvalidOperationException("remove B");
                _view.ThrowOnRemove = removeFailure;
                _view.ThrowOnRemoveTag = "B";

                var ex = await Assert.ThrowsAsync<AggregateException>(() => ws.Stack.PopToCountAsync(0));

                Assert.Equal(new Exception[] { cleanupFailure, removeFailure }, ex.InnerExceptions);
                Assert.True(a.Disposed && b.Disposed && c.Disposed);
                Assert.Equal(new[] { "C", "B", "A" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task Pop_SingleRemovalFailure_RethrownAsIs()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var removeFailure = new InvalidOperationException("remove A");
                _view.ThrowOnRemove = removeFailure;

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ws.Stack.PopAsync());

                Assert.Same(removeFailure, ex);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task Pop_RemoveViewThatPopsAgain_IsRejected_NothingRemovedTwice()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log));
                ws.Push(new GuardedPage("C", _log));
                Task<bool> reentrant = null;
                // A view whose removal triggers another jump (a crumb click raised while the control goes away).
                _view.OnRemove = tag => reentrant = reentrant ?? ws.Stack.PopToCountAsync(0);

                Assert.True(await ws.Stack.PopToCountAsync(2));

                Assert.False(await reentrant);
                Assert.Equal(new[] { "C" }, _view.Removed);
                Assert.Equal(2, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task CanCloseAllAsync_AsksAll_RemovesNothing()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log);
                ws.Push(a);
                ws.Push(b);

                Assert.True(await ws.Stack.CanCloseAllAsync());

                Assert.Equal(new[] { "ask:B", "ask:A" }, _log.Where(l => l.StartsWith("ask:")));
                Assert.Equal(2, ws.Stack.Count);
                Assert.Empty(_view.Removed);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task CanCloseAllAsync_Empty_ReturnsTrue()
        {
            using (Start(out var ws))
                Assert.True(await ws.Stack.CanCloseAllAsync());
        }

        [Fact]
        public async Task Breadcrumb_ViaTypedViewAction()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log) { Answer = () => Task.FromResult(false) });

                await _view.Binder.DispatchAsync(WorkspaceActions.Breadcrumb, 0);
                Assert.Equal(2, ws.Stack.Count);

                ws.Push(new PlainPage("C", _log));
                await _view.Binder.DispatchAsync(WorkspaceActions.Breadcrumb, 1);   // C has no guard
                Assert.Equal(2, ws.Stack.Count);
            }
        }
    }
}
