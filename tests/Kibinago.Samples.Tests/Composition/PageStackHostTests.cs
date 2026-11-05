using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.Testing;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>Push through an IPageHost: one transaction from the host lookup to the page on top.</summary>
    public class PageStackHostTests
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

        private sealed class OtherView : IControlView
        {
            public Kibinago.ViewActions.IViewActionBinder ActionBinder => null;
        }

        [Fact]
        public void PushReturnsTrue_PageOnTop_TitleGivenToTheHost()
        {
            using (Start(out var ws))
            {
                Assert.True(ws.Stack.Push(new PlainPage("A", _log), "顧客一覧"));

                Assert.Equal(1, ws.Stack.Count);
                Assert.Equal("顧客一覧", ((FakePageView)Assert.Single(_view.Added)).Tag);
            }
        }

        [Fact]
        public async Task Busy_PushReturnsFalse_CreatesNothing_PageUntouched()
        {
            using (Start(out var ws))
            {
                var pending = new TaskCompletionSource<bool>();
                ws.Push(new GuardedPage("A", _log) { Answer = () => pending.Task });
                var jump = ws.Stack.PopAsync();
                var page = new PlainPage("B", _log);

                Assert.False(ws.Stack.Push(page, "B"));

                Assert.Single(_view.Added);
                Assert.False(page.Disposed);
                Assert.Equal(1, ws.Stack.Count);
                pending.SetResult(false);
                Assert.False(await jump);
                Assert.True(ws.Stack.Push(page, "B"));   // the same presenter can be pushed later
            }
        }

        [Fact]
        public void HostLookedUpOnce_OnFirstPush()
        {
            using (Start(out var _))
            {
                int lookups = 0;
                var owner = new PlainPage("owner", _log);
                var host = new TestPageHost();
                var stack = new PageStack(owner, () => { lookups++; return host; });
                Assert.Equal(0, lookups);   // not read in the constructor
            }

            var calls = 0;
            var ws2 = new LookupCountingWorkspace(() => calls++);
            using (PresenterTestHost.Start(_fakes.Provider, ws2, new FakeWorkspaceView()))
            {
                ws2.Push(new PlainPage("A", _log));
                ws2.Push(new PlainPage("B", _log));
                Assert.Equal(1, calls);
            }
        }

        private sealed class LookupCountingWorkspace : WindowPresenterBase<IWorkspaceView>
        {
            private readonly PageStack _stack;
            public LookupCountingWorkspace(Action onLookup) { _stack = new PageStack(this, () => { onLookup(); return View.Pages; }); }
            internal void Push(PlainPage page) => _stack.Push(page, page.Tag);
        }

        [Fact]
        public void HostNull_Throws_InvalidOperation_PageUntouched()
        {
            var ws = new NullHostWorkspace();
            using (PresenterTestHost.Start(_fakes.Provider, ws, new FakeWorkspaceView()))
            {
                var page = new PlainPage("A", _log);
                Assert.Throws<InvalidOperationException>(() => ws.Push(page));
                Assert.False(page.Disposed);
            }
        }

        private sealed class NullHostWorkspace : WindowPresenterBase<IWorkspaceView>
        {
            private readonly PageStack _stack;
            public NullHostWorkspace() { _stack = new PageStack(this, () => null); }
            internal void Push(PlainPage page) => _stack.Push(page, page.Tag);
        }

        [Fact]
        public void TitleEmpty_Allowed()
        {
            using (Start(out var ws))
                Assert.True(ws.Stack.Push(new PlainPage("A", _log), ""));
        }

        [Fact]
        public void AddPageThrows_Propagates_CountUnchanged_NoRemove_PageUntouched()
        {
            using (Start(out var ws))
            {
                ws.Push(new PlainPage("A", _log));
                var boom = new InvalidOperationException("no mapping");
                _view.ThrowOnAdd = boom;
                var page = new PlainPage("B", _log);

                Assert.Same(boom, Assert.Throws<InvalidOperationException>(() => ws.Push(page)));

                Assert.Equal(1, ws.Stack.Count);
                Assert.Empty(_view.Removed);
                Assert.False(page.Disposed);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public void ViewOfWrongType_RemovedOnce_PresenterUntouched_Throws()
        {
            using (Start(out var ws))
            {
                _view.NextView = new OtherView();
                var page = new PlainPage("A", _log);

                Assert.Throws<InvalidOperationException>(() => ws.Push(page));

                Assert.Equal(new[] { "OtherView" }, _view.Removed);
                Assert.False(page.Disposed);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public void PageRejectedAfterTheViewWasMade_ViewRemovedOnce_PresenterUntouched()
        {
            using (Start(out var ws))
            {
                // A page that resolved other framework services in its constructor cannot inherit the owner's.
                var page = new ServicesInCtorPage();

                Assert.Throws<InvalidOperationException>(() => ws.Stack.Push(page, "P"));

                Assert.Equal(new[] { "P" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        private sealed class ServicesInCtorPage : ControlPresenterBase<IPageView>
        {
            public ServicesInCtorPage() { var _ = Services; }   // fixes the ambient provider (a mistake), as in AttachChildTests
        }

        [Fact]
        public void PageDisposesItselfDuringInit_ReturnsFalse_ViewRemovedOnce()
        {
            using (Start(out var ws))
            {
                var page = new PlainPage("A", _log) { OnInit = p => p.Dispose() };

                Assert.False(ws.Push(page));

                Assert.Equal(new[] { "A" }, _view.Removed);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public void OwnerDisposedDuringInit_ReturnsFalse_NothingRemoved()
        {
            var host = Start(out var ws);
            var page = new PlainPage("A", _log) { OnInit = p => host.Dispose() };

            Assert.False(ws.Push(page));

            Assert.Empty(_view.Removed);
            Assert.True(page.Disposed);
        }

        [Fact]
        public async Task ReentrancyFromTheHost_DuringAddPage_IsRejected()
        {
            using (Start(out var ws))
            {
                ws.Push(new PlainPage("A", _log));
                bool? innerPush = null;
                Task<bool> innerPop = null;
                _view.OnAdd = () =>
                {
                    _view.OnAdd = null;
                    innerPush = ws.Stack.Push(new PlainPage("X", _log), "X");
                    innerPop = ws.Stack.PopAsync();
                };

                Assert.True(ws.Push(new PlainPage("B", _log)));

                Assert.False(innerPush);
                Assert.False(await innerPop);
                Assert.Equal(2, ws.Stack.Count);
            }
        }

        [Fact]
        public void PageWithoutOwnServices_HostReceivesTheOwners()
        {
            using (Start(out var ws))
            {
                ws.Push(new PlainPage("A", _log));
                Assert.Same(_fakes.Provider, _view.LastServices);
            }
        }

        [Fact]
        public void PageWithOwnServices_HostReceivesThem()
        {
            using (Start(out var ws))
            {
                var own = TestServices.Create();
                var page = new PlainPage("A", _log);
                ((IPresenterServicesBinding)page).BindServices(own);

                Assert.True(ws.Push(page));

                Assert.Same(own, _view.LastServices);
            }
        }

        [Fact]
        public void HostLookupThatPushes_IsRejected_TheStackIsAlreadyReserved()
        {
            var ws = new ReentrantLookupWorkspace();
            using (PresenterTestHost.Start(_fakes.Provider, ws, new FakeWorkspaceView()))
            {
                Assert.True(ws.Push(new PlainPage("A", _log)));
                Assert.False(ws.InnerResult);
                Assert.Equal(1, ws.Count);
            }
        }

        private sealed class ReentrantLookupWorkspace : WindowPresenterBase<IWorkspaceView>
        {
            private readonly PageStack _stack;
            public bool? InnerResult;
            public ReentrantLookupWorkspace()
            {
                _stack = new PageStack(this, () =>
                {
                    if (InnerResult == null) InnerResult = _stack.Push(new PlainPage("inner", null), "inner");
                    return View.Pages;
                });
            }
            internal bool Push(PlainPage page) => _stack.Push(page, page.Tag);
            internal int Count => _stack.Count;
        }

        [Fact]
        public void HostReturnsAnotherPresentersViewOfAnotherType_NotRemoved()
        {
            using (Start(out var ws))
            {
                var foreign = new OtherView();
                using (ViewOwnership.Claim(foreign, new object()))
                {
                    _view.NextView = foreign;
                    var page = new PlainPage("A", _log);

                    Assert.Throws<InvalidOperationException>(() => ws.Push(page));

                    Assert.Empty(_view.Removed);   // not ours to remove
                    Assert.False(page.Disposed);
                }
            }
        }
    }
}
