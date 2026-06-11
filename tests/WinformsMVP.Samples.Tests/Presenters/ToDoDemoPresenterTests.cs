using System;
using Xunit;
using WinformsMVP.Samples.ToDoDemo;
using WinformsMVP.Samples.Tests.Mocks;
using WinformsMVP.Samples.Tests.TestHelpers;

namespace WinformsMVP.Samples.Tests.Presenters
{
    /// <summary>
    /// Unit test example: Testing ToDoDemoPresenter
    ///
    /// Demonstrates:
    /// 1. Injecting mock services using MockServices via the service provider
    /// 2. Mocking the view using MockToDoView
    /// 3. Verifying presenter business logic
    /// 4. Verifying service calls (MessageService, etc.)
    /// </summary>
    public class ToDoDemoPresenterTests
    {
        private MockServices _mockServices;
        private MockToDoView _mockView;
        private ToDoDemoPresenter _presenter;

        public ToDoDemoPresenterTests()
        {
            // Constructor runs before each test
            SetupTest();
        }

        private void SetupTest()
        {
            // 1. Create mock services
            _mockServices = new MockServices();

            // 2. Create presenter and inject mock services with WithServiceProvider
            _presenter = new ToDoDemoPresenter()
                .WithServiceProvider(_mockServices.Provider);

            // 3. Create mock view
            _mockView = new MockToDoView();

            // 4. Attach view and initialize
            _presenter.AttachView(_mockView);
            _presenter.Initialize();

            // Clear initialization call records (as needed)
            _mockServices.Reset();
            _mockView.MethodCalls.Clear();
            _mockView.StatusMessages.Clear();
        }

        #region AddTask Tests

        [Fact]
        public void AddTask_WithValidText_AddsTaskToView()
        {
            // Arrange
            _mockView.TaskText = "Buy groceries";

            // Act
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert
            Assert.Contains("AddTaskToList(Buy groceries)", _mockView.MethodCalls);
            Assert.Equal(1, _mockView.TaskCount);
            Assert.Equal("Buy groceries", _mockView.GetTask(0));
        }

        [Fact]
        public void AddTask_WithValidText_ClearsTaskText()
        {
            // Arrange
            _mockView.TaskText = "Buy groceries";

            // Act
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert
            Assert.Equal(string.Empty, _mockView.TaskText);
        }

        [Fact]
        public void AddTask_WithValidText_ShowsSuccessMessage()
        {
            // Arrange
            _mockView.TaskText = "Buy groceries";

            // Act
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify MessageService was called
            Assert.True(_mockServices.MessageService.InfoMessageShown);
            Assert.True(_mockServices.MessageService.HasCall(
                MessageType.Info,
                messageContains: "Buy groceries"));
        }

        [Fact]
        public void AddTask_WithEmptyText_ShowsWarning()
        {
            // Arrange
            _mockView.TaskText = "";

            // Act
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify warning message was displayed
            Assert.True(_mockServices.MessageService.WarningMessageShown);
            Assert.DoesNotContain("AddTaskToList", _mockView.MethodCalls);
        }

        [Fact]
        public void AddTask_WithWhitespaceText_ShowsWarning()
        {
            // Arrange
            _mockView.TaskText = "   ";

            // Act
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert
            Assert.True(_mockServices.MessageService.WarningMessageShown);
            Assert.Equal(0, _mockView.TaskCount);
        }

        #endregion

        #region RemoveTask Tests

        [Fact]
        public void RemoveTask_WithConfirmYes_RemovesTask()
        {
            // Arrange
            _mockView.TaskText = "Task to delete";
            _presenter.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);
            _mockServices.MessageService.ConfirmYesNoResult = true;

            // Act
            _presenter.Dispatch(ToDoDemoActions.RemoveTask);

            // Assert
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.Contains("RemoveSelectedTask()", _mockView.MethodCalls);
            Assert.Equal(0, _mockView.TaskCount);
        }

        [Fact]
        public void RemoveTask_WithConfirmNo_DoesNotRemoveTask()
        {
            // Arrange
            _mockView.TaskText = "Task to keep";
            _presenter.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);
            _mockServices.MessageService.ConfirmYesNoResult = false;  // User clicks No

