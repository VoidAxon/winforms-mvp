using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services;
using Xunit;
using WF = System.Windows.Forms.CloseReason;
using FormClosingEventArgs = System.Windows.Forms.FormClosingEventArgs;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="WindowClosingBridge"/>, the public helper that lets a Form NOT shown
    /// through <see cref="IWindowNavigator"/> (an app shell / a legacy form) participate in the
    /// framework's Pull-direction <see cref="IWindowView.Closing"/> abstraction.
    /// </summary>
    public class WindowClosingBridgeTests
    {
        private sealed class PassiveView : IWindowView
        {
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
            public event EventHandler<WindowClosingEventArgs> Closing;
            public void OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        }

        [Theory]
        [InlineData(WF.UserClosing, CloseReason.Normal)]
        [InlineData(WF.WindowsShutDown, CloseReason.SystemShutdown)]
        [InlineData(WF.TaskManagerClosing, CloseReason.TaskManager)]
        [InlineData(WF.FormOwnerClosing, CloseReason.ParentClosing)]
        [InlineData(WF.MdiFormClosing, CloseReason.ParentClosing)]
        [InlineData(WF.ApplicationExitCall, CloseReason.Normal)]
        [InlineData(WF.None, CloseReason.Unknown)]
        public void MapCloseReason_MapsKnownReasons(WF input, CloseReason expected)
            => Assert.Equal(expected, WindowClosingBridge.MapCloseReason(input));

        [Fact]
        public void ForwardClosing_WhenSubscriberVetoes_SetsEventCancel()
        {
            var view = new PassiveView();
            view.Closing += (s, e) => { if (e.Reason == CloseReason.Normal) e.Cancel = true; };
            var e = new FormClosingEventArgs(WF.UserClosing, cancel: false);

            WindowClosingBridge.ForwardClosing(view, e);

            Assert.True(e.Cancel);
        }

        [Fact]
        public void ForwardClosing_WhenNoVeto_LeavesEventNotCancelled()
        {
            var view = new PassiveView();
            view.Closing += (s, e) => { /* allow */ };
            var e = new FormClosingEventArgs(WF.UserClosing, cancel: false);

            WindowClosingBridge.ForwardClosing(view, e);

            Assert.False(e.Cancel);
        }

        [Fact]
        public void ForwardClosing_PassesMappedReasonToSubscriber()
        {
            var view = new PassiveView();
            CloseReason observed = CloseReason.Unknown;
            view.Closing += (s, e) => observed = e.Reason;
            var e = new FormClosingEventArgs(WF.WindowsShutDown, cancel: false);

            WindowClosingBridge.ForwardClosing(view, e);

            Assert.Equal(CloseReason.SystemShutdown, observed);
        }
    }
}
