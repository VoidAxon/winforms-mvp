using System;
using System.Collections.Generic;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;
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
    /// <list type="bullet">
    ///   <item><description><b>Pull</b>: drive the presenter's <c>CanClose(reason)</c> gate via
    ///     <see cref="ICloseParticipant.CanCloseGate"/> and assert allow/block.</description></item>
    ///   <item><description><b>Push</b>: bind a recording <see cref="ICloseSink"/>, dispatch the
    ///     Save/Cancel action, and assert the pushed result + status.</description></item>
    /// </list>
    /// </summary>
    public class WindowClosingDemoPresenterTests
    {
        #region Test scaffolding

        // Slim view — closing is the presenter's concern (CanClose), so IWindowClosingDemoView
        // exposes no closing members.
        private sealed class MockView : IWindowClosingDemoView
        {
            public string Text { get; set; } = "";
            public string LastStatus { get; private set; }
            public string StatusMessage { set { LastStatus = value; } }
            public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;


            public event EventHandler EditChanged;

            // Test helper.
            public void SimulateEdit(string newText)
            {
                Text = newText;
                EditChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object result, InteractionStatus status)> Closed
                = new List<(object, InteractionStatus)>();
            public void Close(object result, InteractionStatus status) => Closed.Add((result, status));
        }

        private readonly MockServices _platform = new MockServices();
        private readonly MockView _view = new MockView();
        private readonly WindowClosingDemoPresenter _presenter;
        private readonly RecordingSink _sink = new RecordingSink();

        public WindowClosingDemoPresenterTests()
        {
            _presenter = new WindowClosingDemoPresenter()
                .WithServiceProvider(_platform.Provider);

            _presenter.AttachView(_view);
            _presenter.Initialize();
            ((ICloseParticipant)_presenter).BindCloseSink(_sink);
        }

        /// <summary>Drive the Pull gate for <paramref name="reason"/> and return whether it allowed the close.</summary>
        private bool RunCanClose(CloseReason reason = CloseReason.Normal)
        {
            bool allow = false;
            ((ICloseParticipant)_presenter).CanCloseGate(reason, ok => allow = ok);
            return allow;
        }

        #endregion

        // ─── Pull direction (CanClose) ───────────────────────────────────────────────

        [Fact]
        public void CanClose_WithoutEdits_AllowsClose()
        {
            Assert.True(RunCanClose());
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void CanClose_AfterEdit_UserConfirmsDiscard_AllowsClose()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = true;  // "discard"

            Assert.True(RunCanClose());
            Assert.True(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void CanClose_AfterEdit_UserDeclinesDiscard_BlocksClose()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;  // "no, keep editing"

            Assert.False(RunCanClose());
            Assert.Equal("Close cancelled. Continue editing.", _view.LastStatus);
        }

        [Fact]
        public void CanClose_OnSystemShutdown_BypassesPrompt()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;  // would block if asked

            Assert.True(RunCanClose(CloseReason.SystemShutdown));
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        [Fact]
        public void CanClose_OnTaskManagerKill_BypassesPrompt()
        {
            _view.SimulateEdit("modified");
            _platform.MessageService.ConfirmYesNoResult = false;

            Assert.True(RunCanClose(CloseReason.TaskManager));
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        // ─── Push direction (RequestClose via Save/Cancel) ───────────────────────────

        [Fact]
        public void Save_PushesTextWithOkStatus()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Save);

            Assert.Single(_sink.Closed);
            Assert.Equal("hello", _sink.Closed[0].result);
            Assert.Equal(InteractionStatus.Ok, _sink.Closed[0].status);
        }

        [Fact]
        public void Cancel_PushesCancelStatus()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Cancel);

            Assert.Single(_sink.Closed);
            Assert.Equal(InteractionStatus.Cancel, _sink.Closed[0].status);
        }

        // ─── Push then Pull (single-source-of-truth invariant) ───────────────────────

        /// <summary>
        /// After Save (Push), the framework closes the form, which runs the Pull gate again.
        /// Save finalizes the baseline before pushing, so the follow-up <c>CanClose</c> observes
        /// a clean state and allows the close without re-prompting — otherwise the user would get
        /// an unwanted confirm popup after clicking Save.
        /// </summary>
        [Fact]
        public void Save_ThenCanClose_DoesNotPrompt()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Save);
            // Simulate framework follow-up close gate after RequestClose.
            Assert.True(RunCanClose());
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }

        /// <summary>Same single-source-of-truth invariant for Cancel.</summary>
        [Fact]
        public void Cancel_ThenCanClose_DoesNotPrompt()
        {
            _view.SimulateEdit("hello");

            _presenter.Dispatch(WindowClosingDemoActions.Cancel);
            Assert.True(RunCanClose());
            Assert.False(_platform.MessageService.ConfirmDialogShown);
        }
    }
}
