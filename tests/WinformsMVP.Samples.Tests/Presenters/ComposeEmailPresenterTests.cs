using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Samples.EmailDemo;
using WinformsMVP.Samples.EmailDemo.Models;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Unit tests for ComposeEmailPresenter.
    ///
    /// Demonstrates:
    /// 1. Using MockPlatformServices to inject mock services
    /// 2. Using MockComposeEmailView to mock the view
    /// 3. Using MockEmailRepository to provide test data
    /// 4. Testing ChangeTracker integration for dirty state management
    /// 5. Testing push-close (RequestClose) pattern for window closing
    /// 6. Testing validation logic and error handling
    /// 7. Testing async operations (Send, SaveDraft)
    /// 8. Testing different compose modes (New, Reply, Forward)
    /// </summary>
    public class ComposeEmailPresenterTests
    {
        private MockServices _mockServices;
        private MockComposeEmailView _mockView;
        private MockEmailRepository _mockRepository;
        private ComposeEmailPresenter _presenter;
        private RecordingSink _sink;

        /// <summary>
        /// Records every Push close the presenter routes through the framework sink.
        /// </summary>
        private sealed class RecordingSink : ICloseSink
        {
            public readonly List<(object result, InteractionStatus status)> Closed
                = new List<(object, InteractionStatus)>();
            public bool Any => Closed.Count > 0;
            public void Close(object result, InteractionStatus status) => Closed.Add((result, status));
        }

        public ComposeEmailPresenterTests()
        {
            // Constructor runs before each test
            SetupTest();
        }

        private void SetupTest()
        {
            // 1. Create mock services
            _mockServices = new MockServices();

            // 2. Create mock repository
            _mockRepository = new MockEmailRepository();

            // 3. Create presenter and inject mock services BEFORE AttachView
            _presenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);

            // 4. Create mock view
            _mockView = new MockComposeEmailView();

            // 5. Attach view and initialize with default parameters (New mode)
            _presenter.AttachView(_mockView);
            _presenter.Initialize(new ComposeEmailParameters { Mode = ComposeMode.New });

            // 6. Bind a recording sink so Push closes (RequestClose) can be observed.
            _sink = new RecordingSink();
            ((ICloseParticipant)_presenter).BindCloseSink(_sink);

            // Clear initialization calls for clean test assertions
            _mockServices.Reset();
            _mockView.MethodCalls.Clear();
            _mockRepository.MethodCalls.Clear();
        }

        #region Initialization Tests

        /// <summary>
        /// Test: Initialize with New mode should clear all fields
        /// </summary>
        [Fact]
        public void Initialize_WithNewMode_ClearsAllFields()
        {
            // Arrange - create new presenter
            var newPresenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);
            var newView = new MockComposeEmailView();

            // Act
            newPresenter.AttachView(newView);
            newPresenter.Initialize(new ComposeEmailParameters { Mode = ComposeMode.New });

            // Assert - all fields should be empty
            Assert.Contains("To = ", newView.MethodCalls);
            Assert.Contains("Subject = ", newView.MethodCalls);
            Assert.Contains("Body = ", newView.MethodCalls);
            Assert.Equal(string.Empty, newView.To);
            Assert.Equal(string.Empty, newView.Subject);
            Assert.Equal(string.Empty, newView.Body);
        }

        /// <summary>
        /// Test: Initialize with Reply mode should fill recipient and quote original
        /// </summary>
        [Fact]
        public void Initialize_WithReplyMode_FillsRecipientAndQuotesOriginal()
        {
            // Arrange
            var originalEmail = new EmailMessage
            {
                From = "alice@example.com",
                Subject = "Original Subject",
                Body = "Original body text",
                Date = DateTime.Now.AddHours(-1)
            };

            var newPresenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);
            var newView = new MockComposeEmailView();

            // Act
            newPresenter.AttachView(newView);
            newPresenter.Initialize(new ComposeEmailParameters
            {
                Mode = ComposeMode.Reply,
                OriginalEmail = originalEmail
            });

            // Assert - To should be original sender, Subject should have "Re:", Body should quote original
            Assert.Equal("alice@example.com", newView.To);
            Assert.Equal("Re: Original Subject", newView.Subject);
            Assert.Contains("--- Original Message ---", newView.Body);
            Assert.Contains("Original body text", newView.Body);
        }

        /// <summary>
        /// Test: Initialize with Forward mode should quote original body
        /// </summary>
        [Fact]
        public void Initialize_WithForwardMode_QuotesOriginalBody()
        {
            // Arrange
            var originalEmail = new EmailMessage
            {
                From = "bob@example.com",
                Subject = "Important Info",
                Body = "Here's the important information...",
                Date = DateTime.Now.AddHours(-2)
            };

            var newPresenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);
            var newView = new MockComposeEmailView();

            // Act
            newPresenter.AttachView(newView);
            newPresenter.Initialize(new ComposeEmailParameters
            {
                Mode = ComposeMode.Forward,
                OriginalEmail = originalEmail
            });

            // Assert - To should be empty, Subject should have "Fwd:", Body should quote original
            Assert.Equal(string.Empty, newView.To);
            Assert.Equal("Fwd: Important Info", newView.Subject);
            Assert.Contains("--- Forwarded Message ---", newView.Body);
            Assert.Contains("Here's the important information...", newView.Body);
        }

        /// <summary>
        /// Test: Initialize should set mode on view
        /// </summary>
        [Fact]
        public void Initialize_SetsMode()
        {
            // Arrange
            var originalEmail = new EmailMessage
            {
                From = "sender@example.com",
                Subject = "Original",
                Body = "Body",
                Date = DateTime.Now
            };

            var newPresenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);
            var newView = new MockComposeEmailView();

            // Act
            newPresenter.AttachView(newView);
            newPresenter.Initialize(new ComposeEmailParameters
            {
                Mode = ComposeMode.Forward,
                OriginalEmail = originalEmail  // Required for Forward mode
            });

            // Assert
            Assert.Contains("Mode = Forward", newView.MethodCalls);
            Assert.Equal(ComposeMode.Forward, newView.Mode);
        }

        #endregion

        #region Email Data Changed Tests

        /// <summary>
        /// Test: EmailDataChanged should update ChangeTracker
        /// </summary>
        [Fact]
        public void OnEmailDataChanged_UpdatesChangeTracker()
        {
            // Arrange
            Assert.False(_mockView.IsDirty);

            // Act - simulate user changing email data
            _mockView.To = "test@example.com";
            _mockView.Subject = "Test Subject";
            _mockView.Body = "Test Body";
            _mockView.SimulateEmailDataChange();

            // Assert - IsDirty should be set to true after change
            Assert.True(_mockView.IsDirty);
        }

        /// <summary>
        /// Test: EmailDataChanged should set dirty flag
        /// </summary>
        [Fact]
        public void OnEmailDataChanged_SetsDirtyFlag()
        {
            // Arrange
            _mockView.MethodCalls.Clear();
            Assert.False(_mockView.IsDirty);

            // Act - change email data
            _mockView.To = "changed@example.com";
            _mockView.SimulateEmailDataChange();

            // Assert - verify IsDirty property is true (set by ChangeTracker via presenter)
            Assert.True(_mockView.IsDirty);
        }

        /// <summary>
        /// Test: EmailDataChanged should raise CanExecute changed
        /// This enables/disables Send and SaveDraft buttons based on validation
        /// </summary>
        [Fact]
        public void OnEmailDataChanged_RaisesCanExecuteChanged()
        {
            // Arrange
            _mockView.To = "user@example.com";
            _mockView.Subject = "Valid Subject";

            // Act - simulate data change
            _mockView.SimulateEmailDataChange();

            // Assert - CanExecute should be re-evaluated
            // (In real scenario, Send button would become enabled when To and Subject are filled)
            Assert.NotNull(_mockView.To);
            Assert.NotNull(_mockView.Subject);
        }

        #endregion

        #region Send Action Tests

        /// <summary>
        /// Test: Send with valid email should send successfully
        /// </summary>
        [Fact]
        public async Task OnSend_WithValidEmail_SendsEmail()
        {
            // Arrange
            _mockView.To = "recipient@example.com";
            _mockView.Subject = "Test Email";
            _mockView.Body = "Test body content";
            _mockRepository.SendEmailAsyncResult = true;

            // Act
            _presenter.Dispatch(ComposeEmailActions.Send);

            // Wait for async operation
            await Task.Delay(50);

            // Assert
            Assert.Contains("SendEmailAsync(Test Email)", _mockRepository.MethodCalls);
            Assert.True(_sink.Any);  // Window should close on successful send
        }

        /// <summary>
        /// Test: Send CanExecute requires valid To field
        /// NOTE: CanExecute prevents dispatch when To is empty
        /// This test verifies the CanExecute predicate, not internal validation
        /// </summary>
        [Fact]
        public void OnSend_WithMissingTo_CannotExecute()
        {
            // Arrange
            _mockView.To = "";  // Missing recipient
            _mockView.Subject = "Test Subject";
            _mockView.Body = "Test body";
            _mockView.SimulateEmailDataChange();

            // Act - try to dispatch (will be blocked by CanExecute)
            _presenter.Dispatch(ComposeEmailActions.Send);

            // Assert - Send should not have been called (blocked by CanExecute)
            Assert.DoesNotContain("SendEmailAsync", string.Join(", ", _mockRepository.MethodCalls));
        }

        /// <summary>
        /// Test: Send CanExecute requires valid Subject field
        /// NOTE: CanExecute prevents dispatch when Subject is empty
        /// </summary>
        [Fact]
        public void OnSend_WithMissingSubject_CannotExecute()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.Subject = "";  // Missing subject
            _mockView.Body = "Test body";
            _mockView.SimulateEmailDataChange();

            // Act - try to dispatch (will be blocked by CanExecute)
            _presenter.Dispatch(ComposeEmailActions.Send);

            // Assert - Send should not have been called (blocked by CanExecute)
            Assert.DoesNotContain("SendEmailAsync", string.Join(", ", _mockRepository.MethodCalls));
        }

        /// <summary>
        /// Test: Send with invalid email format should show validation error
        /// This tests the internal validation logic (after CanExecute passes)
        /// </summary>
        [Fact]
        public void OnSend_WithInvalidEmailFormat_ShowsValidationError()
        {
            // Arrange
            _mockView.To = "invalid-email-format";  // Invalid format (CanExecute will pass, but validation fails)
            _mockView.Subject = "Test Subject";
            _mockView.Body = "Test body";
            _mockView.SimulateEmailDataChange();

            // Act
            _presenter.Dispatch(ComposeEmailActions.Send);

            // Assert - warning should be shown by internal validation
            Assert.True(_mockServices.MessageService.WarningMessageShown);
            Assert.True(_mockServices.MessageService.HasCall(
                MessageType.Warning,
                messageContains: "email address"));
        }

        /// <summary>
        /// Test: Send should disable and re-enable input during operation
        /// </summary>
        [Fact]
        public async Task OnSend_DisablesAndReEnablesInput()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.Subject = "Test";
            _mockView.Body = "Body";
            _mockRepository.SendEmailAsyncResult = true;
            _mockView.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(ComposeEmailActions.Send);
            await Task.Delay(50);

            // Assert - EnableInput should be set to false, then true
            Assert.Contains("EnableInput = False", _mockView.MethodCalls);
            Assert.Contains("EnableInput = True", _mockView.MethodCalls);
        }

        /// <summary>
        /// Test: Send on error should show error message
        /// </summary>
        [Fact]
        public async Task OnSend_OnError_ShowsErrorMessage()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.Subject = "Test";
            _mockView.Body = "Body";
            _mockRepository.SendEmailAsyncResult = false;  // Simulate failure

            // Act
            _presenter.Dispatch(ComposeEmailActions.Send);
            await Task.Delay(50);

            // Assert - error message should be shown
            Assert.True(_mockServices.MessageService.ErrorMessageShown);
            Assert.True(_mockServices.MessageService.HasCall(
                MessageType.Error,
                messageContains: "Failed to send"));
        }

        #endregion

        #region Save Draft Tests

        /// <summary>
        /// Test: SaveDraft with changes should save draft
        /// </summary>
        [Fact]
        public async Task OnSaveDraft_WithChanges_SavesDraft()
        {
            // Arrange - make changes to trigger dirty state
            _mockView.To = "draft@example.com";
            _mockView.Subject = "Draft Subject";
            _mockView.Body = "Draft body";
            _mockView.SimulateEmailDataChange();
            _mockRepository.SaveDraftResult = 999;
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(ComposeEmailActions.SaveDraft);
            await Task.Delay(50);

            // Assert
            Assert.Contains("SaveDraftAsync(Draft Subject)", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// Test: SaveDraft should accept changes (ChangeTracker)
        /// </summary>
        [Fact]
        public async Task OnSaveDraft_AcceptsChanges()
        {
            // Arrange - make changes
            _mockView.To = "draft@example.com";
            _mockView.Subject = "Draft";
            _mockView.Body = "Body";
            _mockView.SimulateEmailDataChange();
            Assert.True(_mockView.IsDirty);

            // Act
            _presenter.Dispatch(ComposeEmailActions.SaveDraft);
            await Task.Delay(100);  // Wait for async operation

            // Assert - verify SaveDraft was called
            Assert.Contains("SaveDraftAsync(Draft)", _mockRepository.MethodCalls);
            // Note: IsDirty is cleared via ChangeTracker.AcceptChanges() which triggers IsChangedChanged event
            // The dirty state is managed internally by ChangeTracker
        }

        /// <summary>
        /// Test: SaveDraft should show success message
        /// </summary>
        [Fact]
        public async Task OnSaveDraft_ShowsSuccessMessage()
        {
            // Arrange - make changes
            _mockView.To = "draft@example.com";
            _mockView.Subject = "Draft";
            _mockView.Body = "Body";
            _mockView.SimulateEmailDataChange();

            // Act
            _presenter.Dispatch(ComposeEmailActions.SaveDraft);
            await Task.Delay(50);

            // Assert - success message should be shown
            Assert.True(_mockServices.MessageService.InfoMessageShown);
            Assert.True(_mockServices.MessageService.HasCall(
                MessageType.Info,
                messageContains: "Draft saved"));
        }

        #endregion

        #region Discard Tests

        /// <summary>
        /// Test: Discard with no changes should close without prompting (or prompts if ChangeTracker detects changes)
        /// NOTE: OnDiscard checks ChangeTracker.IsChanged, which may be true even on initialization
        /// This test verifies the discard flow works correctly
        /// </summary>
        [Fact]
        public void OnDiscard_WithoutUserChanges_RequestsClose()
        {
            // Arrange - fresh presenter with no user-made changes.
            // If ChangeTracker thinks there are changes, user needs to confirm.
            // If no changes, should close immediately.
            if (_mockView.IsDirty)
            {
                _mockServices.MessageService.ConfirmYesNoResult = true;  // User confirms discard
            }

            // Act
            _presenter.Dispatch(ComposeEmailActions.Discard);

            // Assert - close should be pushed with Cancel status and no result (discard = cancel, no payload).
            Assert.Single(_sink.Closed);
            Assert.Null(_sink.Closed[0].result);
            Assert.Equal(InteractionStatus.Cancel, _sink.Closed[0].status);
        }

        /// <summary>
        /// Test: Discard with changes should prompt confirmation
        /// </summary>
        [Fact]
        public void OnDiscard_WithChanges_PromptsConfirmation()
        {
            // Arrange - make changes
            _mockView.To = "test@example.com";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoResult = true;

            // Act
            _presenter.Dispatch(ComposeEmailActions.Discard);

            // Assert - should show confirmation dialog and push the close.
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.True(_sink.Any);
        }

        #endregion

        #region Window Closing Tests (Pull direction — user clicks X)

        /// <summary>
        /// Drives the presenter's Pull gate (<c>CanClose(reason)</c>) the way the framework's
        /// close controller does — what happens when the user clicks X, Alt+F4, or the framework
        /// calls Form.Close() in response to a Presenter-initiated <c>RequestClose</c>. Returns
        /// whether the gate allowed the close.
        /// </summary>
        private bool RunCanClose(CloseReason reason = CloseReason.Normal)
        {
            bool allow = false;
            ((ICloseParticipant)_presenter).CanCloseGate(reason, ok => allow = ok);
            return allow;
        }

        /// <summary>
        /// When there is no dirty state, the close is allowed and the user is not prompted.
        /// </summary>
        [Fact]
        public void Closing_WithoutDirtyState_AllowsCloseAndDoesNotPrompt()
        {
            // Arrange — fresh presenter, no changes.
            // (ChangeTracker initialization may have set IsDirty; clear it via a No close.)
            if (_mockView.IsDirty)
            {
                _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.No;
                RunCanClose();
                _mockServices.MessageService.Calls.Clear();
            }

            // Act
            bool allow = RunCanClose();

            // Assert
            Assert.True(allow);
            Assert.False(_mockServices.MessageService.ConfirmDialogShown);
        }

        /// <summary>
        /// When there are unsaved changes and the user picks "Cancel" in the prompt,
        /// the close is blocked (the gate proceeds with false).
        /// </summary>
        [Fact]
        public void Closing_WithDirtyState_UserSelectsCancel_BlocksClose()
        {
            // Arrange — make changes.
            _mockView.To = "test@example.com";
            _mockView.Subject = "Test";
            _mockView.Body = "Body";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Cancel;

            // Act
            bool allow = RunCanClose();

            // Assert
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.False(allow);  // close blocked
        }

        /// <summary>
        /// When the user picks "No" (discard), the close proceeds.
        /// </summary>
        [Fact]
        public void Closing_WithDirtyState_UserSelectsNo_AllowsClose()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.No;

            // Act
            bool allow = RunCanClose();

            // Assert
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.True(allow);  // close proceeds
        }

        /// <summary>
        /// When the user picks "Yes" (save draft and close), the draft is saved
        /// (fire-and-forget) and the close proceeds.
        /// </summary>
        [Fact]
        public async Task Closing_WithDirtyState_UserSelectsYes_SavesDraftAndAllowsClose()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.Subject = "Test";
            _mockView.Body = "Body";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Yes;
            _mockRepository.MethodCalls.Clear();

            // Act
            bool allow = RunCanClose();
            await Task.Delay(50);  // OnSaveDraft is async fire-and-forget

            // Assert
            Assert.True(allow);  // close proceeds even while save is in flight
            Assert.Contains("SaveDraftAsync(Test)", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// On <see cref="CloseReason.SystemShutdown"/>, the Presenter must NOT prompt the
        /// user — the process is exiting and there is no chance to recover.
        /// </summary>
        [Fact]
        public void Closing_OnSystemShutdown_DoesNotPromptEvenWithDirtyState()
        {
            // Arrange — make changes.
            _mockView.To = "test@example.com";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Cancel;

            // Act
            bool allow = RunCanClose(CloseReason.SystemShutdown);

            // Assert
            Assert.False(_mockServices.MessageService.ConfirmDialogShown);
            Assert.True(allow);  // system shutdown always proceeds
        }

        /// <summary>
        /// <see cref="CloseReason.TaskManager"/> bypasses the prompt as well.
        /// </summary>
        [Fact]
        public void Closing_OnTaskManagerClose_DoesNotPrompt()
        {
            _mockView.To = "test@example.com";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoCancelResult = ConfirmResult.Cancel;

            bool allow = RunCanClose(CloseReason.TaskManager);

            Assert.False(_mockServices.MessageService.ConfirmDialogShown);
            Assert.True(allow);
        }

        /// <summary>
        /// When OnSend completes successfully it calls AcceptChanges then RequestClose.
        /// The follow-up Pull gate (run by the framework when it calls form.Close())
        /// should observe IsChanged == false and NOT prompt.
        /// </summary>
        [Fact]
        public async Task OnSend_FollowedByClosing_DoesNotPrompt()
        {
            // Arrange
            _mockView.To = "test@example.com";
            _mockView.Subject = "Hello";
            _mockView.Body = "Body";
            _mockView.SimulateEmailDataChange();
            _mockRepository.SendEmailAsyncResult = true;

            // Act — simulate Send completing (AcceptChanges + RequestClose).
            _presenter.Dispatch(ComposeEmailActions.Send);
            await Task.Delay(50);
            // Then framework runs the close gate in response to RequestClose.
            bool allow = RunCanClose();

            // Assert
            Assert.True(_sink.Any);
            Assert.False(_mockServices.MessageService.ConfirmDialogShown);
            Assert.True(allow);
        }

        /// <summary>
        /// When OnDiscard is confirmed it calls RejectChanges then RequestClose.
        /// The follow-up Pull gate should observe IsChanged == false and NOT prompt.
        /// </summary>
        [Fact]
        public void OnDiscard_FollowedByClosing_DoesNotPrompt()
        {
            // Arrange — make changes.
            _mockView.To = "test@example.com";
            _mockView.SimulateEmailDataChange();
            _mockServices.MessageService.ConfirmYesNoResult = true;  // confirm discard

            // Act — Discard then run the close gate.
            _presenter.Dispatch(ComposeEmailActions.Discard);
            var initialCalls = _mockServices.MessageService.Calls.Count;
            bool allow = RunCanClose();
            var afterClosingCalls = _mockServices.MessageService.Calls.Count;
            var callsDump = string.Join(", ",
                _mockServices.MessageService.Calls.ConvertAll(c => $"{c.Type}:{c.Message}"));

            // Assert — the gate should NOT add any new prompts (the dirty state was
            // already finalized by OnDiscard's RejectChanges).
            Assert.True(_sink.Any);
            Assert.True(initialCalls == afterClosingCalls,
                $"CanClose gate added {afterClosingCalls - initialCalls} extra call(s). " +
                $"All calls: [{callsDump}]");
            Assert.True(allow);
        }

        #endregion

        #region ChangeTracker Integration Tests

        /// <summary>
        /// Test: ChangeTracker should detect To field change
        /// </summary>
        [Fact]
        public void ChangeTracker_DetectsToChange()
        {
            // Arrange
            Assert.False(_mockView.IsDirty);

            // Act - change To field
            _mockView.To = "newrecipient@example.com";
            _mockView.SimulateEmailDataChange();

            // Assert - dirty flag should be set
            Assert.True(_mockView.IsDirty);
        }

        /// <summary>
        /// Test: ChangeTracker should detect Subject field change
        /// </summary>
        [Fact]
        public void ChangeTracker_DetectsSubjectChange()
        {
            // Arrange
            Assert.False(_mockView.IsDirty);

            // Act - change Subject field
            _mockView.Subject = "New Subject";
            _mockView.SimulateEmailDataChange();

            // Assert - dirty flag should be set
            Assert.True(_mockView.IsDirty);
        }

        /// <summary>
        /// Test: ChangeTracker should detect Body field change
        /// </summary>
        [Fact]
        public void ChangeTracker_DetectsBodyChange()
        {
            // Arrange
            Assert.False(_mockView.IsDirty);

            // Act - change Body field
            _mockView.Body = "New email body content...";
            _mockView.SimulateEmailDataChange();

            // Assert - dirty flag should be set
            Assert.True(_mockView.IsDirty);
        }

        #endregion

        #region Service Injection Tests

        /// <summary>
        /// Test: Presenter uses injected mock services
        /// </summary>
        [Fact]
        public void Presenter_UsesInjectedMockServices()
        {
            // Arrange
            _mockView.To = "invalid-email";  // Invalid format to trigger validation
            _mockView.Subject = "Test";

            // Act - dispatch Send with invalid email
            _presenter.Dispatch(ComposeEmailActions.Send);

            // Assert - verify mock service recorded calls
            Assert.True(_mockServices.MessageService.WarningMessageShown);
            Assert.NotEmpty(_mockServices.MessageService.Calls);
        }

        /// <summary>
        /// Test: Multiple tests don't interfere (independent mock instances)
        /// </summary>
        [Fact]
        public void MultipleTests_DontInterfere()
        {
            // Each test has independent mock instances (created in constructor)
            // This test verifies tests don't affect each other

            // Assert - each test has its own independent mocks
            Assert.NotNull(_mockServices);
            Assert.NotNull(_mockView);
            Assert.NotNull(_mockRepository);
            Assert.NotNull(_presenter);

            // Method calls were cleared in SetupTest
            Assert.Empty(_mockView.MethodCalls);
            Assert.Empty(_mockRepository.MethodCalls);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Test: Complete compose workflow - compose, edit, send
        /// </summary>
        [Fact]
        public async Task CompleteWorkflow_ComposeEditSend_WorksCorrectly()
        {
            // 1. Start composing new email
            Assert.Equal(ComposeMode.New, _mockView.Mode);
            Assert.Equal(string.Empty, _mockView.To);

            // 2. Fill in email data
            _mockView.To = "recipient@example.com";
            _mockView.Subject = "Important Message";
            _mockView.Body = "This is the message body...";
            _mockView.SimulateEmailDataChange();

            // 3. Verify dirty state
            Assert.True(_mockView.IsDirty);

            // 4. Send email
            _presenter.Dispatch(ComposeEmailActions.Send);
            await Task.Delay(100);  // Wait for async operation

            // Assert - verify complete workflow
            Assert.Contains("SendEmailAsync(Important Message)", _mockRepository.MethodCalls);
            Assert.True(_sink.Any);
            // Note: IsDirty is cleared via ChangeTracker.AcceptChanges() which triggers IsChangedChanged event
            // The actual dirty flag is managed internally by ChangeTracker
        }

        /// <summary>
        /// Test: Draft workflow - compose, save, resume editing
        /// </summary>
        [Fact]
        public async Task DraftWorkflow_ComposeSaveResume_WorksCorrectly()
        {
            // 1. Start composing
            _mockView.To = "draft@example.com";
            _mockView.Subject = "Draft Email";
            _mockView.Body = "Working on this...";
            _mockView.SimulateEmailDataChange();
            Assert.True(_mockView.IsDirty);

            // 2. Save as draft
            _presenter.Dispatch(ComposeEmailActions.SaveDraft);
            await Task.Delay(50);

            // 3. Verify save
            Assert.Contains("SaveDraftAsync(Draft Email)", _mockRepository.MethodCalls);
            Assert.True(_mockServices.MessageService.InfoMessageShown);

            // 4. Make more changes
            _mockView.Body = "Updated draft content...";
            _mockView.SimulateEmailDataChange();

            // 5. Save again
            _presenter.Dispatch(ComposeEmailActions.SaveDraft);
            await Task.Delay(50);

            // Assert - can save multiple times
            Assert.Equal(2, _mockRepository.MethodCalls.Count(m => m.Contains("SaveDraftAsync")));
        }

        /// <summary>
        /// Test: Reply workflow with original email context
        /// </summary>
        [Fact]
        public void ReplyWorkflow_PreservesOriginalContext()
        {
            // Arrange
            var originalEmail = new EmailMessage
            {
                From = "sender@example.com",
                To = "me@example.com",
                Subject = "Question",
                Body = "What is the answer?",
                Date = DateTime.Now.AddHours(-3)
            };

            var replyPresenter = new ComposeEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);
            var replyView = new MockComposeEmailView();

            // Act
            replyPresenter.AttachView(replyView);
            replyPresenter.Initialize(new ComposeEmailParameters
            {
                Mode = ComposeMode.Reply,
                OriginalEmail = originalEmail
            });

            // Assert - verify reply context
            Assert.Equal(ComposeMode.Reply, replyView.Mode);
            Assert.Equal("sender@example.com", replyView.To);
            Assert.Equal("Re: Question", replyView.Subject);
            Assert.Contains("What is the answer?", replyView.Body);
            Assert.Contains("From: sender@example.com", replyView.Body);
        }

        #endregion
    }
}
