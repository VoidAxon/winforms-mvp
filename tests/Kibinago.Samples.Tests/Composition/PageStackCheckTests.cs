using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kibinago.Logging;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>The one check policy behind jumps and CanCloseAllAsync.</summary>
    public class PageStackCheckTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeWorkspaceView _view = new FakeWorkspaceView();
        private readonly List<string> _log = new List<string>();

        private PresenterTestHost Start(out WorkspacePresenter workspace)
        {
            _view.Log = _log;
            workspace = new WorkspacePresenter(_log);
            return PresenterTestHost.Start(_fakes.Provider, workspace, _view);
        }

        private string[] Asks() => _log.Where(l => l.StartsWith("ask:")).ToArray();

        private static async Task<T> Within<T>(Task<T> task)
        {
            var done = await Task.WhenAny(task, Task.Delay(5000));
            Assert.Same(task, done);
            return await task;
        }

        [Fact]
        public async Task TopFirst_NestedFirst_Order()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var b = new GuardedPage("B", _log, ownsStack: true);
                ws.Push(b);
                b.PushInner(new GuardedPage("B1", _log), null);
                b.PushInner(new GuardedPage("B2", _log), null);
                ws.Push(new GuardedPage("C", _log));

                Assert.True(await ws.Stack.PopToCountAsync(0));

                Assert.Equal(new[] { "ask:C", "ask:B2", "ask:B1", "ask:B", "ask:A" }, Asks());
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task NestedStacksChecked_ForPageWithoutGuard()
        {
            using (Start(out var ws))
            {
                var plain = new PlainPage("P", _log, ownsStack: true);
                ws.Push(plain);
                var refusing = new GuardedPage("R", _log) { Answer = () => Task.FromResult(false) };
                plain.PushInner(refusing, null);

                Assert.False(await ws.Stack.PopAsync());

                Assert.Equal(1, ws.Stack.Count);
                Assert.Equal(1, refusing.AskCount);
                Assert.False(plain.Disposed);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ForwardingGuard_JoinsCheck_NoFalseBusy(bool innerAwaits)
        {
            using (Start(out var ws))
            {
                var panel = new ForwardingPage("F", _log);
                ws.Push(panel);
                var inner = new GuardedPage("F1", _log);
                if (innerAwaits) inner.Answer = async () => { await Task.Yield(); return true; };
                panel.PushInner(inner, null);

                Assert.True(await Within(ws.Stack.PopAsync()));

                Assert.Equal(1, panel.AskCount);
                Assert.Equal(1, inner.AskCount);
                Assert.Equal(0, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task ForwardingGuard_AskedDirectly_RunsItsOwnCheck()
        {
            using (Start(out var ws))
            {
                var panel = new ForwardingPage("F", _log);
                ws.Push(panel);
                var inner = new GuardedPage("F1", _log) { Answer = () => Task.FromResult(false) };
                panel.PushInner(inner, null);

                Assert.False(await ((Kibinago.Presenters.IPageCloseGuard)panel).CanCloseAsync());
                Assert.False(panel.Inner.IsBusy);
                Assert.Equal(1, panel.Inner.Count);
            }
        }

        [Fact]
        public async Task Cycle_RefusesWithoutHanging()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var b = new GuardedPage("B", _log);
                b.Answer = () => ws.Stack.CanCloseAllAsync();   // asks the stack whose check is asking b
                ws.Push(b);

                Assert.False(await Within(ws.Stack.PopToCountAsync(0)));
                Assert.Equal(2, ws.Stack.Count);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task Cycle_ThroughAForwardingPanel_RefusesWithoutHanging()
        {
            using (Start(out var ws))
            {
                var panel = new ForwardingPage("F", _log);
                ws.Push(panel);
                var inner = new GuardedPage("F1", _log);
                inner.Answer = () => panel.Inner.CanCloseAllAsync();   // F.Inner is being asked: it waits for F1
                panel.PushInner(inner, null);

                Assert.False(await Within(ws.Stack.PopAsync()));
                Assert.Equal(1, ws.Stack.Count);
            }
        }

        public static IEnumerable<object[]> BrokenAnswers()
        {
            yield return new object[] { "faulted" };
            yield return new object[] { "cancelled" };
            yield return new object[] { "null" };
            yield return new object[] { "throws" };
        }

        [Theory]
        [MemberData(nameof(BrokenAnswers))]
        public async Task FaultedCancelledNullOrThrowingGuard_Refuses_AndLogs(string kind)
        {
            using (Start(out var ws))
            {
                var page = new GuardedPage("A", _log);
                switch (kind)
                {
                    case "faulted": page.Answer = () => Task.FromException<bool>(new InvalidOperationException("x")); break;
                    case "cancelled": page.Answer = () => Task.FromCanceled<bool>(new CancellationToken(true)); break;
                    case "null": page.Answer = () => null; break;
                    case "throws": page.Answer = () => throw new InvalidOperationException("x"); break;
                }
                ws.Push(page);

                Assert.False(await ws.Stack.PopAsync());

                Assert.Equal(1, ws.Stack.Count);
                Assert.Contains(_fakes.Logs.Entries, e => e.Level == LogLevel.Error);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task Refusal_RemovesNothing_AskedAgainNextTime()
        {
            using (Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                var b = new GuardedPage("B", _log) { Answer = () => Task.FromResult(false) };
                ws.Push(a);
                ws.Push(b);

                Assert.False(await ws.Stack.PopToCountAsync(0));
                Assert.Equal(new[] { "ask:B" }, Asks());
                Assert.Equal(2, ws.Stack.Count);
                Assert.Empty(_view.Removed);

                b.Answer = () => Task.FromResult(true);
                Assert.True(await ws.Stack.PopToCountAsync(0));
                Assert.Equal(new[] { "ask:B", "ask:B", "ask:A" }, Asks());
            }
        }

        [Fact]
        public async Task OverlappingOperation_WhileAsyncGuardPending()
        {
            using (Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var pending = new TaskCompletionSource<bool>();
                var b = new GuardedPage("B", _log) { Answer = () => pending.Task };
                ws.Push(b);

                var first = ws.Stack.PopAsync();
                Assert.True(ws.Stack.IsBusy);

                var second = ws.Stack.PopToCountAsync(0);
                Assert.True(second.IsCompleted);
                Assert.False(await second);
                Assert.Throws<InvalidOperationException>(() => ws.Push(new PlainPage("C", _log)));
                Assert.False(await ws.Stack.CanCloseAllAsync());
                Assert.Equal(1, b.AskCount);

                pending.SetResult(true);
                Assert.True(await Within(first));
                Assert.Equal(1, ws.Stack.Count);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task OwnerDisposedWhilePending_ReturnsFalse_NoRemoveView()
        {
            var host = Start(out var ws);
            ws.Push(new GuardedPage("A", _log));
            var pending = new TaskCompletionSource<bool>();
            ws.Push(new GuardedPage("B", _log) { Answer = () => pending.Task });

            var jump = ws.Stack.PopAsync();
            host.Dispose();
            pending.SetResult(true);

            Assert.False(await Within(jump));
            Assert.Empty(_view.Removed);
        }

        [Fact]
        public async Task StackCreatedOrAttachedDuringCheck_IsEnrolled()
        {
            using (Start(out var ws))
            {
                var pending = new TaskCompletionSource<bool>();
                var b = new GuardedPage("B", _log) { Answer = () => pending.Task };
                ws.Push(b);

                var jump = ws.Stack.PopAsync();
                // b's guard code, while it decides: a new stack, and a panel that owns one.
                var born = b.CreateStackForTest();
                var panel = new PlainPage("panel", _log, ownsStack: true);
                b.AttachPanelForTest(panel);
                Assert.True(born.IsBusy);
                Assert.True(panel.Inner.IsBusy);

                pending.SetResult(false);
                Assert.False(await Within(jump));
                Assert.False(born.IsBusy);
                Assert.False(panel.Inner.IsBusy);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task UnrelatedStack_IsNotReserved()
        {
            using (Start(out var ws))
            {
                var pending = new TaskCompletionSource<bool>();
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log) { Answer = () => pending.Task });
                var side = new PlainPage("side", _log, ownsStack: true);
                ws.AttachPanel(side);

                var jump = ws.Stack.PopAsync();
                Assert.False(side.Inner.IsBusy);
                side.PushInner(new PlainPage("S1", _log), null);   // a sibling tree is not part of the check

                pending.SetResult(true);
                Assert.True(await Within(jump));
            }
        }

        [Fact]
        public async Task IndependentCaller_WhileAGuardIsPending_DoesNotJoinTheCheck()
        {
            using (Start(out var ws))
            {
                var pending = new TaskCompletionSource<bool>();
                var b = new GuardedPage("B", _log, ownsStack: true) { Answer = () => pending.Task };
                ws.Push(b);
                b.PushInner(new GuardedPage("B1", _log), null);

                var jump = ws.Stack.PopAsync();   // B1 answered, B's guard is pending
                Assert.Equal(new[] { "ask:B1", "ask:B" }, Asks());

                // An unrelated handler (not B's guard code) asks B's own stack meanwhile.
                Assert.False(await b.Inner.CanCloseAllAsync());

                pending.SetResult(false);
                Assert.False(await Within(jump));
            }
        }

        [Fact]
        public async Task GuardAnswers_AreKeptPerPresenterInstance_NotByEquality()
        {
            using (Start(out var ws))
            {
                var lower = new EqualPage("lower", _log) { Answer = () => Task.FromResult(false) };
                var upper = new EqualPage("upper", _log);
                ws.Push(lower);
                ws.Push(upper);

                Assert.False(await ws.Stack.PopToCountAsync(0));
                Assert.Equal(1, lower.AskCount);
                Assert.Equal(2, ws.Stack.Count);
            }
        }
    }
}