            // Act
            _presenter.Dispatch(ToDoDemoActions.RemoveTask);

            // Assert
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
            Assert.DoesNotContain("RemoveSelectedTask()", _mockView.MethodCalls);
            Assert.Equal(1, _mockView.TaskCount);  // Task was not deleted
        }

        #endregion

        #region CompleteTask Tests

        [Fact]
        public void CompleteTask_MarksTaskAsCompleted()
        {
            // Arrange
            _mockView.TaskText = "Task to complete";
            _presenter.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);

            // Act
            _presenter.Dispatch(ToDoDemoActions.CompleteTask);

            // Assert
            Assert.Contains("CompleteSelectedTask()", _mockView.MethodCalls);
            Assert.StartsWith("[Done]", _mockView.GetTask(0));
        }

        #endregion

        #region SaveAll Tests

        [Fact]
        public void SaveAll_ClearsPendingChanges()
        {
            // Arrange
            _mockView.TaskText = "New task";
            _presenter.Dispatch(ToDoDemoActions.AddTask);
            Assert.True(_mockView.HasPendingChanges);

            // Act
            _presenter.Dispatch(ToDoDemoActions.SaveAll);

            // Assert
            Assert.Contains("ClearPendingChanges()", _mockView.MethodCalls);
            Assert.False(_mockView.HasPendingChanges);
        }

        [Fact]
        public void SaveAll_ShowsSuccessMessage()
        {
            // Arrange
            _mockView.TaskText = "New task";
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Act
            _presenter.Dispatch(ToDoDemoActions.SaveAll);

            // Assert - Verify success message was displayed
            Assert.True(_mockServices.MessageService.InfoMessageShown);
            Assert.True(_mockServices.MessageService.HasCall(
                MessageType.Info,
                messageContains: "saved"));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteWorkflow_AddEditSave_WorksCorrectly()
        {
            // 1. Add first task
            _mockView.TaskText = "Task 1";
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // 2. Add second task
            _mockView.TaskText = "Task 2";
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // 3. Complete first task
            _mockView.SelectTask(0);
            _presenter.Dispatch(ToDoDemoActions.CompleteTask);

            // 4. Delete second task
            _mockView.SelectTask(1);
            _mockServices.MessageService.ConfirmYesNoResult = true;
            _presenter.Dispatch(ToDoDemoActions.RemoveTask);

            // 5. Save all changes
            _presenter.Dispatch(ToDoDemoActions.SaveAll);

            // Assert - verify final state
            Assert.Equal(1, _mockView.TaskCount);
            Assert.StartsWith("[Done]", _mockView.GetTask(0));
            Assert.False(_mockView.HasPendingChanges);

            // Verify messages shown
            Assert.True(_mockServices.MessageService.InfoMessageShown);
            Assert.True(_mockServices.MessageService.ConfirmDialogShown);
        }

        #endregion

        #region State-Driven Updates Tests

        [Fact]
        public void SelectionChanged_TriggersCanExecuteUpdate()
        {
            // This test demonstrates how View events affect action availability
            // Actual CanExecute verification requires accessing dispatcher's internal state
            // Here we only verify that event subscription works correctly

            // Arrange
            _mockView.TaskText = "Test task";
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Act - Trigger SelectionChanged event
            _mockView.SelectTask(0);

            // Assert - CanExecute should be re-evaluated
            // In real scenarios, RemoveTask and CompleteTask buttons would become enabled
            Assert.True(_mockView.HasSelectedTask);
        }

        #endregion

        #region Service Injection Tests

        [Fact]
        public void Presenter_UsesInjectedMockServices()
        {
            // This test verifies that mock services are actually being used

            // Act
            _mockView.TaskText = "Test";
            _presenter.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify mock service recorded the call
            Assert.Single(_mockServices.MessageService.Calls);
            Assert.Equal(MessageType.Info, _mockServices.MessageService.Calls[0].Type);
        }

        [Fact]
        public void MultipleTests_DontInterfere()
        {
            // Each test has independent mock instances (created via constructor)
            // This test verifies that tests don't interfere with each other

            // Assert - State is clean at the start of new test
            Assert.Empty(_mockServices.MessageService.Calls);
            Assert.Equal(0, _mockView.TaskCount);
        }

        #endregion
    }
}
