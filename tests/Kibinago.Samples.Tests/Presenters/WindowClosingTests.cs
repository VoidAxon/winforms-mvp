using System;
using System.Collections.Generic;
using Kibinago.Common;
using Kibinago.Views;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Samples.Tests.Mocks;
using Xunit;
using Kibinago.Testing;

namespace Kibinago.Samples.Tests.Presenters
{
    /// <summary>
    /// Presenter-level closing-contract coverage that complements <see cref="CanCloseTests"/>.
    /// CanCloseTests already covers the synchronous gate (default-allow, dirty-blocks-Normal,
    /// shutdown-bypass), push-routes-through-sink, and the no-sink no-op.
    /// This suite adds the cases it does not exercise:
    /// <list type="bullet">
    ///   <item><description>the <b>asynchronous</b> Pull gate (decision deferred until a callback fires);</description></item>
    ///   <item><description>per-<see cref="CloseReason"/> coverage of a dirty-blocking gate (which reasons bypass);</description></item>
    ///   <item><description>the no-result base <c>RequestClose(status)</c> Push path.</description></item>
    /// </list>
    /// </summary>
    public class WindowClosingTests
    {
        #region Test doubles

        public interface IFakeView : IWindowView
        {
            bool HasUnsavedChanges { get; set; }
        }

        private sealed class FakeView : IFakeView
        {
            public bool HasUnsavedChanges { get; set; }
            public IViewActionBinder ActionBinder { get; } = new CapturingActionBinder();
        }

        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object result, InteractionStatus status)> Closed
                = new List<(object, InteractionStatus)>();
            public void Close(object result, InteractionStatus status) => Closed.Add((result, status));
        }

        /// <summary>
        /// Presenter whose Pull gate is asynchronous: <c>CanCloseAsync</c> returns a pending task that
        /// a test completes through <see cref="Pending"/>, driving the deferred decision explicitly.
        /// </summary>
        private sealed class AsyncPresenter : WindowPresenterBase<IFakeView>
        {
            private System.Threading.Tasks.TaskCompletionSource<bool> _decision;
            public Action<bool> Pending => ok => _decision.SetResult(ok);
            protected override System.Threading.Tasks.Task<bool> CanCloseAsync(CloseReason reason)
            {
                _decision = new System.Threading.Tasks.TaskCompletionSource<bool>(
                    System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
                return _decision.Task;
            }
        }

        /// <summary>
        /// Presenter with a synchronous gate that blocks only a dirty <see cref="CloseReason.Normal"/>
        /// close; every non-Normal reason bypasses the dirty check (shutdown / task-manager / parent /
        /// unknown must never be blocked by a modal prompt). Also exercises the no-result Push path.
        /// </summary>
        private sealed class DirtyGuardPresenter : WindowPresenterBase<IFakeView>
        {
            protected override bool CanClose(CloseReason reason)
            {
                if (reason != CloseReason.Normal) return true;
                return !View.HasUnsavedChanges;
            }
            public void PushNoResult(InteractionStatus status) => RequestClose(status);
        }

        private static T Attached<T>(FakeView view) where T : WindowPresenterBase<IFakeView>, new()
        {
            var p = new T();
            // Drives the framework contract directly (no recording sink), as the hosts do.
            ((IViewAttacher<IFakeView>)p).AttachView(view);
            ((IInitializable)p).Initialize();
            return p;
        }

        #endregion

        #region Asynchronous Pull gate

        [Fact]
        public async System.Threading.Tasks.Task AsyncCanClose_DefersUntilProceed_ThenAllows()
        {
            var p = Attached<AsyncPresenter>(new FakeView());
            var answer = new System.Threading.Tasks.TaskCompletionSource<bool>();

            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => answer.TrySetResult(ok));

            Assert.False(answer.Task.IsCompleted);   // not answered yet — the gate deferred the decision
            p.Pending(true);
            Assert.True(await answer.Task);         // proceeds once the presenter's task completes
        }

        [Fact]
        public async System.Threading.Tasks.Task AsyncCanClose_DefersUntilProceed_ThenBlocks()
        {
            var p = Attached<AsyncPresenter>(new FakeView());
            var answer = new System.Threading.Tasks.TaskCompletionSource<bool>();

            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => answer.TrySetResult(ok));

            Assert.False(answer.Task.IsCompleted);   // not answered yet — the gate deferred the decision
            p.Pending(false);
            Assert.False(await answer.Task);         // proceeds once the presenter's task completes
        }

        #endregion

        #region CloseReason coverage of a dirty-blocking gate

        [Theory]
        [InlineData(CloseReason.SystemShutdown)]
        [InlineData(CloseReason.TaskManager)]
        [InlineData(CloseReason.ParentClosing)]
        [InlineData(CloseReason.Unknown)]
        public void DirtyGate_NonNormalReason_BypassesDirtyCheck(CloseReason reason)
        {
            var p = Attached<DirtyGuardPresenter>(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;

            ((ICloseParticipant)p).CanCloseGate(reason, ok => allow = ok);

            Assert.True(allow);   // only Normal performs the dirty check
        }

        [Fact]
        public void DirtyGate_NormalReason_Dirty_Blocks()
        {
            var p = Attached<DirtyGuardPresenter>(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;

            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);

            Assert.False(allow);
        }

        #endregion

        #region No-result Push path

        [Fact]
        public void RequestClose_NoResult_RoutesStatusThroughSink_WithNullResult()
        {
            var p = Attached<DirtyGuardPresenter>(new FakeView());
            var sink = new RecordingSink();
            ((ICloseParticipant)p).BindCloseSink(sink);

            p.PushNoResult(InteractionStatus.Cancel);

            Assert.Single(sink.Closed);
            Assert.Null(sink.Closed[0].result);
            Assert.Equal(InteractionStatus.Cancel, sink.Closed[0].status);
        }

        #endregion
    }
}
