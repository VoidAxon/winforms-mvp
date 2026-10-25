using System;
using System.Collections.Generic;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    public class AttachChildTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeEditView _view = new FakeEditView();
        private readonly EditInput _input = new EditInput { Name = "initial" };

        private EditPresenter _parent;

        private PresenterTestHost Start(Action<EditPresenter, EditInput> compose, List<string> log = null)
        {
            _parent = new EditPresenter(compose, log);
            return PresenterTestHost.Start(_fakes.Provider, _parent, _view, _input);
        }

        [Fact]
        public void Fixed_ChildIsInitializedWithItsParameter_AndItsActionsRun()
        {
            using (Start((p, input) => p.Attach(new NamePanelPresenter(new NameSource()), _view.NamePanel, input)))
            {
                Assert.Equal("initial", _view.NamePanel.Displayed);

                _view.NamePanel.Binder.Dispatch(PanelActions.Fill);

                Assert.Equal("Alice", _input.Name);
            }
        }

        [Fact]
        public void Child_InheritsTheParentsServices()
        {
            var child = new NamePanelPresenter(new NameSource());
            using (Start((p, input) => p.Attach(child, _view.NamePanel, input)))
            {
                _view.NamePanel.Binder.Dispatch(PanelActions.Fill);
                Assert.Same(_fakes.Messages, child.SeenMessages);
            }
        }

        [Fact]
        public void Child_WithExplicitServices_KeepsThem()
        {
            var own = TestServices.CreateFakes();
            var child = new NamePanelPresenter(new NameSource());
            ((Kibinago.Presenters.IPresenterServicesBinding)child).BindServices(own.Provider);
            using (Start((p, input) => p.Attach(child, _view.NamePanel, input)))
            {
                _view.NamePanel.Binder.Dispatch(PanelActions.Fill);
                Assert.Same(own.Messages, child.SeenMessages);
            }
        }

        [Fact]
        public void Child_ThatResolvedOtherServicesInItsConstructor_IsRejected()
        {
            var child = new NamePanelPresenter(new NameSource(), useServicesInConstructor: true);

            var ex = Assert.Throws<InvalidOperationException>(
                () => Start((p, input) => p.Attach(child, _view.NamePanel, input)));
            Assert.Contains("constructor", ex.Message);
        }

        [Fact]
        public void Preflight_Rejections_LeaveTheChildUntouched()
        {
            var child = new NamePanelPresenter(new NameSource());
            using (var host = Start((p, input) => { }))
            {
                var parent = GetParent(host);
                Assert.Throws<ArgumentNullException>(() => parent.Attach(null, _view.NamePanel, _input));
                Assert.Throws<ArgumentNullException>(() => parent.Attach(child, null, _input));

                parent.Attach(child, _view.NamePanel, _input);
                Assert.Throws<InvalidOperationException>(() => parent.Attach(child, _view.OtherPanel, _input));
                Assert.Throws<InvalidOperationException>(
                    () => parent.Attach(new NamePanelPresenter(new NameSource()), _view.NamePanel, _input));
                Assert.False(child.Disposed);
            }
        }

        [Fact]
        public void AfterTheParentIsDisposed_AttachThrows()
        {
            EditPresenter parent = null;
            var host = Start((p, input) => parent = p);
            host.Dispose();

            Assert.Throws<ObjectDisposedException>(
                () => parent.Attach(new NamePanelPresenter(new NameSource()), _view.NamePanel, _input));
        }

        [Fact]
        public void ADisposedChild_CannotBeAttached()
        {
            var child = new NamePanelPresenter(new NameSource());
            child.Dispose();
            Assert.Throws<ObjectDisposedException>(() => Start((p, input) => p.Attach(child, _view.NamePanel, input)));
        }

        [Fact]
        public void ChildInitializeThrows_ChildDisposed_ParentStaysUsable()
        {
            var failing = new NamePanelPresenter(new NameSource()) { ThrowOnInitialize = new InvalidOperationException("boom") };
            using (var host = Start((p, input) => { }))
            {
                var parent = GetParent(host);
                var ex = Assert.Throws<InvalidOperationException>(() => parent.Attach(failing, _view.NamePanel, _input));
                Assert.Equal("boom", ex.Message);
                Assert.True(failing.Disposed);

                // The view is free again; another child can use it.
                parent.Attach(new NamePanelPresenter(new NameSource()), _view.NamePanel, _input);
                _view.NamePanel.Binder.Dispatch(PanelActions.Fill);
                Assert.Equal("Alice", _input.Name);
            }
        }

        [Fact]
        public void SecondChildFails_DuringParentInitialize_EverythingIsDisposed()
        {
            var first = new NamePanelPresenter(new NameSource());
            var second = new NamePanelPresenter(new NameSource()) { ThrowOnInitialize = new InvalidOperationException("boom") };

            Assert.Throws<InvalidOperationException>(() => Start((p, input) =>
            {
                p.Attach(first, _view.NamePanel, input);
                p.Attach(second, _view.OtherPanel, input);
            }));

            Assert.True(first.Disposed);
            Assert.True(second.Disposed);
        }

        [Fact]
        public void ParentDispose_Order_CleanupThenChildrenInReverseThenDisposables()
        {
            var log = new List<string>();
            var host = Start((p, input) =>
            {
                p.Attach(new NamePanelPresenter(new NameSource(), log, "a"), _view.NamePanel, input);
                p.Attach(new NamePanelPresenter(new NameSource(), log, "b"), _view.OtherPanel, input);
            }, log);

            host.Dispose();

            Assert.Equal(new[] { "cleanup:parent", "cleanup:b", "cleanup:a", "disposables:parent" }, log);
        }

        [Fact]
        public void ParentDispose_AChildCleanupThrows_OtherChildrenStillDisposed_FailureRethrown()
        {
            var a = new NamePanelPresenter(new NameSource());
            var b = new NamePanelPresenter(new NameSource()) { ThrowOnCleanup = new InvalidOperationException("cleanup failed") };
            var host = Start((p, input) => { p.Attach(a, _view.NamePanel, input); p.Attach(b, _view.OtherPanel, input); });

            var ex = Assert.Throws<InvalidOperationException>(() => host.Dispose());

            Assert.Equal("cleanup failed", ex.Message);
            Assert.True(a.Disposed);
        }

        [Fact]
        public void WhileTheParentIsDisposing_ChildActionsAreAlreadyInert()
        {
            var child = new NamePanelPresenter(new NameSource());
            var host = Start((p, input) =>
            {
                p.Attach(child, _view.NamePanel, input);
                p.OnCleanup = () => _view.NamePanel.Binder.Dispatch(PanelActions.Fill);   // a late click during teardown
            });

            host.Dispose();

            Assert.Equal("initial", _input.Name);
        }

        [Fact]
        public void AViewOwnedByAnotherParentsChild_IsRejected_UntilReleased()
        {
            var shared = new FakeNamePanelView();
            var otherView = new FakeEditView();
            IDisposable firstHandle = null;
            using (PresenterTestHost.Start(_fakes.Provider,
                       new EditPresenter((p, input) => firstHandle = p.Attach(new NamePanelPresenter(new NameSource()), shared, input)),
                       otherView, new EditInput()))
            using (var host = Start((p, input) => { }))
            {
                var parent = GetParent(host);
                Assert.Throws<InvalidOperationException>(
                    () => parent.Attach(new NamePanelPresenter(new NameSource()), shared, _input));

                firstHandle.Dispose();
                parent.Attach(new NamePanelPresenter(new NameSource()), shared, _input);   // free again
            }
        }

        private EditPresenter GetParent(PresenterTestHost host) => _parent;
    }
}
