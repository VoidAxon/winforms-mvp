using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.Common;
using WinformsMVP.Logging;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Services;
using WinformsMVP.Testing;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>
    /// Asynchronous action handlers, OnInitializeAsync and CanCloseAsync.
    /// </summary>
    public class AsyncActionTests
    {
        private static readonly ViewAction Save = ViewAction.Create("Async.Save");
        private static readonly ViewAction Load = ViewAction.Create("Async.Load");

        #region Dispatcher

        [Fact]
        public async Task AsyncHandler_IsDisabledWhileRunning_AndCompletesWithActionExecuted()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            int runs = 0, executed = 0, refreshes = 0;
            dispatcher.Register(Save, async () => { runs++; await gate.Task; });
            dispatcher.ActionExecuted += (s, e) => executed++;
            dispatcher.CanExecuteChanged += (s, e) => refreshes++;

            var first = dispatcher.DispatchAsync(Save);
            Assert.False(dispatcher.CanDispatch(Save));   // running: bound controls disabled
            Assert.Equal(1, refreshes);

            dispatcher.Dispatch(Save);                     // double-click while running
            Assert.Equal(1, runs);                         // ignored

            gate.SetResult(true);
            await first;

            Assert.True(dispatcher.CanDispatch(Save));
            Assert.Equal(1, executed);
        }

        [Fact]
        public async Task AsyncHandler_FailureAfterAwait_IsLoggedAndReEnables()
        {
            var logger = new CapturingLogger();
            var dispatcher = new ViewActionDispatcher { Logger = logger };
            int executed = 0;
            dispatcher.Register(Save, async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("save failed");
            });
            dispatcher.ActionExecuted += (s, e) => executed++;

            await dispatcher.DispatchAsync(Save);          // does not fault

            Assert.True(dispatcher.CanDispatch(Save));
            Assert.Equal(0, executed);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
        }

        [Fact]
        public async Task AsyncHandler_WithPayloadAndToken_ReceivesBoth()
        {
            var dispatcher = new ViewActionDispatcher();
            string received = null;
            bool tokenIsLive = false;
            dispatcher.Register<string>(Load, async (name, ct) =>
            {
                await Task.Yield();
                received = name;
                tokenIsLive = ct.CanBeCanceled || !ct.IsCancellationRequested;
            });

            await dispatcher.DispatchAsync(Load, "orders");

            Assert.Equal("orders", received);
            Assert.True(tokenIsLive);
        }

        [Fact]
        public async Task Middleware_SeesTheHandlersCompletionTask()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            dispatcher.Register(Save, () => (Task)gate.Task);
            Task seen = null;
            dispatcher.Use((ctx, next) => { next(ctx); seen = ctx.Completion; });

            var dispatch = dispatcher.DispatchAsync(Save);

            Assert.NotNull(seen);
            Assert.False(seen.IsCompleted);
            gate.SetResult(true);
            await dispatch;
            Assert.True(seen.IsCompleted);
        }

        [Fact]
        public async Task MiddlewareThrowsAfterStartingAsyncHandler_HandlerIsStillTracked()
        {
            var dispatcher = new ViewActionDispatcher();
            var gate = new TaskCompletionSource<bool>();
            int runs = 0, executed = 0;
            dispatcher.Register(Save, () => { runs++; return (Task)gate.Task; });
            dispatcher.Use((ctx, next) => { next(ctx); throw new InvalidOperationException("audit failed"); });
            dispatcher.ActionExecuted += (s, e) => executed++;

            var dispatch = dispatcher.DispatchAsync(Save);
            Assert.False(dispatch.IsCompleted);            // still awaiting the handler
            dispatcher.Dispatch(Save);                     // repeat while running: ignored
            Assert.Equal(1, runs);

            gate.SetResult(true);
            await dispatch;

            Assert.True(dispatcher.CanDispatch(Save));
            Assert.Equal(0, executed);                     // the dispatch failed overall
        }

        [Fact]
        public void SyncHandler_DispatchAsync_IsAlreadyComplete()
        {
            var dispatcher = new ViewActionDispatcher();
            int runs = 0;
            dispatcher.Register(Save, () => runs++);

            Assert.True(dispatcher.DispatchAsync(Save).IsCompleted);
            Assert.Equal(1, runs);
        }

        #endregion

        #region Presenter

        public interface IOrderView : IWindowView
        {
            string Status { get; set; }
        }

        private sealed class OrderView : IOrderView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public string Status { get; set; }
        }

        private sealed class OrderPresenter : WindowPresenterBase<IOrderView>
        {
            public TaskCompletionSource<bool> SaveGate = new TaskCompletionSource<bool>();
            public TaskCompletionSource<bool> LoadGate;
            public Exception LoadFailure;
            public Task<bool> CloseDecision;
            public CancellationToken SaveToken;
            public bool SaveSawCancellation;

            protected override void OnViewAttached() { }

            protected override void RegisterViewActions()
            {
                Dispatcher.Register(Save, async ct =>
                {
                    SaveToken = ct;
                    try { await SaveGate.Task.WithCancellation(ct); }
                    catch (OperationCanceledException) { SaveSawCancellation = true; throw; }
                    RequestClose("saved", InteractionStatus.Ok);
                });
            }

            protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
            {
                View.Status = "loading";
                if (LoadFailure != null) { await Task.Yield(); throw LoadFailure; }
                if (LoadGate != null) await LoadGate.Task.WithCancellation(cancellationToken);
                View.Status = "loaded";
            }

            protected override Task<bool> CanCloseAsync(CloseReason reason)
                => CloseDecision ?? base.CanCloseAsync(reason);
        }

        private static (OrderPresenter presenter, OrderView view, CapturingLogger log) NewPresenter()
        {
            var logs = new CapturingLoggerFactory();
            var services = new DefaultServiceProvider();
            services.RegisterInstance<ILoggerFactory>(logs);
            var presenter = new OrderPresenter().WithServiceProvider(services);
            return (presenter, new OrderView(), logs.Logger);
        }

        [Fact]
        public async Task AsyncAction_ThroughTheViewsBinder_ClosesWhenDone()
        {
            var (presenter, view, _) = NewPresenter();
            var host = PresenterTestHost.Start(presenter, view);

            var save = view.Binder.DispatchAsync(Save);
            Assert.False(view.Binder.CanDispatch(Save));
            presenter.SaveGate.SetResult(true);
            await save;

            Assert.Equal(InteractionStatus.Ok, host.CloseStatus);
            Assert.Equal("saved", host.CloseResult);
        }

        [Fact]
        public async Task DisposingThePresenter_CancelsTheRunningAction_Silently()
        {
            var (presenter, view, log) = NewPresenter();
            var host = PresenterTestHost.Start(presenter, view);

            var save = view.Binder.DispatchAsync(Save);
            host.Dispose();     // the window closed mid-save
            await save;

            Assert.True(presenter.SaveSawCancellation);
            Assert.False(host.CloseRequested);
            Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task OnInitializeAsync_RunsAfterInitialize_AndCanBeAwaited()
        {
            var (presenter, view, _) = NewPresenter();
            presenter.LoadGate = new TaskCompletionSource<bool>();
            var host = PresenterTestHost.Start(presenter, view);

            Assert.Equal("loading", view.Status);       // Start returned without waiting
            presenter.LoadGate.SetResult(true);
            await host.Initialized;

            Assert.Equal("loaded", view.Status);
        }

        [Fact]
        public async Task OnInitializeAsync_Failure_IsLogged()
        {
            var (presenter, view, log) = NewPresenter();
            presenter.LoadFailure = new InvalidOperationException("db down");
            var host = PresenterTestHost.Start(presenter, view);

            await host.Initialized;   // does not fault

            Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Exception?.Message == "db down");
        }

        [Fact]
        public async Task OnInitializeAsync_CancelledByDispose_IsSilent()
        {
            var (presenter, view, log) = NewPresenter();
            presenter.LoadGate = new TaskCompletionSource<bool>();
            var host = PresenterTestHost.Start(presenter, view);

            host.Dispose();
            await host.Initialized;

            Assert.Equal("loading", view.Status);
            Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task CanCloseAsync_AnswerIsAwaited()
        {
            var (presenter, view, _) = NewPresenter();
            var decision = new TaskCompletionSource<bool>();
            presenter.CloseDecision = decision.Task;
            var host = PresenterTestHost.Start(presenter, view);

            Assert.Null(host.CanClose());               // pending
            var answer = host.CanCloseAsync();
            decision.SetResult(false);

            Assert.False(await answer);
        }

        [Fact]
        public async Task CanCloseAsync_Faulted_BlocksNormalButAllowsShutdown()
        {
            var (presenter, view, log) = NewPresenter();
            presenter.CloseDecision = Faulted();
            var host = PresenterTestHost.Start(presenter, view);

            Assert.False(await host.CanCloseAsync(CloseReason.Normal));
            Assert.True(await host.CanCloseAsync(CloseReason.SystemShutdown));
            Assert.Contains(log.Entries, e => e.Level == LogLevel.Error);
        }

        private static async Task<bool> Faulted()
        {
            await Task.Yield();
            throw new InvalidOperationException("server unreachable");
        }

        #endregion
    }

    internal static class TaskTestExtensions
    {
        /// <summary>Completes with the task, or is cancelled when <paramref name="ct"/> is.</summary>
        public static async Task WithCancellation(this Task task, CancellationToken ct)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                    throw new OperationCanceledException(ct);
            }
            await task;
        }
    }
}
