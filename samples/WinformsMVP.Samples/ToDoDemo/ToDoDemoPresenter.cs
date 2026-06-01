using System;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples.ToDoDemo
{
    /// <summary>
    /// ActionKeys for ToDo demo.
    /// Best practice: Use static classes. Use Factory for qualified actions, StandardActions for standard actions.
    /// </summary>
    public static class ToDoDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("ToDoDemo");

        // Business-specific actions - Use Factory to add "ToDoDemo" prefix
        public static readonly ViewAction AddTask = Factory.Create("AddTask");        // "ToDoDemo.AddTask"
        public static readonly ViewAction RemoveTask = Factory.Create("RemoveTask");  // "ToDoDemo.RemoveTask"
        public static readonly ViewAction CompleteTask = Factory.Create("CompleteTask"); // "ToDoDemo.CompleteTask"
        public static readonly ViewAction SaveAll = Factory.Create("Save"); // "ToDoDemo.Save" (use standard name but with prefix)
    }

    /// <summary>
    /// Presenter for ToDo demo.
    ///
    /// IMPORTANT - Proper MVP Design:
    /// - This presenter has ZERO knowledge of UI elements (no Button, TextBox, etc.)
    /// - Only interacts with view through the IToDoView interface (data and behavior)
    /// - Registers actions with dispatcher, but doesn't know what UI controls trigger them
    /// - View implementation (Form) is responsible for binding UI controls to actions
    ///
    /// Demonstrates:
    /// 1. Static ActionKey classes
    /// 2. Proper MVP separation (no UI types in Presenter)
    /// 3. CanExecute predicates that control action availability
    /// 4. AUTOMATIC UI updates after actions (action-driven)
    /// 5. MANUAL UI updates from view events (state-driven via RaiseCanExecuteChanged)
    /// 6. Simplified service injection with CommonServices.Default
    /// </summary>
    public class ToDoDemoPresenter : WindowPresenterBase<IToDoView>
    {
        // No constructor needed - Platform services are automatically available

        protected override void OnViewAttached()
        {
            // Nothing to do here - we don't access any UI elements
            // The view will bind its UI controls in BindActions()
        }

        protected override void RegisterViewActions()
        {
            // Register action handlers with CanExecute predicates
            // The dispatcher will manage which actions can execute
            // The view will decide which UI controls trigger which actions

            // AddTask: Always enabled (no CanExecute = always true)
            _dispatcher.Register(ToDoDemoActions.AddTask, OnAddTask);

            // RemoveTask: Only enabled when a task is selected
            _dispatcher.Register(
                ToDoDemoActions.RemoveTask,
                OnRemoveTask,
                canExecute: () => View.HasSelectedTask);

            // CompleteTask: Only enabled when a task is selected
            _dispatcher.Register(
                ToDoDemoActions.CompleteTask,
                OnCompleteTask,
                canExecute: () => View.HasSelectedTask);

            // SaveAll: Only enabled when there are unsaved changes
            _dispatcher.Register(
                ToDoDemoActions.SaveAll,
                OnSaveAll,
                canExecute: () => View.HasPendingChanges);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Subscribe to view events for STATE-DRIVEN updates
            SubscribeToViewEvents();

            View.UpdateStatus("Ready! Try adding tasks and watch buttons enable/disable automatically.");
        }

        private void SubscribeToViewEvents()
        {
            // STATE-DRIVEN UPDATE PATTERN:
            // When state changes from view events (not from our actions),
            // we call RaiseCanExecuteChanged() to refresh action availability.
            // This is similar to WPF's ICommand.RaiseCanExecuteChanged().

            View.SelectionChanged += (sender, e) =>
            {
                // User selected/deselected a task
                // Notify dispatcher to re-evaluate CanExecute for all actions
                _dispatcher.RaiseCanExecuteChanged();

                // Update status
                if (View.HasSelectedTask)
                {
                    View.UpdateStatus("Task selected. Delete and Complete actions are now available.");
                }
                else
                {
                    View.UpdateStatus("No task selected. Delete and Complete actions are disabled.");
                }
            };

            View.DataChanged += (sender, e) =>
            {
                // Data changed externally (e.g., checkbox toggled)
                _dispatcher.RaiseCanExecuteChanged();
            };
        }

        #region Action Handlers - ACTION-DRIVEN Updates

        // These handlers demonstrate ACTION-DRIVEN automatic updates.
        // After each action completes, UI state automatically refreshes
        // because the dispatcher triggers ActionExecuted event.
        // NO manual UpdateCanExecuteStates() or RaiseCanExecuteChanged() needed!

        private void OnAddTask()
        {
            var taskText = View.TaskText;

            if (string.IsNullOrWhiteSpace(taskText))
            {
                Messages.ShowWarning("Please enter a task description.", "Empty Task");
                return;
            }

            // Add task to view
            View.AddTaskToList(taskText);
            View.TaskText = string.Empty;

            // Show confirmation
            Messages.ShowInfo($"Task added: {taskText}", "Success");

            // UI state automatically updates here!
            // - Save action will become available (HasPendingChanges = true)
            // No manual code needed - ActionExecuted event handles it
        }

        private void OnRemoveTask()
        {
            // Confirm deletion
            if (!Messages.ConfirmYesNo("Are you sure you want to delete this task?", "Confirm Delete"))
            {
                return;
            }

            // Delete task
            View.RemoveSelectedTask();

            // UI state automatically updates here!
            // - Delete/Complete actions may become unavailable (if no selection after delete)
            // - Save action will become available (HasPendingChanges = true)
            // No manual code needed - ActionExecuted event handles it
        }

        private void OnCompleteTask()
        {
            // Mark task as completed
            View.CompleteSelectedTask();

            // UI state automatically updates here!
            // - Save action will become available (HasPendingChanges = true)
            // No manual code needed - ActionExecuted event handles it
        }

        private void OnSaveAll()
        {
            // Simulate saving
            View.ClearPendingChanges();

            Messages.ShowInfo("All changes have been saved!", "Save Successful");

            // UI state automatically updates here!
            // - Save action will become unavailable (HasPendingChanges = false)
            // No manual code needed - ActionExecuted event handles it
        }

        #endregion
    }
}
