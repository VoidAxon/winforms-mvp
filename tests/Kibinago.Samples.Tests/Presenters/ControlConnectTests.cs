using System;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// Covers the two-phase construction of <see cref="ControlPresenterBase{TView}"/> and its
    /// <c>Connect</c> hosting path. The headline guarantee is the regression test
    /// <see cref="OnInitialize_SeesConstructorInjectedDependency"/>: a constructor-injected
    /// dependency used inside <c>OnInitialize</c> used to NRE, because the old base invoked
    /// <c>OnInitialize</c> from its constructor — before the derived constructor body had assigned
    /// the field. Initialization now runs after construction, so the field is observed.
    /// </summary>
    public class ControlConnectTests
    {
        public interface IDependency { string GetValue(); }

        private sealed class FakeDependency : IDependency
        {
            public string GetValue() => "ready";
        }

        public interface IFakeControlView : IControlView { }

        /// <summary>A view that is NOT a Control — used to drive the presenter without a UI handle.</summary>
        private sealed class NonControlView : IFakeControlView { public IViewActionBinder ActionBinder => null; }

        /// <summary>A Control-backed view — required by the <c>Connect</c> hosting path.</summary>
        private sealed class ControlBackedView : UserControl, IFakeControlView { public IViewActionBinder ActionBinder => null; }

        /// <summary>
        /// Reproduces the exact shape that crashed: a dependency injected through the constructor
        /// and dereferenced inside <c>OnInitialize</c>.
        /// </summary>
        private sealed class DepUsingPresenter : ControlPresenterBase<IFakeControlView>
        {
            private readonly IDependency _dep;
            public string Observed;
            public bool CleanedUp;

            public DepUsingPresenter(IDependency dep)
            {
                _dep = dep ?? throw new ArgumentNullException(nameof(dep));
            }

            protected override void OnInitialize()
            {
                // Before the fix this threw NullReferenceException: _dep was still null because
                // OnInitialize ran inside the base constructor, ahead of the field assignment above.
                Observed = _dep.GetValue();
            }

            protected override void Cleanup()
            {
                CleanedUp = true;
                base.Cleanup();
            }
        }

        [Fact]
        public void OnInitialize_SeesConstructorInjectedDependency()
        {
            var presenter = new DepUsingPresenter(new FakeDependency());
            ((IViewAttacher<IFakeControlView>)presenter).AttachView(new NonControlView());

            ((IInitializable)presenter).Initialize();

            Assert.Equal("ready", presenter.Observed);
        }

        [Fact]
        public void Initialize_Twice_Throws()
        {
            var presenter = new DepUsingPresenter(new FakeDependency());
            PresenterTestHost.Start(presenter, new NonControlView());

            Assert.Throws<InvalidOperationException>(() => ((IInitializable)presenter).Initialize());
        }

        [Fact]
        public void Connect_AttachesAndInitializes_WithControlBackedView()
        {
            var view = new ControlBackedView();
            var presenter = new DepUsingPresenter(new FakeDependency());

            presenter.Connect(view);

            Assert.True(((IViewAttachable)presenter).IsViewAttached);
            Assert.Equal("ready", presenter.Observed);
            view.Dispose();
        }

        [Fact]
        public void Connect_NonControlView_Throws()
        {
            var presenter = new DepUsingPresenter(new FakeDependency());

            Assert.Throws<ArgumentException>(() => presenter.Connect(new NonControlView()));
        }

        [Fact]
        public void Connect_DisposingControl_TearsDownPresenter()
        {
            var view = new ControlBackedView();
            var presenter = new DepUsingPresenter(new FakeDependency());
            presenter.Connect(view);

            view.Dispose();

            Assert.True(presenter.CleanedUp);
        }

        private sealed class DisposingPresenter : ControlPresenterBase<IFakeControlView>
        {
            private readonly Action _duringInitialize;
            public bool CleanedUp;
            public DisposingPresenter(Action duringInitialize) => _duringInitialize = duringInitialize;
            protected override void OnInitialize() => _duringInitialize();
            protected override void Cleanup() => CleanedUp = true;
        }

        [Fact]
        public void Connect_DisposedControl_Throws_WithoutAttaching()
        {
            var view = new ControlBackedView();
            view.Dispose();
            var presenter = new DepUsingPresenter(new FakeDependency());

            Assert.Throws<ObjectDisposedException>(() => presenter.Connect(view));
            Assert.Null(presenter.Observed);
        }

        [Fact]
        public void Connect_ControlDisposedDuringInitialize_DisposesThePresenter()
        {
            var view = new ControlBackedView();
            var presenter = new DisposingPresenter(view.Dispose);   // the control goes away mid-initialization

            Assert.Throws<ObjectDisposedException>(() => presenter.Connect(view));
            Assert.True(presenter.CleanedUp);
        }
    }
}
