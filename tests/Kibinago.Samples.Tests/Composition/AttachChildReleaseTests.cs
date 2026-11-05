using System;
using System.Collections.Generic;
using Kibinago.Presenters;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>The internal seams PageStack builds on: the post-transfer hook and release kinds.</summary>
    public class AttachChildReleaseTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeEditView _view = new FakeEditView();
        private readonly EditInput _input = new EditInput { Name = "initial" };

        private PresenterTestHost Start(out EditPresenter parent)
        {
            parent = new EditPresenter((p, input) => { });
            return PresenterTestHost.Start(_fakes.Provider, parent, _view, _input);
        }

        private ChildAttachment AttachInternal(EditPresenter parent, NamePanelPresenter child,
            Action<ChildAttachment> beforeInitialize)
            => ((IComposablePresenter)parent).AttachChildInternal(child, _view.NamePanel,
                () =>
                {
                    ((IViewAttacher<INamePanelView>)child).AttachView(_view.NamePanel);
                    ((IInitializable<EditInput>)child).Initialize(_input);
                },
                beforeInitialize);

        [Fact]
        public void PostTransferFailure_InBeforeInitialize_DisposesChildAndRethrows()
        {
            using (Start(out var parent))
            {
                var child = new NamePanelPresenter(new NameSource());
                var boom = new InvalidOperationException("boom");
                ChildAttachment seen = null;

                var ex = Assert.Throws<InvalidOperationException>(
                    () => AttachInternal(parent, child, a => { seen = a; throw boom; }));

                Assert.Same(boom, ex);
                Assert.NotNull(seen);
                Assert.True(child.Disposed);
                Assert.Empty(((IComposablePresenter)parent).Children);
                Assert.False(ViewOwnership.IsClaimed(_view.NamePanel));
            }
        }

        [Fact]
        public void BeforeInitialize_RunsAfterOwnershipTransfer_BeforeTheChildsCode()
        {
            using (Start(out var parent))
            {
                var child = new NamePanelPresenter(new NameSource());
                bool ownedWhenCalled = false, claimedWhenCalled = false, shownWhenCalled = true;

                var attachment = AttachInternal(parent, child, a =>
                {
                    ownedWhenCalled = ReferenceEquals(((IComposablePresenter)child).OwnerLink, a);
                    claimedWhenCalled = ViewOwnership.IsClaimed(_view.NamePanel);
                    shownWhenCalled = _view.NamePanel.Displayed != null;
                });

                Assert.True(ownedWhenCalled);
                Assert.True(claimedWhenCalled);
                Assert.False(shownWhenCalled);
                Assert.Equal(new[] { attachment }, ((IComposablePresenter)parent).Children);
            }
        }

        [Fact]
        public void Release_ViaHandle_IsExplicit()
        {
            using (Start(out var parent))
            {
                var kinds = new List<ChildReleaseKind>();
                var attachment = AttachInternal(parent, new NamePanelPresenter(new NameSource()),
                    a => a.Released = (_, k) => kinds.Add(k));

                attachment.Dispose();
                attachment.Dispose();

                Assert.Equal(new[] { ChildReleaseKind.Explicit }, kinds);
            }
        }

        [Fact]
        public void Release_ChildDisposedDirectly_IsExternal_BeforeTheChildsTeardown()
        {
            using (Start(out var parent))
            {
                var log = new List<string>();
                var child = new NamePanelPresenter(new NameSource(), log, "child");
                AttachInternal(parent, child, a => a.Released = (_, k) => log.Add("released:" + k));

                child.Dispose();

                Assert.Equal(new[] { "released:External", "cleanup:child" }, log);
                Assert.Empty(((IComposablePresenter)parent).Children);
            }
        }

        [Fact]
        public void Release_OwnerDisposed_IsOwnerTeardown()
        {
            var kinds = new List<ChildReleaseKind>();
            var host = Start(out var parent);
            AttachInternal(parent, new NamePanelPresenter(new NameSource()), a => a.Released = (_, k) => kinds.Add(k));

            host.Dispose();

            Assert.Equal(new[] { ChildReleaseKind.OwnerTeardown }, kinds);
        }

        [Fact]
        public void Release_HandlerThrows_ChildStillDisposed_FailurePropagates()
        {
            using (Start(out var parent))
            {
                var child = new NamePanelPresenter(new NameSource());
                var boom = new InvalidOperationException("handler");
                var attachment = AttachInternal(parent, child, a => a.Released = (_, k) => throw boom);

                var ex = Assert.Throws<InvalidOperationException>(() => attachment.Dispose());

                Assert.Same(boom, ex);
                Assert.True(child.Disposed);
                Assert.Empty(((IComposablePresenter)parent).Children);
            }
        }

        [Fact]
        public void Children_IsASnapshot()
        {
            using (Start(out var parent))
            {
                var a = AttachInternal(parent, new NamePanelPresenter(new NameSource()), _ => { });
                var snapshot = ((IComposablePresenter)parent).Children;
                a.Dispose();

                Assert.Single(snapshot);
                Assert.Empty(((IComposablePresenter)parent).Children);
            }
        }
    }
}
