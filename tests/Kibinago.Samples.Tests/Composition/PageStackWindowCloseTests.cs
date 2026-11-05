using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.Testing;
using Xunit;

namespace Kibinago.Samples.Tests.Composition
{
    /// <summary>A window close asks every page first, and holds the stacks until the close settles.</summary>
    public class PageStackWindowCloseTests
    {
        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly FakeWorkspaceView _view = new FakeWorkspaceView();
        private readonly List<string> _log = new List<string>();

        private PresenterTestHost Start(out WorkspacePresenter workspace)
        {
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
        public void Close_AsksPagesRecursively_ThenWindow()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log));
                var panel = new PlainPage("P", _log, ownsStack: true);
                ws.AttachPanel(panel);
                panel.PushInner(new GuardedPage("P1", _log), null);

                Assert.True(host.CanClose());

                Assert.Equal(new[] { "ask:B", "ask:A", "ask:P1", "ask:window" }, Asks());
            }
        }

        [Fact]
        public void PageRefusal_BlocksAndSkipsWindowGate()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log) { Answer = () => Task.FromResult(false) });

                Assert.False(host.CanClose());

                Assert.Equal(new[] { "ask:B" }, Asks());
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Theory]
        [InlineData(CloseReason.SystemShutdown)]
        [InlineData(CloseReason.TaskManager)]
        public void SystemCloses_BypassPages(CloseReason reason)
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log) { Answer = () => Task.FromResult(false) });

                Assert.True(host.CanClose(reason));

                Assert.Empty(Asks());
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task BusyStack_BlocksCloseWithoutAsking()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var pending = new TaskCompletionSource<bool>();
                ws.Push(new GuardedPage("B", _log) { Answer = () => pending.Task });
                var jump = ws.Stack.PopAsync();

                Assert.False(host.CanClose());

                Assert.Equal(new[] { "ask:B" }, Asks());
                pending.SetResult(false);
                Assert.False(await Within(jump));
            }
        }

        [Fact]
        public void RequestClose_AsksPages()
        {
            using (var host = Start(out var ws))
            {
                var b = new GuardedPage("B", _log) { Answer = () => Task.FromResult(false) };
                ws.Push(b);

                ws.RequestCloseForTest();
                Assert.False(host.CloseRequested);

                b.Answer = () => Task.FromResult(true);
                ws.RequestCloseForTest();
                Assert.True(host.CloseRequested);
                Assert.Equal(new[] { "ask:B", "ask:B", "ask:window" }, Asks());
            }
        }

        [Fact]
        public async Task ReservationHeld_FromApprovalUntilHostDispose_ForRequestClose()
        {
            var host = Start(out var ws);
            ws.Push(new GuardedPage("A", _log));
            ws.Push(new GuardedPage("B", _log));

            ws.RequestCloseForTest();

            Assert.True(host.CloseRequested);
            Assert.True(ws.Stack.IsBusy);
            Assert.False(await ws.Stack.PopAsync());
            Assert.Throws<InvalidOperationException>(() => ws.Push(new PlainPage("C", _log)));
            Assert.Equal(2, ws.Stack.Count);
            host.Dispose();
        }

        [Fact]
        public async Task CanCloseQuery_ReleasesWhenAnswered()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.Push(new GuardedPage("B", _log));

                Assert.True(host.CanClose());
                Assert.False(ws.Stack.IsBusy);
                Assert.True(await host.CanCloseAsync());
                Assert.False(ws.Stack.IsBusy);

                Assert.True(await ws.Stack.PopAsync());
                Assert.Equal(1, ws.Stack.Count);
            }
        }

        [Fact]
        public async Task AsyncWindowGatePending_StacksStayBusy_ThenReleasedByTheQuery()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                var pending = new TaskCompletionSource<bool>();
                ws.WindowAnswer = _ => pending.Task;

                var decision = host.CanCloseAsync();
                Assert.False(decision.IsCompleted);
                Assert.True(ws.Stack.IsBusy);
                Assert.False(await ws.Stack.PopAsync());

                pending.SetResult(true);
                Assert.True(await Within(decision));
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public async Task AsyncPageGuard_RepeatCloseIsHeldOff_ThenWindowAsked()
        {
            using (var host = Start(out var ws))
            {
                var pending = new TaskCompletionSource<bool>();
                var a = new GuardedPage("A", _log) { Answer = () => pending.Task };
                ws.Push(a);

                var decision = host.CanCloseAsync();
                Assert.False(decision.IsCompleted);
                Assert.Null(host.CanClose());
                Assert.Equal(1, a.AskCount);

                pending.SetResult(true);
                Assert.True(await Within(decision));
                Assert.Equal(new[] { "ask:A", "ask:window" }, Asks());
            }
        }

        [Fact]
        public void RefusedByTheWindow_ReleasesStacks()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.WindowAnswer = _ => Task.FromResult(false);

                Assert.False(host.CanClose());
                ws.RequestCloseForTest();

                Assert.False(host.CloseRequested);
                Assert.False(ws.Stack.IsBusy);
            }
        }

        [Fact]
        public void WindowCanCloseAsync_ForwardingToItsStack_JoinsTheCheck()
        {
            using (var host = Start(out var ws))
            {
                var a = new GuardedPage("A", _log);
                ws.Push(a);
                ws.WindowAnswer = _ => ws.Stack.CanCloseAllAsync();

                Assert.True(host.CanClose());
                Assert.Equal(1, a.AskCount);
            }
        }

        [Fact]
        public void ThrowingPageGuard_BlocksTheClose_AndLogs()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log) { Answer = () => throw new InvalidOperationException("x") });

                Assert.False(host.CanClose());
                Assert.Contains(_fakes.Logs.Entries, e => e.Level == LogLevel.Error);
                Assert.DoesNotContain("ask:window", _log);
            }
        }

        [Fact]
        public async Task ControlOwner_HasNoGate_CanCloseAllAsyncStillWorks()
        {
            var panel = new PlainPage("panel", _log, ownsStack: true);
            using (var host = PresenterTestHost.Start(_fakes.Provider, panel, new FakePageView("panel")))
            {
                panel.PushInner(new GuardedPage("X", _log) { Answer = () => Task.FromResult(false) }, null);

                Assert.Throws<InvalidOperationException>(() => host.CanClose());
                Assert.False(await panel.Inner.CanCloseAllAsync());
            }
        }

        [Fact]
        public async Task DisposedWhileTheWindowsOwnDecisionIsPending_AnswersFalse()
        {
            var host = Start(out var ws);
            ws.Push(new GuardedPage("A", _log));
            var pending = new TaskCompletionSource<bool>();
            ws.WindowAnswer = _ => pending.Task;

            var decision = host.CanCloseAsync();
            host.Dispose();
            pending.SetResult(true);

            Assert.False(await Within(decision));
        }

        [Fact]
        public async Task QueryDuringWhichTheWindowRequestsClose_KeepsTheReservation()
        {
            using (var host = Start(out var ws))
            {
                ws.Push(new GuardedPage("A", _log));
                ws.WindowAnswer = _ => { ws.RequestCloseForTest(); return Task.FromResult(true); };

                Assert.True(await host.CanCloseAsync());

                Assert.True(host.CloseRequested);
                Assert.True(ws.Stack.IsBusy);   // a close happened: held until disposal
            }
        }

        [Fact]
        public void ApprovedReservation_IsReleasedWhenThePresenterIsDisposed()
        {
            var host = Start(out var ws);
            ws.Push(new GuardedPage("A", _log));
            ws.RequestCloseForTest();
            Assert.True(ws.Stack.IsBusy);

            host.Dispose();

            Assert.False(ws.Stack.IsBusy);
        }

        private sealed class ThrowingLoggerFactory : RecordingLoggerFactory
        {
            public override ILogger CreateLogger(string categoryName) => new Throwing();
            private sealed class Throwing : ILogger
            {
                public bool IsEnabled(LogLevel level) => true;
                public void Log(LogLevel level, Exception exception, string message, params object[] args)
                    => throw new InvalidOperationException("logger down");
            }
        }

        [Fact]
        public void AThrowingLogger_StillAnswersTheCloseAndReleasesTheStacks()
        {
            var services = TestServices.Create();
            services.RegisterInstance<ILoggerFactory>(new ThrowingLoggerFactory());
            var ws = new WorkspacePresenter(_log);
            using (var host = PresenterTestHost.Start(services, ws, _view))
            {
                ws.Push(new GuardedPage("A", _log) { Answer = () => throw new InvalidOperationException("guard") });

                Assert.False(host.CanClose());
                Assert.False(ws.Stack.IsBusy);
            }
        }

        // ---- Baseline: a window without stacks closes exactly as before ----

        private sealed class PlainWindow : WindowPresenterBase<IWorkspaceView>
        {
            public int Asked;
            public Func<Task<bool>> Answer = () => Task.FromResult(true);
            protected override Task<bool> CanCloseAsync(CloseReason reason) { Asked++; return Answer(); }
            public void Close() => RequestClose(InteractionStatus.Ok);
        }

        [Fact]
        public async Task NoStacks_GateCallsAreUnchanged()
        {
            var window = new PlainWindow();
            using (var host = PresenterTestHost.Start(_fakes.Provider, window, _view))
            {
                Assert.True(host.CanClose());                       // ✕, sync answer: decided at once
                Assert.Equal(1, window.Asked);

                window.Answer = () => Task.FromResult(false);
                Assert.False(host.CanClose());                      // veto
                Assert.Equal(2, window.Asked);

                var pending = new TaskCompletionSource<bool>();
                window.Answer = () => pending.Task;
                Assert.Null(host.CanClose());                       // async: pending
                Assert.Null(host.CanClose());                       // repeat ✕ is held off, not re-asked
                Assert.Equal(3, window.Asked);
                Assert.True(host.CanClose(CloseReason.SystemShutdown));
                Assert.Equal(3, window.Asked);
                pending.SetResult(true);
                await host.WaitForCloseDecisionAsync();

                window.Answer = () => Task.FromResult(true);
                window.Close();                                      // RequestClose runs the gate once
                Assert.Equal(4, window.Asked);
                Assert.True(host.CloseRequested);
                Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
            }
        }
    }
}
