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
            Assert.Throws<ArgumentNullException>(() => new PageStack(null));
            Assert.Throws<ArgumentException>(() => new PageStack(new ForeignPresenter()));
            var disposed = new PlainPage("x", null);
            disposed.Dispose();
            Assert.Throws<ObjectDisposedException>(() => new PageStack(disposed));
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
            bool removed = false;

            Assert.Throws<InvalidOperationException>(
                () => workspace.Stack.Push(page, new FakePageView("A"), v => removed = true));

            Assert.False(removed);
            Assert.False(page.Disposed);
            Assert.Equal(0, workspace.Stack.Count);
        }

        [Fact]
        public void Push_Preflight_ViewClaimedByAnotherPresenter_DoesNotRemoveOrDispose()
        {
            using (Start(out var workspace))
            {
                var shared = new FakePageView("shared");
                var first = new PlainPage("first", _log);
                workspace.Stack.Push(first, shared, _view.RemovePage);
                var second = new PlainPage("second", _log);

                Assert.Throws<InvalidOperationException>(() => workspace.Stack.Push(second, shared, _view.RemovePage));

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
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push<IPageView>(null, new FakePageView("v"), _view.RemovePage));
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push(new PlainPage("A", _log), null, _view.RemovePage));
                Assert.Throws<ArgumentNullException>(() => workspace.Stack.Push(new PlainPage("A", _log), new FakePageView("v"), null));
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
        public void Push_ReentrantFromPageOnInitialize_Throws()
        {
            using (Start(out var workspace))
            {
                Exception inner = null;
                var innerPage = new PlainPage("inner", _log);
                var outer = new PlainPage("outer", _log)
                {
                    OnInit = p =>
                    {
                        try { workspace.Push(innerPage); }
                        catch (Exception e) { inner = e; }
                    },
                };

                workspace.Push(outer);

                Assert.IsType<InvalidOperationException>(inner);
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
                var view = new FakePageView("detail");
                workspace.Stack.Push(detail, view,
                    new PageParams { Saved = tag => workspace.Push(new PlainPage("after:" + tag, _log)) }, _view.RemovePage);

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
