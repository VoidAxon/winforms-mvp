using System;
using System.Linq;
using WinformsMVP.Testing;
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
    /// 1. Injecting the package fakes (TestServices.CreateFakes) via the service provider
    /// 2. Mocking the view using MockToDoView
    /// 3. Verifying presenter business logic
    /// 4. Verifying service calls (MessageService, etc.)
    /// </summary>
    public class ToDoDemoPresenterTests
    {
        private FakeServices _fakes;
        private MockToDoView _mockView;
        private ToDoDemoPresenter _presenter;

        public ToDoDemoPresenterTests()
        {
            // Constructor runs before each test
            SetupTest();
        }

        private void SetupTest()
        {
            // 1. Create fake services
            _fakes = TestServices.CreateFakes();

            // 2. Create presenter (the fake services are given to it by PresenterTestHost.Start)
            _presenter = new ToDoDemoPresenter();

            // 3. Create mock view
            _mockView = new MockToDoView();

            // 4. Attach view and initialize
            PresenterTestHost.Start(_fakes.Provider, _presenter, _mockView);

            // Clear initialization call records (as needed)
            _fakes.Messages.ClearCalls();
            _fakes.Toasts.ClearCalls();
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
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

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
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Assert
            Assert.Equal(string.Empty, _mockView.TaskText);
        }

        [Fact]
        public void AddTask_WithValidText_ShowsSuccessMessage()
        {
            // Arrange
            _mockView.TaskText = "Buy groceries";

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify MessageService was called
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Info);
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Info && c.Text.Contains("Buy groceries"));
        }

        [Fact]
        public void AddTask_WithEmptyText_ShowsWarning()
        {
            // Arrange
            _mockView.TaskText = "";

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify warning message was displayed
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Warning);
            Assert.DoesNotContain("AddTaskToList", _mockView.MethodCalls);
        }

        [Fact]
        public void AddTask_WithWhitespaceText_ShowsWarning()
        {
            // Arrange
            _mockView.TaskText = "   ";

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Assert
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Warning);
            Assert.Equal(0, _mockView.TaskCount);
        }

        #endregion

        #region RemoveTask Tests

        [Fact]
        public void RemoveTask_WithConfirmYes_RemovesTask()
        {
            // Arrange
            _mockView.TaskText = "Task to delete";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);
            _fakes.Messages.YesNoHandler = _ => true;

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.RemoveTask);

            // Assert
            Assert.Contains(_fakes.Messages.Calls, c => c.IsConfirmation);
            Assert.Contains("RemoveSelectedTask()", _mockView.MethodCalls);
            Assert.Equal(0, _mockView.TaskCount);
        }

        [Fact]
        public void RemoveTask_WithConfirmNo_DoesNotRemoveTask()
        {
            // Arrange
            _mockView.TaskText = "Task to keep";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);
            _fakes.Messages.YesNoHandler = _ => false;  // User clicks No

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.RemoveTask);

            // Assert
            Assert.Contains(_fakes.Messages.Calls, c => c.IsConfirmation);
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
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);
            _mockView.SelectTask(0);

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.CompleteTask);

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
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);
            Assert.True(_mockView.HasPendingChanges);

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.SaveAll);

            // Assert
            Assert.Contains("ClearPendingChanges()", _mockView.MethodCalls);
            Assert.False(_mockView.HasPendingChanges);
        }

        [Fact]
        public void SaveAll_ShowsSuccessMessage()
        {
            // Arrange
            _mockView.TaskText = "New task";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Act
            _mockView.Binder.Dispatch(ToDoDemoActions.SaveAll);

            // Assert - Verify success message was displayed
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Info);
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Info && c.Text.Contains("saved"));
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void CompleteWorkflow_AddEditSave_WorksCorrectly()
        {
            // 1. Add first task
            _mockView.TaskText = "Task 1";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // 2. Add second task
            _mockView.TaskText = "Task 2";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // 3. Complete first task
            _mockView.SelectTask(0);
            _mockView.Binder.Dispatch(ToDoDemoActions.CompleteTask);

            // 4. Delete second task
            _mockView.SelectTask(1);
            _fakes.Messages.YesNoHandler = _ => true;
            _mockView.Binder.Dispatch(ToDoDemoActions.RemoveTask);

            // 5. Save all changes
            _mockView.Binder.Dispatch(ToDoDemoActions.SaveAll);

            // Assert - verify final state
            Assert.Equal(1, _mockView.TaskCount);
            Assert.StartsWith("[Done]", _mockView.GetTask(0));
            Assert.False(_mockView.HasPendingChanges);

            // Verify messages shown
            Assert.Contains(_fakes.Messages.Calls, c => c.Kind == MessageKind.Info);
            Assert.Contains(_fakes.Messages.Calls, c => c.IsConfirmation);
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
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Act - Trigger SelectionChanged event
            _mockView.SelectTask(0);

            // Assert - CanExecute should be re-evaluated
            // In real scenarios, RemoveTask and CompleteTask buttons would become enabled
            Assert.True(_mockView.HasSelectedTask);
        }

        #endregion

        #region Service Injection Tests

        [Fact]
        public void Presenter_UsesInjectedFakes()
        {
            // This test verifies that fake services are actually being used

            // Act
            _mockView.TaskText = "Test";
            _mockView.Binder.Dispatch(ToDoDemoActions.AddTask);

            // Assert - Verify fake service recorded the call
            Assert.Single(_fakes.Messages.Calls);
            Assert.Equal(MessageKind.Info, _fakes.Messages.Calls[0].Kind);
        }

        [Fact]
        public void MultipleTests_DontInterfere()
        {
            // Each test has independent mock instances (created via constructor)
            // This test verifies that tests don't interfere with each other

            // Assert - State is clean at the start of new test
            Assert.Empty(_fakes.Messages.Calls);
            Assert.Equal(0, _mockView.TaskCount);
        }

        #endregion
    }
}
