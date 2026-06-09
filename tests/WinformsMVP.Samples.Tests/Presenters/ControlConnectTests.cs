using System;
using System.Windows.Forms;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
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

        public interface IFakeControlView : IViewBase { }

        /// <summary>A view that is NOT a Control — used to drive the presenter without a UI handle.</summary>
        private sealed class NonControlView : IFakeControlView { }

        /// <summary>A Control-backed view — required by the <c>Connect</c> hosting path.</summary>
        private sealed class ControlBackedView : UserControl, IFakeControlView { }

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

            protected override void OnViewAttached() { }

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
            presenter.AttachView(new NonControlView());

            presenter.Initialize();

            Assert.Equal("ready", presenter.Observed);
        }

        [Fact]
        public void Initialize_Twice_Throws()
        {
            var presenter = new DepUsingPresenter(new FakeDependency());
            presenter.AttachView(new NonControlView());
            presenter.Initialize();

            Assert.Throws<InvalidOperationException>(() => presenter.Initialize());
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
    }
}
