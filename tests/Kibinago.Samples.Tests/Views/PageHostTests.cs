using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Services;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kibinago.Samples.Tests.Views
{
    /// <summary>The WinForms page host: creates pages through the view mapping and shows the top one.</summary>
    public class PageHostTests : IDisposable
    {
        public interface ITestPageView : IControlView { }
        public interface IOtherPageView : IControlView { }

        public sealed class TestPageControl : UserControl, ITestPageView
        {
            public IViewActionBinder ActionBinder => null;
        }

        public sealed class ThrowingPageControl : UserControl, ITestPageView
        {
            public ThrowingPageControl() { throw new InvalidOperationException("ctor"); }
            public IViewActionBinder ActionBinder => null;
        }

        private readonly PageHost _host = new PageHost();
        private readonly ViewMappingRegister _register = new ViewMappingRegister();
        private readonly DefaultServiceProvider _services = TestServices.Create();
        private int _changes;

        public PageHostTests()
        {
            _services.RegisterInstance<IViewMappingRegister>(_register);
            _host.PagesChanged += (s, e) => _changes++;
        }

        public void Dispose() => _host.Dispose();

        private Control Add(string title) => (Control)_host.AddPage(typeof(ITestPageView), title, _services);

        [Fact]
        public void TypeBasedMapping_AcceptsAControl()
        {
            _register.Register<ITestPageView, TestPageControl>();
            Assert.Equal(typeof(TestPageControl), _register.GetViewImplementationType(typeof(ITestPageView)));
        }

        [Fact]
        public void Add_ShowsTheNewPageOnTop_HidesThePrevious_TitlesAndEvent()
        {
            _register.Register<ITestPageView, TestPageControl>();

            var a = Add("一覧");
            var b = Add("詳細");

            Assert.IsType<TestPageControl>(b);
            Assert.Same(_host, b.Parent);
            Assert.Equal(DockStyle.Fill, b.Dock);
            Assert.False(a.Visible);
            Assert.Equal(new[] { "一覧", "詳細" }, _host.PageTitles);
            Assert.Equal(2, _changes);
        }

        [Fact]
        public void RemoveMiddle_RemovesAndDisposesIt_TopStays()
        {
            _register.Register<ITestPageView, TestPageControl>();
            var a = Add("A");
            var b = Add("B");
            var c = Add("C");

            _host.RemovePage((IControlView)b);

            Assert.True(b.IsDisposed);
            Assert.Null(b.Parent);
            Assert.Equal(new[] { "A", "C" }, _host.PageTitles);
            Assert.False(a.Visible);
        }

        [Fact]
        public void RemoveTop_ShowsTheNewTop()
        {
            _register.Register<ITestPageView, TestPageControl>();
            var a = Add("A");
            var b = Add("B");

            _host.RemovePage((IControlView)b);

            Assert.True(a.Visible);
            Assert.Equal(new[] { "A" }, _host.PageTitles);
            Assert.Equal(3, _changes);
        }

        [Fact]
        public void Remove_ToleratesADisposedControl()
        {
            _register.Register<ITestPageView, TestPageControl>();
            var a = Add("A");
            a.Dispose();

            _host.RemovePage((IControlView)a);

            Assert.Empty(_host.PageTitles);
        }

        [Fact]
        public void Remove_Unknown_Throws()
        {
            Assert.Throws<ArgumentException>(() => _host.RemovePage(new TestPageControl()));
        }

        [Fact]
        public void MappingMissing_Throws_NothingAdded()
        {
            Assert.ThrowsAny<Exception>(() => Add("A"));
            Assert.Empty(_host.Controls);
            Assert.Empty(_host.PageTitles);
        }

        [Fact]
        public void NoMappingRegistryInTheServices_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _host.AddPage(typeof(ITestPageView), "A", TestServices.Create()));
        }

        [Fact]
        public void ConstructorThrows_NothingAdded_PreviousTopStillVisible()
        {
            _register.Register<IOtherPageView>(() => new OtherControl());
            var first = (Control)_host.AddPage(typeof(IOtherPageView), "A", _services);
            _register.Register<ITestPageView, ThrowingPageControl>();

            Assert.ThrowsAny<Exception>(() => Add("B"));

            Assert.True(first.Visible);
            Assert.Equal(new[] { "A" }, _host.PageTitles);
        }

        public sealed class OtherControl : UserControl, IOtherPageView
        {
            public IViewActionBinder ActionBinder => null;
        }

        [Fact]
        public void FactoryReturnsAForm_Rejected_NothingAdded()
        {
            _register.Register<ITestPageView>(() => new FormPage());
            Assert.Throws<InvalidOperationException>(() => Add("A"));
            Assert.Empty(_host.Controls);
        }

        public sealed class FormPage : Form, ITestPageView
        {
            public IViewActionBinder ActionBinder => null;
        }

        [Fact]
        public void FactoryReturnsAControlPlacedElsewhere_Rejected_NotDisposed()
        {
            var elsewhere = new TestPageControl();
            using (var panel = new Panel())
            {
                panel.Controls.Add(elsewhere);
                _register.Register<ITestPageView>(() => elsewhere);

                Assert.Throws<InvalidOperationException>(() => Add("A"));

                Assert.False(elsewhere.IsDisposed);
                Assert.Same(panel, elsewhere.Parent);
            }
        }

        [Fact]
        public void APagesChangedHandlerThatThrows_UndoesTheAdd()
        {
            _register.Register<ITestPageView, TestPageControl>();
            var a = Add("A");
            _host.PagesChanged += (s, e) => { if (_host.PageTitles.Count == 2) throw new InvalidOperationException("redraw"); };

            Assert.Throws<InvalidOperationException>(() => Add("B"));

            Assert.True(a.Visible);
            Assert.Equal(new[] { "A" }, _host.PageTitles);
        }

        // ---- the mapping still keeps windows and pages apart ----

        public interface IControlAsWindowView : IWindowView { }

        public sealed class ControlAsWindow : UserControl, IControlAsWindowView
        {
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class WindowPresenter : WindowPresenterBase<IControlAsWindowView> { }

        [Fact]
        public void WindowNavigator_StillRejectsAControlAsAWindow()
        {
            _register.Register<IControlAsWindowView, ControlAsWindow>();
            var navigator = new WindowNavigator(_register);

            Assert.Throws<InvalidOperationException>(() => navigator.ShowWindow(new WindowPresenter()));
        }

        // ---- M.E.DI: the page view shares its presenter's scope (Review Focus 4) ----

        public sealed class ScopedDependency { }

        public interface IScopedPageView : IControlView { ScopedDependency Dependency { get; } }

        public sealed class ScopedPageControl : UserControl, IScopedPageView
        {
            public ScopedPageControl(ScopedDependency dependency) { Dependency = dependency; }
            public ScopedDependency Dependency { get; }
            public IViewActionBinder ActionBinder => null;
        }

        public sealed class ScopedPagePresenter : ControlPresenterBase<IScopedPageView>
        {
            public ScopedPagePresenter(ScopedDependency dependency) { Dependency = dependency; }
            public ScopedDependency Dependency { get; }
            public ScopedDependency ViewDependency;
            protected override void OnInitialize() => ViewDependency = View.Dependency;
        }

        public interface IHostedWindowView : IWindowView { IPageHost Pages { get; } }

        private sealed class HostedWindowView : IHostedWindowView
        {
            public PageHost Host { get; } = new PageHost();
            public IPageHost Pages => Host;
            public IViewActionBinder ActionBinder => null;
        }

        private sealed class HostedWindow : WindowPresenterBase<IHostedWindowView>
        {
            private readonly PageStack _pages;
            public HostedWindow() { _pages = new PageStack(this, () => View.Pages); }
            internal bool Push(ScopedPagePresenter page) => _pages.Push(page, "page");
        }

        [Fact]
        public void UnderDI_APageViewRegisteredInTheContainer_GetsItsPresentersScope()
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedDependency>();
            services.AddTransient<IScopedPageView, ScopedPageControl>();
            services.AddTransient<ScopedPagePresenter>();
            services.AddSingleton<IViewMappingRegister>(new ViewMappingRegister());
            services.AddSingleton<Kibinago.Logging.ILoggerFactory>(Kibinago.Logging.NullLoggerFactory.Instance);
            var factory = new Kibinago.DependencyInjection.ServiceProviderPresenterFactory(services.BuildServiceProvider());
            var page = factory.Create<ScopedPagePresenter>();
            var view = new HostedWindowView();
            var window = new HostedWindow();

            using (PresenterTestHost.Start(TestServices.CreateFakes().Provider, window, view))
            {
                Assert.True(window.Push(page));
                Assert.Same(page.Dependency, page.ViewDependency);
            }
            view.Host.Dispose();
        }
    }
}
