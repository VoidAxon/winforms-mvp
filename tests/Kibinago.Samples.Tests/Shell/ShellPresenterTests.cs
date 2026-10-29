using System;
using System.Collections.Generic;
using System.Linq;
using Kibinago.Presenters;
using Kibinago.Samples.Shell;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Shell
{
    /// <summary>
    /// The sample's main window: demos attached as child presenters the first time their tab opens,
    /// released (presenters, then views) when another category is chosen; and the catalog's consistency.
    /// </summary>
    public class ShellPresenterTests
    {
        #region Test doubles

        public interface IPanelView : IControlView { }

        private sealed class PanelView : IPanelView
        {
            public IViewActionBinder ActionBinder => null;
            public string DemoId;
            public bool Removed;
        }

        private sealed class PanelPresenter : ControlPresenterBase<IPanelView>
        {
            private readonly List<string> _log;
            private readonly string _name;
            private readonly bool _fail;

            public PanelPresenter(List<string> log, string name, bool fail = false)
            {
                _log = log;
                _name = name;
                _fail = fail;
            }

            protected override void OnInitialize()
            {
                if (_fail) throw new InvalidOperationException("init failed");
                _log.Add("init " + _name + (((PanelView)View).Removed ? " (removed view!)" : ""));
            }

            protected override void Cleanup() =>
                _log.Add("dispose " + _name + (((PanelView)View).Removed ? " (removed view!)" : ""));
        }

        private sealed class MockShellView : IShellView
        {
            private readonly List<string> _log;
            private readonly Dictionary<string, PanelView> _views = new Dictionary<string, PanelView>();

            public MockShellView(List<string> log) => _log = log;

            public IReadOnlyList<SampleCategory> Categories;
            public SampleCategory Shown;
            public IReadOnlyList<string> Source = Array.Empty<string>();

            public IViewActionBinder ActionBinder => null;
            public event EventHandler<string> CategorySelected;
            public event EventHandler<string> DemoSelected;

            public void ShowCategories(IReadOnlyList<SampleCategory> categories) => Categories = categories;
            public void SelectCategory(string categoryId) => CategorySelected?.Invoke(this, categoryId);
            public void ShowCategory(SampleCategory category) { Shown = category; _log.Add("show " + category.Id); }

            public IView CreateDemoView(string demoId)
            {
                _log.Add("create " + demoId);
                return _views[demoId] = new PanelView { DemoId = demoId };
            }

            public void RemoveDemoView(string demoId)
            {
                _log.Add("remove " + demoId);
                _views[demoId].Removed = true;
                _views.Remove(demoId);
            }

            public void RemoveDemoViews()
            {
                foreach (var id in _views.Keys.ToList())
                    RemoveDemoView(id);
            }

            public void ShowSource(IReadOnlyList<string> sourceFiles) => Source = sourceFiles;

            public void OpenDemo(string demoId) => DemoSelected?.Invoke(this, demoId);
        }

        private static SampleDemo Panel(List<string> log, string id, bool fail = false) =>
            SampleDemo.Embedded(id, id, "", (a, v) => a.Attach(new PanelPresenter(log, id, fail), (IPanelView)v), id + ".cs");

        private static IReadOnlyList<SampleCategory> Catalog(List<string> log) => new[]
        {
            new SampleCategory("a", "A", "", "", null, new[] { Panel(log, "a1"), Panel(log, "a2"), SampleDemo.Window("aw", "AW", "", "aw.cs") }, new[] { "a.cs" }),
            new SampleCategory("b", "B", "", "", null, new[] { Panel(log, "b1"), Panel(log, "bad", fail: true) }),
        };

        #endregion

        [Fact]
        public void TheFirstCategory_IsShown_WithoutCreatingAnyDemo()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            using (PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log)), view))
            {
                Assert.Equal(new[] { "a", "b" }, view.Categories.Select(c => c.Id));
                Assert.Equal("a", view.Shown.Id);
                Assert.Equal(new[] { "show a" }, log);
                Assert.Equal(new[] { "a.cs", "a1.cs", "a2.cs", "aw.cs" }, view.Source);   // the category's and its demos'
            }
        }

        [Fact]
        public void ADemo_IsCreatedTheFirstTimeItsTabOpens_AndKeptWithinTheCategory()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            using (PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log)), view))
            {
                view.OpenDemo("a1");
                view.OpenDemo("a2");
                view.OpenDemo("a1");   // back to a tab already open: same instance
                view.OpenDemo("aw");   // a window demo: nothing is created in the tab

                Assert.Equal(new[] { "show a", "create a1", "init a1", "create a2", "init a2" }, log);
                Assert.Equal(new[] { "aw.cs" }, view.Source);
            }
        }

        [Fact]
        public void AnotherCategory_DisposesThePresentersFirst_ThenTheirViews_AndComingBackStartsAfresh()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            using (PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log)), view))
            {
                view.OpenDemo("a1");
                view.OpenDemo("a2");
                log.Clear();

                view.SelectCategory("b");
                Assert.Equal(new[] { "dispose a2", "dispose a1", "remove a1", "remove a2", "show b" }, log);

                log.Clear();
                view.SelectCategory("a");
                view.OpenDemo("a1");
                Assert.Equal(new[] { "show a", "create a1", "init a1" }, log);

                log.Clear();
                view.SelectCategory("a");   // the category already shown: nothing happens
                Assert.Empty(log);
            }
        }

        [Fact]
        public void ClosingTheShell_DisposesTheOpenDemos()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            var host = PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log)), view);
            view.OpenDemo("a1");
            log.Clear();

            host.Dispose();

            Assert.Equal(new[] { "dispose a1", "remove a1" }, log);
        }

        [Fact]
        public void ADemoThatFailsToStart_HasItsViewRemoved_AndTheFailurePropagates()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            using (PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log)), view))
            {
                view.SelectCategory("b");
                log.Clear();

                Assert.ThrowsAny<Exception>(() => view.OpenDemo("bad"));
                Assert.Equal("create bad", log.First());
                Assert.Equal("remove bad", log.Last());

                view.OpenDemo("b1");   // the shell still works
                Assert.Contains("init b1", log);
            }
        }

        [Fact]
        public void TheStartCategory_CanBeChosen()
        {
            var log = new List<string>();
            var view = new MockShellView(log);
            using (PresenterTestHost.Start(TestServices.Create(), new ShellPresenter(Catalog(log), "b"), view))
                Assert.Equal("b", view.Shown.Id);
        }

        [Fact]
        public void TheCatalog_IsConsistent_WithTheViewsAndTheEmbeddedSources()
        {
            var categories = SampleCatalog.Create();

            Assert.Equal(categories.Count, categories.Select(c => c.Id).Distinct().Count());
            var demos = categories.SelectMany(c => c.Demos).ToList();
            Assert.Equal(demos.Count, demos.Select(d => d.Id).Distinct().Count());
            Assert.Contains(categories, c => c.Id == SampleCatalog.CustomizeCategoryId);

            foreach (var demo in demos)
            {
                Assert.True(DemoViews.Has(demo.Id, demo.Kind), $"no {demo.Kind} view for {demo.Id}");
                Assert.Equal(demo.Kind == DemoKind.Embedded, demo.Attach != null);
            }

            foreach (var file in categories.SelectMany(c => c.SourceFiles).Concat(demos.SelectMany(d => d.SourceFiles)))
            {
                Assert.True(SampleSources.Exists(file), $"{file} is not embedded");
                Assert.False(file.EndsWith(".Designer.cs"), $"{file}: the コード tab shows a view's behaviour, not its layout");
                Assert.False(string.IsNullOrEmpty(SampleSources.Read(file)));
            }
        }
    }
}
