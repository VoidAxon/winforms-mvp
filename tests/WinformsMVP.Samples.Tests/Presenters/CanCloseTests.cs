using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    public class CanCloseTests
    {
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

        private sealed class EditPresenter : WindowPresenterBase<IFakeView>
        {
            protected override void OnViewAttached() { }
            protected override bool CanClose(CloseReason reason)
            {
                if (reason != CloseReason.Normal) return true;
                return !View.HasUnsavedChanges;
            }
            public void PushSave(string r) => RequestClose(r, InteractionStatus.Ok);
        }

        private static EditPresenter Attached(FakeView view)
        {
            var p = new EditPresenter();
            p.AttachView(view);
            p.Initialize();
            return p;
        }

        [Fact]
        public void CanClose_Default_Allows()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = false });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);
            Assert.True(allow);
        }

        [Fact]
        public void CanClose_DirtyNormal_Blocks()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.Normal, ok => allow = ok);
            Assert.False(allow);
        }

        [Fact]
        public void CanClose_SystemShutdown_BypassesDirty()
        {
            var p = Attached(new FakeView { HasUnsavedChanges = true });
            bool? allow = null;
            ((ICloseParticipant)p).CanCloseGate(CloseReason.SystemShutdown, ok => allow = ok);
            Assert.True(allow);
        }

        [Fact]
        public void RequestClose_RoutesResultThroughInjectedSink()
        {
            var p = Attached(new FakeView());
            var sink = new RecordingSink();
            ((ICloseParticipant)p).BindCloseSink(sink);

            p.PushSave("hello");

            Assert.Single(sink.Closed);
            Assert.Equal("hello", sink.Closed[0].result);
            Assert.Equal(InteractionStatus.Ok, sink.Closed[0].status);
        }

        [Fact]
        public void RequestClose_WithoutSink_DoesNotThrow()
        {
            var p = Attached(new FakeView());
            // No BindCloseSink call — sink is null; must be a silent no-op.
            p.PushSave("x");
        }

    }
}
