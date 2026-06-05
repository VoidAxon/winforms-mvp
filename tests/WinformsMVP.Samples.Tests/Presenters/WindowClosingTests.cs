using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
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
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
        }

        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object result, InteractionStatus status)> Closed
                = new List<(object, InteractionStatus)>();
            public void Close(object result, InteractionStatus status) => Closed.Add((result, status));
        }

        /// <summary>
        /// Presenter whose Pull gate is asynchronous: it stores the <c>proceed</c> callback in
        /// <see cref="Pending"/> instead of answering inline, so a test can drive the deferred
        /// decision explicitly.
        /// </summary>
        private sealed class AsyncPresenter : WindowPresenterBase<IFakeView>
        {
            public Action<bool> Pending;
            protected override void OnViewAttached() { }
            protected override void CanClose(CloseReason reason, Action<bool> proceed) => Pending = proceed;
        }

        /// <summary>
        /// Presenter with a synchronous gate that blocks only a dirty <see cref="CloseReason.Normal"/>
        /// close; every non-Normal reason bypasses the dirty check (shutdown / task-manager / parent /
        /// unknown must never be blocked by a modal prompt). Also exercises the no-result Push path.
        /// </summary>
        private sealed class DirtyGuardPresenter : WindowPresenterBase<IFakeView>
        {
            protected override void OnViewAttached() { }
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
            p.AttachView(view);
            p.Initialize();
            return p;
        }

        #endregion

        #region Asynchronous Pull gate

        [Fact]
        public void AsyncCanClose_DefersUntilProceed_ThenAllows()
        {
            var p = Attached<AsyncPresenter>(new FakeView());
            bool? allow = null;

            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);

            Assert.Null(allow);   // not answered yet — the gate deferred the decision
            p.Pending(true);
            Assert.True(allow);   // proceeds once the stored callback fires
        }

        [Fact]
        public void AsyncCanClose_DefersUntilProceed_ThenBlocks()
        {
            var p = Attached<AsyncPresenter>(new FakeView());
            bool? allow = null;

            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);

            Assert.Null(allow);
            p.Pending(false);
            Assert.False(allow);  // blocks once the stored callback fires with false
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
