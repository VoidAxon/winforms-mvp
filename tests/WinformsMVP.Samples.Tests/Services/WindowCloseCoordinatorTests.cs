using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Services.Implementations;
using Xunit;

namespace WinformsMVP.Samples.Tests.Services
{
    /// <summary>
    /// Tests for <see cref="WindowCloseCoordinator"/>, the per-window state that lets
    /// <see cref="WindowNavigator"/> skip the <see cref="IWindowView.Closing"/> cancellation
    /// gate for Presenter-initiated closes. This is the logic that removes the old
    /// "finalize the dirty flag before RaiseClose" timing dependency: a Save/Cancel that the
    /// Presenter drives must never be vetoed by its own dirty-state prompt.
    /// </summary>
    public class WindowCloseCoordinatorTests
    {
        /// <summary>
        /// Passive view: <see cref="OnClosing"/> only forwards the event, exactly like a real
        /// Form. The veto comes from a subscriber (a Presenter's Closing handler), never the
        /// view itself.
        /// </summary>
        private sealed class PassiveView : IWindowView
        {
            public bool IsDisposed => false;
            public IntPtr Handle => IntPtr.Zero;
            public IViewActionBinder ActionBinder => NullViewActionBinder.Instance;
            public void Activate() { }
            public event EventHandler<WindowClosingEventArgs> Closing;
            public void OnClosing(WindowClosingEventArgs args) => Closing?.Invoke(this, args);
        }

        [Fact]
        public void UserClose_WhenSubscriberVetoes_IsCancelled()
        {
            var view = new PassiveView();
            // Mimic a dirty Presenter blocking a normal user close.
            view.Closing += (s, e) => { if (e.Reason == CloseReason.Normal) e.Cancel = true; };
            var sut = new WindowCloseCoordinator(view);

            Assert.True(sut.ShouldCancel(CloseReason.Normal));
        }

        [Fact]
        public void UserClose_WhenNoSubscriberVetoes_IsNotCancelled()
        {
            var view = new PassiveView();
            view.Closing += (s, e) => { /* allow */ };
            var sut = new WindowCloseCoordinator(view);

            Assert.False(sut.ShouldCancel(CloseReason.Normal));
        }

        [Fact] // Regression: a Presenter-initiated close must not run — let alone be vetoed by — the gate.
        public void PresenterInitiatedClose_DoesNotRunSubscriberGate()
        {
            var view = new PassiveView();
            bool gateRan = false;
            view.Closing += (s, e) => { gateRan = true; e.Cancel = true; };
            var sut = new WindowCloseCoordinator(view);

            sut.BeginPresenterClose();

            Assert.False(sut.ShouldCancel(CloseReason.Normal));
            Assert.False(gateRan, "Presenter-initiated close must not invoke the View.Closing gate.");
        }

        [Fact] // The Presenter-initiated flag is consumed once: if that close is vetoed elsewhere,
               // the next (user-driven) close must run the gate again, not be silently skipped.
        public void PresenterInitiatedFlag_IsConsumedOnce()
        {
            var view = new PassiveView();
            bool gateRan = false;
            view.Closing += (s, e) => { gateRan = true; e.Cancel = true; };
            var sut = new WindowCloseCoordinator(view);

            sut.BeginPresenterClose();

            // First close: Presenter-initiated, gate skipped.
            Assert.False(sut.ShouldCancel(CloseReason.Normal));
            Assert.False(gateRan);

            // Second close (e.g. the previous one was vetoed by another handler): gate runs again.
            Assert.True(sut.ShouldCancel(CloseReason.Normal));
            Assert.True(gateRan);
        }

        [Fact] // System-level closes still reach the subscriber, which decides to bypass the prompt itself.
        public void SystemShutdown_StillForwardsToSubscriber()
        {
            var view = new PassiveView();
            bool gateRan = false;
            view.Closing += (s, e) => gateRan = true;
            var sut = new WindowCloseCoordinator(view);

            Assert.False(sut.ShouldCancel(CloseReason.SystemShutdown));
            Assert.True(gateRan);
        }
    }
}
