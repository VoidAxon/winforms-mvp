using System;
using System.Collections.Generic;
using Kibinago.Presenters;
using Kibinago.Samples.Tests.Composition;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Testing
{
    public class FakePageHostTests
    {
        private sealed class OtherView : IControlView
        {
            public IViewActionBinder ActionBinder => null;
        }

        [Fact]
        public void AddPage_CreatesTheRegisteredView_RecordsTitle()
        {
            var host = new FakePageHost().Register<IPageView>(() => new FakePageView("v"));

            var view = host.AddPage(typeof(IPageView), "一覧", null);

            Assert.IsType<FakePageView>(view);
            Assert.Equal(new[] { view }, host.Pages);
            Assert.Equal(new[] { "一覧" }, host.PageTitles);
            Assert.Same(view, host.Page<FakePageView>(0));
        }

        [Fact]
        public void AddPage_Unregistered_Throws_NothingAdded()
        {
            var host = new FakePageHost();

            var ex = Assert.Throws<InvalidOperationException>(() => host.AddPage(typeof(IPageView), "x", null));

            Assert.Contains(nameof(IPageView), ex.Message);
            Assert.Empty(host.Pages);
        }

        [Fact]
        public void RemovePage_AnyLevel_Recorded()
        {
            var host = new FakePageHost().Register<IPageView>(() => new FakePageView("v")).Register(() => new OtherView());
            var a = host.AddPage(typeof(IPageView), "A", null);
            var b = host.AddPage(typeof(OtherView), "B", null);
            var c = host.AddPage(typeof(IPageView), "C", null);

            host.RemovePage(b);

            Assert.Equal(new[] { a, c }, host.Pages);
            Assert.Equal(new[] { "A", "C" }, host.PageTitles);
            Assert.Equal(new[] { b }, host.Removed);
        }

        [Fact]
        public void RemovePage_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => new FakePageHost().RemovePage(new OtherView()));
        }

        [Fact]
        public void AddPage_AViewAlreadyUsedByAPresenter_Rejected_NothingRecorded()
        {
            var shared = new FakePageView("shared");
            var host = new FakePageHost().Register<IPageView>(() => shared);
            using (Kibinago.Presenters.ViewOwnership.Claim(shared, new object()))
            {
                Assert.Throws<InvalidOperationException>(() => host.AddPage(typeof(IPageView), "x", null));
                Assert.Empty(host.Pages);
                Assert.Empty(host.PageTitles);
            }
        }

        // End to end: a window presenter whose stack shows its pages in a FakePageHost.
        private interface IHostWindowView : IWindowView { IPageHost Pages { get; } }

        private sealed class HostWindowView : IHostWindowView
        {
            public FakePageHost Host { get; } = new FakePageHost().Register<IPageView>(() => new FakePageView("page"));
            public IPageHost Pages => Host;
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class HostWindow : WindowPresenterBase<IHostWindowView>
        {
            private readonly PageStack _pages;
            public HostWindow() { _pages = new PageStack(this, () => View.Pages); }
            protected override void OnInitialize() => _pages.Push(new PlainPage("list", null), "一覧");
            internal System.Threading.Tasks.Task<bool> Back() => _pages.PopToCountAsync(0);
        }

        [Fact]
        public async System.Threading.Tasks.Task WorksWithAPageStack_ThroughPresenterTestHost()
        {
            var view = new HostWindowView();
            var window = new HostWindow();
            using (PresenterTestHost.Start(TestServices.CreateFakes().Provider, window, view))
            {
                Assert.Equal(new[] { "一覧" }, view.Host.PageTitles);

                Assert.True(await window.Back());

                Assert.Empty(view.Host.Pages);
                Assert.Single(view.Host.Removed);
            }
        }
    }
}
