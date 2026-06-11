using System;
using System.Linq;
using Xunit;
using WinformsMVP.Samples.EmailDemo;
using WinformsMVP.Samples.EmailDemo.Models;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Unit tests for MainEmailPresenter.
    ///
    /// Demonstrates:
    /// 1. Using MockServices to inject mock services via the service provider
    /// 2. Using MockMainEmailView to mock the view
    /// 3. Using MockEmailRepository to provide test data
    /// 4. Testing business logic and service interactions
    /// 5. Testing state-driven UI updates via RaiseCanExecuteChanged
    /// </summary>
    public class MainEmailPresenterTests
    {
        private MockServices _mockServices;
        private MockMainEmailView _mockView;
        private MockEmailRepository _mockRepository;
        private MainEmailPresenter _presenter;

        public MainEmailPresenterTests()
        {
            // Constructor runs before each test
            SetupTest();
        }

        private void SetupTest()
        {
            // 1. Create mock services
            _mockServices = new MockServices();

            // 2. Create mock repository with test data
            _mockRepository = new MockEmailRepository();
            AddTestEmails();

            // 3. Create presenter and inject mock services BEFORE AttachView
            _presenter = new MainEmailPresenter(_mockRepository)
                .WithServiceProvider(_mockServices.Provider);

            // 4. Create mock view
            _mockView = new MockMainEmailView();

            // 5. Attach view and initialize
            _presenter.AttachView(_mockView);
            _presenter.Initialize();

            // NOTE: Do NOT clear method calls here - initialization tests need them!
            // Individual tests should clear if needed before their Act phase
        }

        private void AddTestEmails()
        {
            // Add sample emails to repository for testing
            _mockRepository.AddEmail(new EmailMessage
            {
                Id = 1,
                From = "alice@example.com",
                To = "me@example.com",
                Subject = "Project Update",
                Body = "Here's the latest status on the project...",
                Date = DateTime.Now.AddHours(-2),
                IsRead = false,
                IsStarred = false,
                Folder = EmailFolder.Inbox
            });

            _mockRepository.AddEmail(new EmailMessage
            {
                Id = 2,
                From = "bob@example.com",
                To = "me@example.com",
                Subject = "Meeting Tomorrow",
                Body = "Don't forget about our meeting at 10am...",
                Date = DateTime.Now.AddHours(-1),
                IsRead = true,
                IsStarred = true,
                Folder = EmailFolder.Inbox
            });

            _mockRepository.AddEmail(new EmailMessage
            {
                Id = 3,
                From = "me@example.com",
                To = "charlie@example.com",
                Subject = "Re: Question",
                Body = "Here's the answer to your question...",
                Date = DateTime.Now.AddDays(-1),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Sent
            });

            _mockRepository.AddEmail(new EmailMessage
            {
                Id = 4,
                From = "me@example.com",
                To = "",
                Subject = "Draft: Ideas",
                Body = "Some ideas I'm working on...",
                Date = DateTime.Now.AddDays(-2),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Drafts
            });

            _mockRepository.AddEmail(new EmailMessage
            {
                Id = 5,
                From = "spam@example.com",
                To = "me@example.com",
                Subject = "Deleted Email",
                Body = "This is in trash...",
                Date = DateTime.Now.AddDays(-3),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Trash
            });
        }

        #region Initialization Tests

        /// <summary>
        /// Test: Initialize should load inbox emails
        /// </summary>
        [Fact]
        public void Initialize_LoadsInboxEmails()
        {
            // Assert - verify inbox emails were loaded (2 emails in inbox from AddTestEmails)
            Assert.Equal(2, _mockView.EmailCount);
            Assert.Equal("Project Update", _mockView.GetEmail(0).Subject);
            Assert.Equal("Meeting Tomorrow", _mockView.GetEmail(1).Subject);
        }

        /// <summary>
        /// Test: Initialize should set current folder to Inbox
        /// </summary>
        [Fact]
        public void Initialize_SetsCurrentFolderToInbox()
        {
            // Assert
            Assert.Contains("CurrentFolder = Inbox", _mockView.MethodCalls);
        }

        /// <summary>
        /// Test: Initialize should display unread count
        /// </summary>
        [Fact]
        public void Initialize_DisplaysUnreadCount()
        {
            // Assert - only 1 unread email in inbox (email #1)
            Assert.Equal(1, _mockView.GetUnreadCount());
            Assert.Contains("UnreadCount = 1", _mockView.MethodCalls);
        }

        #endregion

        #region Email Selection Tests

        /// <summary>
        /// Test: Selecting an email should show preview
        /// </summary>
        [Fact]
        public void OnEmailSelectionChanged_WithEmail_ShowsPreview()
        {
            // Arrange
            var email = _mockView.GetEmail(0);

            // Act
            _mockView.SimulateEmailSelection(email);

            // Assert
            Assert.Contains("ShowEmailPreview(Project Update)", _mockView.MethodCalls);
        }

        /// <summary>
        /// Test: Selecting an unread email should mark it as read
        /// </summary>
        [Fact]
        public void OnEmailSelectionChanged_WithUnreadEmail_MarksAsRead()
        {
            // Arrange
            var unreadEmail = _mockView.GetEmail(0);
            Assert.False(unreadEmail.IsRead);

            // Act
            _mockView.SimulateEmailSelection(unreadEmail);

            // Assert
            Assert.Contains($"MarkAsReadAsync({unreadEmail.Id}, True)", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// Test: Selecting null should clear preview
        /// </summary>
        [Fact]
        public void OnEmailSelectionChanged_WithNullEmail_ClearsPreview()
        {
            // Act
            _mockView.SimulateEmailSelection(null);

            // Assert
            Assert.Contains("ClearEmailPreview()", _mockView.MethodCalls);
        }

        /// <summary>
        /// Test: Selection change should trigger CanExecute update
        /// </summary>
        [Fact]
        public void OnEmailSelectionChanged_RaisesCanExecuteChanged()
        {
            // Arrange - verify Delete action requires selection
            var email = _mockView.GetEmail(0);

            // Act - simulate selection
            _mockView.SimulateEmailSelection(email);

            // Assert - verify view's HasSelection is now true
            // (In real scenario, Delete button would become enabled)
            Assert.True(_mockView.HasSelection);
        }

        #endregion

        #region Compose Action Tests

        /// <summary>
        /// Test: Compose should open compose window
        /// </summary>
        [Fact]
        public void OnCompose_OpensComposeWindow()
        {
            // Arrange
            _mockServices.WindowNavigator.ShowModalBoolResult = true;

            // Act
            _presenter.Dispatch(EmailActions.Compose);

            // Assert
            Assert.Single(_mockServices.WindowNavigator.ShowModalCalls);
        }

        /// <summary>
        /// Test: Compose should pass New mode parameters
        /// </summary>
        [Fact]
        public void OnCompose_PassesNewModeParameters()
        {
            // Arrange
            _mockServices.WindowNavigator.ShowModalBoolResult = true;

            // Act
            _presenter.Dispatch(EmailActions.Compose);

            // Assert
            var parameters = _mockServices.WindowNavigator.LastParameters as ComposeEmailParameters;
            Assert.NotNull(parameters);
            Assert.Equal(ComposeMode.New, parameters.Mode);
        }

        #endregion

        #region Reply Action Tests

        /// <summary>
        /// Test: Reply with selection should open compose window
        /// </summary>
        [Fact]
        public void OnReply_WithSelection_OpensComposeWindow()
        {
            // Arrange
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.WindowNavigator.ShowModalBoolResult = true;
            _mockView.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Reply);

            // Assert
            Assert.Single(_mockServices.WindowNavigator.ShowModalCalls);
        }

        /// <summary>
        /// Test: Reply should pass Reply mode and original email
        /// </summary>
        [Fact]
        public void OnReply_PassesReplyModeAndOriginalEmail()
        {
            // Arrange
            var originalEmail = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(originalEmail);
            _mockServices.WindowNavigator.ShowModalBoolResult = true;

            // Act
            _presenter.Dispatch(EmailActions.Reply);

            // Assert
            var parameters = _mockServices.WindowNavigator.LastParameters as ComposeEmailParameters;
            Assert.NotNull(parameters);
            Assert.Equal(ComposeMode.Reply, parameters.Mode);
            Assert.Equal(originalEmail, parameters.OriginalEmail);
        }

        #endregion

        #region Forward Action Tests

        /// <summary>
        /// Test: Forward with selection should open compose window
        /// </summary>
        [Fact]
        public void OnForward_WithSelection_OpensComposeWindow()
        {
            // Arrange
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.WindowNavigator.ShowModalBoolResult = true;
            _mockView.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Forward);

            // Assert
            Assert.Single(_mockServices.WindowNavigator.ShowModalCalls);
        }

        /// <summary>
        /// Test: Forward should pass Forward mode and original email
        /// </summary>
        [Fact]
        public void OnForward_PassesForwardModeAndOriginalEmail()
        {
            // Arrange
            var originalEmail = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(originalEmail);
            _mockServices.WindowNavigator.ShowModalBoolResult = true;

            // Act
            _presenter.Dispatch(EmailActions.Forward);

            // Assert
            var parameters = _mockServices.WindowNavigator.LastParameters as ComposeEmailParameters;
            Assert.NotNull(parameters);
            Assert.Equal(ComposeMode.Forward, parameters.Mode);
            Assert.Equal(originalEmail, parameters.OriginalEmail);
        }

        #endregion

        #region Delete Action Tests

        /// <summary>
        /// Test: Delete with user confirmation should delete email
        /// </summary>
        [Fact]
        public void OnDelete_UserConfirmsYes_DeletesEmail()
        {
            // Arrange
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _mockView.MethodCalls.Clear();
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Delete);

            // Assert
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.Contains($"DeleteEmailAsync({email.Id})", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// Test: Delete with user cancel should not delete
        /// </summary>
        [Fact]
        public void OnDelete_UserCancels_DoesNotDelete()
        {
            // Arrange
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.MessageService.ConfirmYesNoResult = false;  // User clicks No
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - user cancelled, no delete should happen
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.Empty(_mockRepository.MethodCalls);  // No repository calls should be made
        }

        /// <summary>
        /// Test: Delete in Inbox should move to Trash
        /// </summary>
        [Fact]
        public void OnDelete_InInbox_MovesToTrash()
        {
            // Arrange
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - should call DeleteEmailAsync (moves to trash), NOT PermanentlyDeleteEmailAsync
            Assert.Contains($"DeleteEmailAsync({email.Id})", _mockRepository.MethodCalls);
            Assert.DoesNotContain($"PermanentlyDeleteEmailAsync({email.Id})", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// Test: Delete in Trash should permanently delete
        /// </summary>
        [Fact]
        public void OnDelete_InTrash_PermanentlyDeletes()
        {
            // Arrange - switch to Trash folder
            _mockView.SimulateFolderChange(EmailFolder.Trash);
            _mockView.MethodCalls.Clear();
            _mockRepository.MethodCalls.Clear();

            // Select trash email
            var trashEmail = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(trashEmail);
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - should call PermanentlyDeleteEmailAsync, NOT DeleteEmailAsync
            Assert.Contains($"PermanentlyDeleteEmailAsync({trashEmail.Id})", _mockRepository.MethodCalls);
            Assert.DoesNotContain($"DeleteEmailAsync({trashEmail.Id})", _mockRepository.MethodCalls);
        }

        #endregion

        #region Mark As Read/Unread Tests

        /// <summary>
        /// Test: Mark as read should update repository
        /// NOTE: OnMarkAsRead action has CanExecute that requires !IsRead
        /// Selecting an email automatically marks it as read, so we can't test this action that way.
        /// This test verifies the automatic mark-as-read behavior instead.
        /// </summary>
        [Fact]
        public void OnMarkAsRead_UpdatesRepository()
        {
            // Arrange
            var unreadEmail = _mockView.GetEmail(0);  // Email #1 is unread
            Assert.False(unreadEmail.IsRead);
            _mockRepository.MethodCalls.Clear();

            // Act - selecting an unread email automatically marks it as read
            _mockView.SimulateEmailSelection(unreadEmail);

            // Assert - automatic mark-as-read was triggered
            Assert.Contains($"MarkAsReadAsync({unreadEmail.Id}, True)", _mockRepository.MethodCalls);
        }

        /// <summary>
        /// Test: Mark as unread should update repository
        /// </summary>
        [Fact]
        public void OnMarkAsUnread_UpdatesRepository()
        {
            // Arrange
            var readEmail = _mockView.GetEmail(1);  // Email #2 is read
            _mockView.SimulateEmailSelection(readEmail);
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.MarkAsUnread);

            // Assert
            Assert.Contains($"MarkAsReadAsync({readEmail.Id}, False)", _mockRepository.MethodCalls);
        }

        #endregion

        #region Folder Change Tests

        /// <summary>
        /// Test: Changing to Sent folder should load sent emails
        /// </summary>
        [Fact]
        public void OnFolderChanged_ToSent_LoadsSentEmails()
        {
            // Arrange
            _mockView.MethodCalls.Clear();
            _mockRepository.MethodCalls.Clear();

            // Act
            _mockView.SimulateFolderChange(EmailFolder.Sent);

            // Assert - should load sent folder
            Assert.Contains("GetEmailsByFolderAsync(Sent)", _mockRepository.MethodCalls);
            Assert.Equal(1, _mockView.EmailCount);  // Only 1 sent email
            Assert.Equal("Re: Question", _mockView.GetEmail(0).Subject);
        }

        /// <summary>
        /// Test: Folder change should refresh email list
        /// </summary>
        [Fact]
        public void OnFolderChanged_RefreshesEmailList()
        {
            // Arrange
            _mockView.MethodCalls.Clear();

            // Act
            _mockView.SimulateFolderChange(EmailFolder.Drafts);

            // Assert
            Assert.Contains("RefreshEmailList()", _mockView.MethodCalls);
            Assert.Contains("CurrentFolder = Drafts", _mockView.MethodCalls);
        }

        #endregion

        #region Refresh Action Tests

        /// <summary>
        /// Test: Refresh should reload current folder
        /// </summary>
        [Fact]
        public void OnRefresh_ReloadsCurrentFolder()
        {
            // Arrange
            _mockRepository.MethodCalls.Clear();

            // Act
            _presenter.Dispatch(EmailActions.Refresh);

            // Assert - should reload inbox (current folder)
            Assert.Contains("GetEmailsByFolderAsync(Inbox)", _mockRepository.MethodCalls);
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
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _mockServices.MessageService.ConfirmYesNoResult = true;

            // Act
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - verify mock service recorded calls
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
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
            // (Method calls from initialization exist, but they don't carry over from other tests)
            Assert.NotNull(_mockServices);
            Assert.NotNull(_mockView);
            Assert.NotNull(_mockRepository);
            Assert.NotNull(_presenter);
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Test: Complete workflow - compose, reply, delete
        /// </summary>
        [Fact]
        public void CompleteWorkflow_ComposeReplyDelete_WorksCorrectly()
        {
            // 1. Compose new email
            _mockServices.WindowNavigator.ShowModalBoolResult = true;
            _presenter.Dispatch(EmailActions.Compose);
            Assert.Single(_mockServices.WindowNavigator.ShowModalCalls);

            // 2. Select email and reply
            var email = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(email);
            _presenter.Dispatch(EmailActions.Reply);
            Assert.Equal(2, _mockServices.WindowNavigator.ShowModalCalls.Count);

            // 3. Delete email
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - verify all actions executed
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.Contains(email.Id, _mockRepository.DeleteCalls);
        }

        /// <summary>
        /// Test: Switching folders and managing emails
        /// </summary>
        [Fact]
        public void FolderWorkflow_SwitchAndManage_WorksCorrectly()
        {
            // 1. Start in Inbox (2 emails)
            Assert.Equal(2, _mockView.EmailCount);
            Assert.Equal(EmailFolder.Inbox, _mockView.CurrentFolder);

            // 2. Switch to Sent (1 email)
            _mockView.SimulateFolderChange(EmailFolder.Sent);
            Assert.Equal(1, _mockView.EmailCount);

            // 3. Switch to Drafts (1 email)
            _mockView.SimulateFolderChange(EmailFolder.Drafts);
            Assert.Equal(1, _mockView.EmailCount);

            // 4. Switch to Trash (1 email)
            _mockView.SimulateFolderChange(EmailFolder.Trash);
            Assert.Equal(1, _mockView.EmailCount);

            // 5. Delete from trash (permanent)
            var trashEmail = _mockView.GetEmail(0);
            _mockView.SimulateEmailSelection(trashEmail);
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _presenter.Dispatch(EmailActions.Delete);

            // Assert - permanently deleted
            Assert.Contains(trashEmail.Id, _mockRepository.PermanentlyDeleteCalls);
        }

        #endregion
    }
}
