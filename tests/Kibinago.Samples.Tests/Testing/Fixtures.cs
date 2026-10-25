using System;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Views;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Testing
{
    // Small presenters and views shared by the Kibinago.Testing tests (no WinForms types).

    public interface IFixtureView : IWindowView { }

    public sealed class FixtureView : IFixtureView
    {
        public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
        public IViewActionBinder ActionBinder => Binder;
    }

    public sealed class FixturePresenter : WindowPresenterBase<IFixtureView>
    {
        public bool Disposed { get; private set; }
        protected override void Cleanup() => Disposed = true;
    }

    public sealed class OtherFixturePresenter : WindowPresenterBase<IFixtureView>
    {
        public bool Disposed { get; private set; }
        protected override void Cleanup() => Disposed = true;
    }

    /// <summary>Implements only <c>IInitializable&lt;int&gt;</c>: a parameterless show must throw.</summary>
    public sealed class ParamFixturePresenter : WindowPresenterBase<IFixtureView, int>
    {
        public bool Disposed { get; private set; }
        protected override void OnInitialize(int parameters) { }
        protected override void Cleanup() => Disposed = true;
    }
}
