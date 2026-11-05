using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    public class PageStackPushTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeWorkspaceView _view = new FakeWorkspaceView();
        private readonly List<string> _log = new List<string>();

        private PresenterTestHost Start(out WorkspacePresenter workspace, Action<WorkspacePresenter> onInit = null)
        {
            workspace = new WorkspacePresenter(_log) { OnInit = onInit };
            return PresenterTestHost.Start(_fakes.Provider, workspace, _view);
        }

        [Fact]
        public void Ctor_Rejects()
        {
            var host = new TestPageHost();
            Assert.Throws<ArgumentNullException>(() => new PageStack(null, () => host));
            Assert.Throws<ArgumentNullException>(() => new PageStack(new PlainPage("y", null), null));
            Assert.Throws<ArgumentException>(() => new PageStack(new ForeignPresenter(), () => host));
            var disposed = new PlainPage("x", null);
            disposed.Dispose();
            Assert.Throws<ObjectDisposedException>(() => new PageStack(disposed, () => host));
        }

        [Fact]
        public void Push_AttachesInitializesAndCounts()
        {
            using (Start(out var workspace))
            {
                bool usedOwnersServices = false;
                var a = new PlainPage("A", _log) { OnInit = p => usedOwnersServices = ReferenceEquals(p.GetMessagesForTest(), _fakes.Messages) };
                workspace.Push(a);
                workspace.Push(new PlainPage("B", _log));

                Assert.Equal(2, workspace.Stack.Count);
                Assert.True(usedOwnersServices);
                Assert.False(workspace.Stack.IsBusy);
            }
        }

        [Fact]
        public void Push_BeforeOwnerView_Throws_InvalidOperation_AndCallsNoRemove()
        {
            var workspace = new WorkspacePresenter(_log);
            var page = new PlainPage("A", _log);

            Assert.Throws<InvalidOperationException>(() => workspace.Stack.Push(page, "A"));

            Assert.Empty(_view.Added);   // nothing created
            Assert.False(page.Disposed);
            Assert.Equal(0, workspace.Stack.Count);
        }

        [Fact]
        public void Push_HostReturnsAViewClaimedByAnotherPresenter_NotRemoved_NothingDisposed()
        {
            using (Start(out var workspace))
            {
                var first = new PlainPage("first", _log);
                workspace.Push(first);
                var second = new PlainPage("second", _log);
                _view.NextView = _view.Added[0];   // a host breaking its contract: another page's live view

                Assert.Throws<InvalidOperationException>(() => workspace.Push(second));

                Assert.Empty(_view.Removed);
                Assert.False(first.Disposed);
                Assert.False(second.Disposed);
                Assert.Equal(1, workspace.Stack.Count);
            }
        }

        [Fact]
        public void Push_NullArguments_Throw()
        {
            using (Start(out var workspace))
            {
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push<IPageView>(null, "t"));
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push(new PlainPage("A", _log), null));
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push<IPageView, PageParams>(null, new PageParams(), "t"));
                Assert.Empty(_view.Added);
                Assert.Empty(_view.Removed);
            }
        }

        [Fact]
        public void Push_FailureAfterAcceptance_RemovesViewOnce()
        {
            using (Start(out var workspace))
            {
                var boom = new InvalidOperationException("boom");
                var page = new PlainPage("A", _log) { ThrowOnInitialize = boom };

                var ex = Assert.Throws<InvalidOperationException>(() => workspace.Push(page));

                Assert.Same(boom, ex);
                Assert.Equal(new[] { "A" }, _view.Removed);
                Assert.Equal(0, workspace.Stack.Count);
                Assert.True(page.Disposed);
                Assert.False(workspace.Stack.IsBusy);
            }
        }

        [Fact]
        public void Push_RemoveViewThrowsAfterFailure_Aggregates()
        {
            using (Start(out var workspace))
            {
                var boom = new InvalidOperationException("boom");
                var removeFailure = new InvalidOperationException("remove");
                _view.ThrowOnRemove = removeFailure;
                var page = new PlainPage("A", _log) { ThrowOnInitialize = boom };

                var ex = Assert.Throws<AggregateException>(() => workspace.Push(page));

                Assert.Equal(new Exception[] { boom, removeFailure }, ex.InnerExceptions);
                Assert.Equal(0, workspace.Stack.Count);
            }
        }

        [Fact]
        public void Push_ReentrantFromPageOnInitialize_ReturnsFalse()
        {
            using (Start(out var workspace))
            {
                bool? inner = null;
                var innerPage = new PlainPage("inner", _log);
                var outer = new PlainPage("outer", _log) { OnInit = p => inner = workspace.Push(innerPage) };

                Assert.True(workspace.Push(outer));

                Assert.False(inner);
                Assert.False(innerPage.Disposed);
                Assert.Equal(1, workspace.Stack.Count);
                Assert.Empty(_view.Removed);
            }
        }

        [Fact]
        public async Task Pop_ReentrantFromPageOnInitialize_ReturnsFalse()
        {
            using (Start(out var workspace))
            {
                workspace.Push(new PlainPage("A", _log));
                Task<bool> inner = null;
                workspace.Push(new PlainPage("B", _log) { OnInit = p => inner = workspace.Stack.PopAsync() });

                Assert.False(await inner);
                Assert.Equal(2, workspace.Stack.Count);
            }
        }

        [Fact]
        public void Push_PageOwnStack_IsUnaffectedByTheOuterPush()
        {
            using (Start(out var workspace))
            {
                var nested = new PlainPage("nested", _log);
                var outer = new PlainPage("outer", _log, ownsStack: true) { OnInit = p => p.PushInner(nested, null) };

                workspace.Push(outer);

                Assert.Equal(1, outer.Inner.Count);
            }
        }

        [Fact]
        public void Push_FromSavedCallbackWhileIdle_Works()
        {
            using (Start(out var workspace))
            {
                var detail = new ParamPage();
                workspace.Push(detail, "detail", new PageParams { Saved = tag => workspace.Push(new PlainPage("after:" + tag, _log)) });
                var view = (FakePageView)_view.Added[0];

                view.Binder.Dispatch(PageActions.Save);

                Assert.Equal(2, workspace.Stack.Count);
            }
        }

        [Fact]
        public async Task OwnerDisposed_DisposesPagesWithoutRemoveView_StackDead()
        {
            var host = Start(out var workspace);
            var a = new PlainPage("A", _log);
            var b = new PlainPage("B", _log);
            workspace.Push(a);
            workspace.Push(b);

            host.Dispose();

            Assert.True(a.Disposed);
            Assert.True(b.Disposed);
            Assert.Empty(_view.Removed);
            Assert.Throws<ObjectDisposedException>(() => workspace.Push(new PlainPage("C", _log)));
            Assert.False(await workspace.Stack.PopAsync());
            Assert.False(await workspace.Stack.PopToCountAsync(0));
            Assert.False(await workspace.Stack.CanCloseAllAsync());
        }

        [Fact]
        public void Pages_AreChildrenOfTheOwner()
        {
            using (Start(out var workspace))
            {
                workspace.Push(new PlainPage("A", _log));
                Assert.Single(((IComposablePresenter)workspace).Children);
                Assert.Equal(new[] { workspace.Stack }, ((IComposablePresenter)workspace).OwnedStacks);
            }
        }
    }
}
