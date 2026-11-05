using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.Samples.Shell;
using Kibinago.Samples.Tests.Composition;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Shell
{
    /// <summary>Switching category asks the embedded demos that can refuse (IPageCloseGuard) before disposing any.</summary>
    public class ShellCategorySwitchTests
    {
        private sealed class GuardedDemo : ControlPresenterBase<IPageView>, IPageCloseGuard
        {
            private readonly List<string> _log;
            private readonly string _name;
            public Func<Task<bool>> Answer = () => Task.FromResult(true);

            public Exception ThrowOnCleanup;

            public GuardedDemo(List<string> log, string name) { _log = log; _name = name; }

            Task<bool> IPageCloseGuard.CanCloseAsync()
            {
                _log.Add("ask " + _name);
                return Answer();
            }

            protected override void Cleanup()
            {
                _log.Add("dispose " + _name);
                if (ThrowOnCleanup != null) throw ThrowOnCleanup;
            }
        }

        private sealed class MockShellView : IShellView
        {
            private readonly List<string> _log;
            public MockShellView(List<string> log) => _log = log;

            public SampleCategory Shown;
            public readonly List<string> Selected = new List<string>();
            public IViewActionBinder ActionBinder => null;
            public event EventHandler<string> CategorySelected;
            public event EventHandler<string> DemoSelected;

            public void ShowCategories(IReadOnlyList<SampleCategory> categories) { }
            public void SelectCategory(string categoryId) { Selected.Add(categoryId); SidebarSelection = categoryId; CategorySelected?.Invoke(this, categoryId); }
            public void ShowCategory(SampleCategory category) { Shown = category; _log.Add("show " + category.Id); }
            public IView CreateDemoView(string demoId) => new FakePageView(demoId);
            public void RemoveDemoView(string demoId) => _log.Add("remove " + demoId);
            public void RemoveDemoViews() => _log.Add("remove views");
            public void ShowSource(IReadOnlyList<string> sourceFiles) { }

            public string SidebarSelection;
            public void ClickCategory(string id) { SidebarSelection = id; CategorySelected?.Invoke(this, id); }
            public void OpenDemo(string id) => DemoSelected?.Invoke(this, id);
        }

        private readonly List<string> _log = new List<string>();
        private readonly Dictionary<string, GuardedDemo> _demos = new Dictionary<string, GuardedDemo>();
        private MockShellView _view;

        private SampleDemo Guarded(string id) => SampleDemo.Embedded(id, id, "", (a, v) =>
        {
            var demo = new GuardedDemo(_log, id);
            _demos[id] = demo;
            return a.Attach(demo, (IPageView)v);
        }, id + ".cs");

        private SampleDemo[] _extraOfA = Array.Empty<SampleDemo>();

        private PresenterTestHost Start(params SampleDemo[] demosOfA)
        {
            _view = new MockShellView(_log);
            var catalog = new[]
            {
                new SampleCategory("a", "A", "", "", null, demosOfA.Concat(_extraOfA).ToArray()),
                new SampleCategory("b", "B", "", "", null, Array.Empty<SampleDemo>()),
                new SampleCategory("c", "C", "", "", null, Array.Empty<SampleDemo>()),
            };
            var host = PresenterTestHost.Start(TestServices.CreateFakes().Provider, new ShellPresenter(catalog), _view);
            foreach (var demo in demosOfA) _view.OpenDemo(demo.Id);
            _log.Clear();
            _view.Selected.Clear();
            return host;
        }

        [Fact]
        public void ARefusingDemo_KeepsEveryDemo_AndTheSidebarGoesBack()
        {
            using (Start(Guarded("g1")))
            {
                _demos["g1"].Answer = () => Task.FromResult(false);

                _view.ClickCategory("b");

                Assert.Equal(new[] { "ask g1" }, _log);
                Assert.Equal("a", _view.Shown.Id);
                Assert.Equal(new[] { "a" }, _view.Selected);
            }
        }

        [Fact]
        public void EveryDemoIsAsked_InReverseAttachOrder_BeforeAnyIsDisposed()
        {
            using (Start(Guarded("g1"), Guarded("g2")))
            {
                _view.ClickCategory("b");

                Assert.Equal(new[] { "ask g2", "ask g1", "dispose g2", "dispose g1", "remove views", "show b" }, _log);
            }
        }

        [Fact]
        public async Task AnOverlappingSwitch_WhileADemoDecides_IsIgnored()
        {
            using (Start(Guarded("g1")))
            {
                var pending = new TaskCompletionSource<bool>();
                _demos["g1"].Answer = () => pending.Task;

                _view.ClickCategory("b");
                _view.ClickCategory("c");
                Assert.Equal(new[] { "ask g1" }, _log);

                pending.SetResult(true);
                await WaitFor(() => _view.Shown.Id == "b");
                Assert.DoesNotContain("show c", _log);
            }
        }

        [Fact]
        public async Task AWindowClose_WhileASwitchIsPending_IsBlockedWithoutPrompting()
        {
            var pending = new TaskCompletionSource<bool>();
            ForwardingPage demo = null;
            var forwarding = SampleDemo.Embedded("f", "f", "", (a, v) =>
            {
                demo = new ForwardingPage("f", _log);
                return a.Attach(demo, (IPageView)v);
            }, "f.cs");
            using (var host = Start(forwarding))
            {
                var page = new GuardedPage("page", _log) { Answer = () => pending.Task };
                demo.PushInner(page, null);

                _view.ClickCategory("b");            // the demo's page is deciding
                Assert.False(host.CanClose());       // its stack is busy: no second prompt
                Assert.Equal(1, page.AskCount);

                pending.SetResult(false);
                await WaitFor(() => !demo.Inner.IsBusy);
                Assert.Equal("a", _view.Shown.Id);
            }
        }

        [Fact]
        public void ClosingTheShell_DisposesEveryDemo_EvenOneThatWouldRefuse()
        {
            var host = Start(Guarded("g1"));
            _demos["g1"].Answer = () => Task.FromResult(false);

            host.Dispose();

            Assert.Contains("dispose g1", _log);
            Assert.DoesNotContain("ask g1", _log);
        }

        [Fact]
        public async Task DisposedWhileASwitchIsPending_NothingMoreHappens()
        {
            var host = Start(Guarded("g1"));
            var pending = new TaskCompletionSource<bool>();
            _demos["g1"].Answer = () => pending.Task;
            _view.ClickCategory("b");

            host.Dispose();
            _log.Clear();
            pending.SetResult(true);
            await Task.Delay(50);

            Assert.Empty(_log);
        }

        [Fact]
        public async Task ADemoOpenedWhileASwitchIsDeciding_IsOpenedIfTheSwitchIsRefused()
        {
            _extraOfA = new[] { Guarded("late") };
            using (Start(Guarded("g1")))
            {
                var pending = new TaskCompletionSource<bool>();
                _demos["g1"].Answer = () => pending.Task;

                _view.ClickCategory("b");
                _view.OpenDemo("late");                 // its tab is shown while the switch decides
                pending.SetResult(false);

                await WaitFor(() => _view.SidebarSelection == "a");
                Assert.True(_demos.ContainsKey("late"));   // opened once the switch was refused
            }
        }

        [Fact]
        public async Task ADemoOpenedWhileASwitchIsDeciding_IsNotOpened()
        {
            _extraOfA = new[] { Guarded("late") };
            using (Start(Guarded("g1")))
            {
                _log.Clear();
                var pending = new TaskCompletionSource<bool>();
                _demos["g1"].Answer = () => pending.Task;

                _view.ClickCategory("b");
                _view.OpenDemo("late");                 // would be discarded without being asked
                Assert.False(_demos.ContainsKey("late"));

                pending.SetResult(true);
                await WaitFor(() => _view.Shown.Id == "b");
                Assert.DoesNotContain(_log, l => l.EndsWith(" late") && l.StartsWith("dispose"));
            }
        }

        [Fact]
        public void ADemoWhoseTeardownFails_StillLeavesTheShellUsable()
        {
            using (Start(Guarded("g1"), Guarded("g2")))
            {
                var failure = new InvalidOperationException("cleanup");
                _demos["g2"].ThrowOnCleanup = failure;

                var thrown = Assert.ThrowsAny<Exception>(() => _view.ClickCategory("b"));

                Assert.Same(failure, thrown);
                Assert.Contains("dispose g1", _log);    // the other demo was still disposed
                Assert.Equal("b", _view.Shown.Id);       // the switch completed

                _log.Clear();
                _view.ClickCategory("c");                // nothing left that could refuse
                Assert.Equal("c", _view.Shown.Id);
            }
        }

        [Fact]
        public async Task AnIgnoredClick_DoesNotLeaveTheSidebarOnTheWrongCategory()
        {
            using (Start(Guarded("g1")))
            {
                var pending = new TaskCompletionSource<bool>();
                _demos["g1"].Answer = () => pending.Task;

                _view.ClickCategory("b");
                _view.ClickCategory("c");               // ignored: the sidebar shows c for now
                pending.SetResult(true);

                await WaitFor(() => _view.Shown.Id == "b");
                Assert.Equal("b", _view.SidebarSelection);
            }
        }

        [Fact]
        public async Task AWindowClose_WhileAStacklessDemoDecides_IsBlocked()
        {
            using (var host = Start(Guarded("g1")))
            {
                var pending = new TaskCompletionSource<bool>();
                _demos["g1"].Answer = () => pending.Task;

                _view.ClickCategory("b");
                Assert.False(host.CanClose());

                pending.SetResult(false);
                await WaitFor(() => _view.SidebarSelection == "a");
                Assert.True(host.CanClose());
            }
        }

        private static async Task WaitFor(Func<bool> condition)
        {
            for (int i = 0; i < 500 && !condition(); i++) await Task.Delay(10);
            Assert.True(condition());
        }
    }
}
