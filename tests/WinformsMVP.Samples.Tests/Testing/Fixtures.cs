using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Testing;

namespace WinformsMVP.Samples.Tests.Testing
{
    // Small presenters and views shared by the WinformsMVP.Testing tests (no WinForms types).

    public interface IFixtureView : IWindowView { }

    public sealed class FixtureView : IFixtureView
    {
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
    }

    public sealed class FixturePresenter : WindowPresenterBase<IFixtureView>
    {
        public bool Disposed { get; private set; }
        protected override void OnViewAttached() { }
        protected override void Cleanup() => Disposed = true;
    }

    public sealed class OtherFixturePresenter : WindowPresenterBase<IFixtureView>
    {
        public bool Disposed { get; private set; }
        protected override void OnViewAttached() { }
        protected override void Cleanup() => Disposed = true;
    }

    /// <summary>Implements only <c>IInitializable&lt;int&gt;</c>: a parameterless show must throw.</summary>
    public sealed class ParamFixturePresenter : WindowPresenterBase<IFixtureView, int>
    {
        public bool Disposed { get; private set; }
        protected override void OnViewAttached() { }
        protected override void OnInitialize(int parameters) { }
        protected override void Cleanup() => Disposed = true;
    }
}
