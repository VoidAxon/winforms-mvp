using System.Collections.Generic;
using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;
using CloseReason = Kibinago.Common.CloseReason;

namespace Kibinago.Samples.Tests.Services
{
    /// <summary>
    /// PrepareWindow: everything ShowWindow does except showing — the caller places and shows the view
    /// (a docking host's pane). Once shown it is a ShowWindow window.
    /// </summary>
    public class WindowNavigatorPrepareTests
    {
        public interface IPaneView : IWindowView { }

        private sealed class PaneForm : Form, IPaneView
        {
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class PanePresenter : WindowPresenterBase<IPaneView>
        {
            public int InitCount;
            public int CleanupCount;
            public bool Refuse;
            public bool CloseInInit;

            protected override void OnInitialize()
            {
                InitCount++;
                if (CloseInInit) RequestClose(7, InteractionStatus.Ok);
            }

            protected override bool CanClose(CloseReason reason) => !Refuse;
            protected override void Cleanup() => CleanupCount++;
            public void Close(int value) => RequestClose(value, InteractionStatus.Ok);
        }

        private sealed class ParamPanePresenter : WindowPresenterBase<IPaneView, string>
        {
            public string Shown;
            protected override void OnInitialize(string parameters) => Shown = parameters;
        }

        private static (WindowNavigator navigator, List<PaneForm> forms) NewNavigator()
        {
            var forms = new List<PaneForm>();
            var registry = new ViewMappingRegister();
            registry.Register<IPaneView>(() => { var f = new PaneForm(); forms.Add(f); return f; });
            return (new WindowNavigator(registry), forms);
        }

        [Fact]
        public void Prepared_IsInitialized_ButNotShown()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter();

            var view = navigator.For(presenter).PrepareWindow();
            try
            {
                Assert.Same(forms[0], view);
                Assert.Equal(1, presenter.InitCount);
                Assert.False(forms[0].Visible);
            }
            finally { forms[0].Dispose(); }
        }

        [Fact]
        public void WithParameters_TheParametersReachTheInitializer()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new ParamPanePresenter();

            navigator.For(presenter).WithParam("orders").PrepareWindow();
            try { Assert.Equal("orders", presenter.Shown); }
            finally { forms[0].Dispose(); }
        }

        [Fact]
        public void ShownByTheCaller_ClosesThroughCanClose_AndReportsTheResult()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter { Refuse = true };
            var results = new List<InteractionResult<int>>();

            navigator.For(presenter).PrepareWindow<int>(key: 1, onClosed: r => results.Add(r));
            forms[0].Show();   // the container places and shows it

            forms[0].Close();   // ✕: refused
            Assert.False(forms[0].IsDisposed);
            Assert.Empty(results);

            presenter.Refuse = false;
            presenter.Close(5);

            var result = Assert.Single(results);
            Assert.Equal(5, result.Value);
            Assert.Equal(1, presenter.CleanupCount);
            Assert.True(forms[0].IsDisposed);

            var again = navigator.For(new PanePresenter()).PrepareWindow(key: 1);   // the key was released
            try { Assert.NotSame(forms[0], again); }
            finally { forms[1].Dispose(); }
        }

        [Fact]
        public void SameKeyWhileOpen_ReturnsTheOpenView_AndDisposesTheNewPresenter()
        {
            var (navigator, forms) = NewNavigator();
            int secondClosed = 0;

            var first = navigator.For(new PanePresenter()).PrepareWindow(key: "a");
            var second = new PanePresenter();
            var again = navigator.For(second).PrepareWindow<object>(key: "a", onClosed: _ => secondClosed++);
            try
            {
                Assert.Same(first, again);
                Assert.Single(forms);
                Assert.Equal(0, second.InitCount);
                Assert.Equal(1, second.CleanupCount);
            }
            finally { forms[0].Dispose(); }
            Assert.Equal(0, secondClosed);   // its callback is never registered
        }

        [Fact]
        public void WithoutAKey_EachCallPreparesANewWindow()
        {
            var (navigator, forms) = NewNavigator();

            var a = navigator.For(new PanePresenter()).PrepareWindow();
            var b = navigator.For(new PanePresenter()).PrepareWindow();
            try { Assert.NotSame(a, b); }
            finally { forms.ForEach(f => f.Dispose()); }
        }

        [Fact]
        public void RequestCloseDuringInitialization_ReturnsNull_AndEnds()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter { CloseInInit = true };
            var results = new List<InteractionResult<int>>();

            var view = navigator.For(presenter).PrepareWindow<int>(key: 1, onClosed: r => results.Add(r));

            Assert.Null(view);
            Assert.Equal(7, Assert.Single(results).Value);
            Assert.Equal(1, presenter.CleanupCount);
            Assert.True(forms[0].IsDisposed);
            var again = navigator.For(new PanePresenter()).PrepareWindow(key: 1);   // released
            try { Assert.NotNull(again); }
            finally { forms[1].Dispose(); }
        }

        [Fact]
        public void NeverShown_DisposedByTheCaller_Ends_AndReleasesTheKey()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter();
            var results = new List<InteractionResult<int>>();

            navigator.For(presenter).PrepareWindow<int>(key: 1, onClosed: r => results.Add(r));
            forms[0].Dispose();   // e.g. a layout restore did not use it

            Assert.True(Assert.Single(results).IsCancelled);
            Assert.Equal(1, presenter.CleanupCount);
            var again = navigator.For(new PanePresenter()).PrepareWindow(key: 1);
            try { Assert.NotSame(forms[0], again); }
            finally { forms[1].Dispose(); }
        }

        [Fact]
        public void RequestCloseBeforeShown_ClosesWhenTheCallerShowsIt()
        {
            var (navigator, forms) = NewNavigator();
            var presenter = new PanePresenter();
            var results = new List<InteractionResult<int>>();

            navigator.For(presenter).PrepareWindow<int>(onClosed: r => results.Add(r));
            presenter.Close(3);       // before the container showed it: cancels the opening, no gate
            Assert.Empty(results);    // waits for the Form's Load

            forms[0].Show();

            Assert.Equal(3, Assert.Single(results).Value);
            Assert.True(forms[0].IsDisposed);
        }

        [Fact]
        public void ShowWindowWithTheKeyOfAPreparedWindow_ReturnsIt()
        {
            var (navigator, forms) = NewNavigator();

            var prepared = navigator.For(new PanePresenter()).PrepareWindow(key: 1);
            var shown = navigator.For(new PanePresenter()).ShowWindow(key: 1);
            try
            {
                Assert.Same(prepared, shown);
                Assert.Single(forms);
            }
            finally { forms[0].Dispose(); }
        }
    }
}
