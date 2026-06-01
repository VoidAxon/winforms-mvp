using System;
using WinformsMVP.Common;
using WinformsMVP.Common.Events;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;
using WinformsMVP.Samples.WindowClosingDemo;
using Xunit;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Tests for the minimal Window Closing demo. These tests serve double-duty as
    /// documentation: they show the canonical interaction patterns for testing both
    /// the Push and Pull directions of the framework's close model.
    /// </summary>
    public class WindowClosingDemoPresenterTests
    {
        #region Test scaffolding

        private class MockView : IWindowClosingDemoView
        {
            public string Text { get; set; } = "";
            public string LastStatus { get; private set; }
            public string StatusMessage { set { LastStatus = value; } }
            public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;

            public bool IsDisposed { get; private set; }
            public IntPtr Handle => IntPtr.Zero;
            public void Activate() { }

            public event EventHandler EditChanged;
            public event EventHandler<WindowClosingEventArgs> Closing;

            public void OnClosing(WindowClosingEventArgs args)
                => Closing?.Invoke(this, args);

            // Test helpers.
            public void SimulateEdit(string newText)
            {
                Text = newText;
                EditChanged?.Invoke(this, EventArgs.Empty);
            }

            public WindowClosingEventArgs RaiseClosing(CloseReason reason = CloseReason.Normal)
            {
                var args = new WindowClosingEventArgs(reason);
                OnClosing(args);
                return args;
            }
        }

        private readonly MockPlatformServices _platform = new MockPlatformServices();
        private readonly MockView _view = new MockView();
        private readonly WindowClosingDemoPresenter _presenter;

        public WindowClosingDemoPresenterTests()
        {
            _presenter = new WindowClosingDemoPresenter()
                .WithPlatformServices(_platform);

            _presenter.AttachView(_view);
            _presenter.Initialize();
        }

        #endregion

        // ─── Pull direction ──────────────────────────────────────────────────────────

        [Fact]
        public void Closing_WithoutEdits_AllowsClose()
        {
            var args = _view.RaiseClosing();

            Assert.False(args.Cancel);
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void Closing_AfterEdit_UserConfirmsDiscard_AllowsClose()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = true;  // "discard"

            var args = _view.RaiseClosing();

            Assert.False(args.Cancel);
            Assert.True(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void Closing_AfterEdit_UserDeclinesDiscard_BlocksClose()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;  // "no, keep editing"

            var args = _view.RaiseClosing();

            Assert.True(args.Cancel);
            Assert.Equal("Close cancelled. Continue editing.", _view.LastStatus);
        }

        [Fact]
        public void Closing_OnSystemShutdown_BypassesPrompt()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;  // would block if asked

            var args = _view.RaiseClosing(CloseReason.SystemShutdown);

            Assert.False(args.Cancel);
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void Closing_OnTaskManagerKill_BypassesPrompt()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;

            var args = _view.RaiseClosing(CloseReason.TaskManager);

            Assert.False(args.Cancel);
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        // ─── Push direction ──────────────────────────────────────────────────────────

        [Fact]
        public void Save_FiresCloseRequestedWithText()
        {
            _view.SimulateEdit("hello");

            CloseRequestedEventArgs<string> captured = null;
            _presenter.CloseRequested += (s, e) => captured = e;

            _presenter.Dispatch(WindowClosingDemoActions.Save);

            Assert.NotNull(captured);
            Assert.Equal("hello", captured.Result);
            Assert.Equal(InteractionStatus.Ok, captured.Status);
        }

        [Fact]
        public void Cancel_FiresCloseRequestedWithCancelStatus()
        {
            _view.SimulateEdit("hello");

            CloseRequestedEventArgs<string> captured = null;
            _presenter.CloseRequested += (s, e) => captured = e;

            _presenter.Dispatch(WindowClosingDemoActions.Cancel);

            Assert.NotNull(captured);
            Assert.Equal(InteractionStatus.Cancel, captured.Status);
        }

        // ─── Push then Pull (single-source-of-truth invariant) ───────────────────────

        /// <summary>
        /// After Save (Push direction), the framework will call Form.Close() which fires
        /// IWindowView.Closing. At that point the dirty flag must already be cleared so
        /// that the OnViewClosing handler observes "no changes" and does not prompt —
        /// otherwise the user gets an unwanted confirm popup after clicking Save.
        /// </summary>
        [Fact]
        public void Save_ThenClosing_DoesNotPrompt()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Save);
            // Simulate framework follow-up Closing after RequestClose.
            var args = _view.RaiseClosing();

            Assert.False(args.Cancel);
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        /// <summary>
        /// Same single-source-of-truth invariant for Cancel.
        /// </summary>
        [Fact]
        public void Cancel_ThenClosing_DoesNotPrompt()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Cancel);
            var args = _view.RaiseClosing();

            Assert.False(args.Cancel);
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }
    }
}
