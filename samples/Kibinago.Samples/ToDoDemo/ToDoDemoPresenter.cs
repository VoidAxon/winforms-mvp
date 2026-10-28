using System;
using Kibinago.Presenters;
using Kibinago.ViewActions;
using Kibinago.Services;

namespace Kibinago.Samples.ToDoDemo
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
    /// 6. A control presenter: the sample's main window attaches it to its tab (AttachChild)
    /// </summary>
    public class ToDoDemoPresenter : ControlPresenterBase<IToDoView>
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
            Dispatcher.Register(ToDoDemoActions.AddTask, OnAddTask);

            // RemoveTask: Only enabled when a task is selected
            Dispatcher.Register(
                ToDoDemoActions.RemoveTask,
                OnRemoveTask,
                canExecute: () => View.HasSelectedTask);

            // CompleteTask: Only enabled when a task is selected
            Dispatcher.Register(
                ToDoDemoActions.CompleteTask,
                OnCompleteTask,
                canExecute: () => View.HasSelectedTask);

            // SaveAll: Only enabled when there are unsaved changes
            Dispatcher.Register(
                ToDoDemoActions.SaveAll,
                OnSaveAll,
                canExecute: () => View.HasPendingChanges);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Subscribe to view events for STATE-DRIVEN updates
            SubscribeToViewEvents();

            View.UpdateStatus("タスクを追加して、ボタンの有効・無効が自動で切り替わるのを確認してください。");
        }

        private void SubscribeToViewEvents()
        {
            // STATE-DRIVEN UPDATE PATTERN:
            // When state changes from view events (not from our actions),
            // we call RaiseCanExecuteChanged() to refresh action availability.
            // This is similar to WPF's ICommand.RaiseCanExecuteChanged().

            // SubscribeViewEvent unsubscribes when the presenter is disposed (here: when the tab's
            // category is left), so no Cleanup override is needed.
            SubscribeViewEvent(h => View.SelectionChanged += h, h => View.SelectionChanged -= h, () =>
            {
                // User selected/deselected a task
                // Notify dispatcher to re-evaluate CanExecute for all actions
                Dispatcher.RaiseCanExecuteChanged();

                View.UpdateStatus(View.HasSelectedTask
                    ? "タスクを選択しました。削除と完了が使えます。"
                    : "タスクが選択されていません。削除と完了は使えません。");
            });

            // Data changed externally (e.g., checkbox toggled)
            SubscribeViewEvent(h => View.DataChanged += h, h => View.DataChanged -= h, Dispatcher.RaiseCanExecuteChanged);
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
                Messages.ShowWarning("タスクの内容を入力してください。", "タスクが空です");
                return;
            }

            // Add task to view
            View.AddTaskToList(taskText);
            View.TaskText = string.Empty;

            // Show confirmation
            Messages.ShowInfo($"タスクを追加しました: {taskText}", "追加");

            // UI state automatically updates here!
            // - Save action will become available (HasPendingChanges = true)
            // No manual code needed - ActionExecuted event handles it
        }

        private void OnRemoveTask()
        {
            // Confirm deletion
            if (!Messages.ConfirmYesNo("このタスクを削除しますか？", "削除の確認"))
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

            Messages.ShowInfo("すべての変更を保存しました。", "保存");

            // UI state automatically updates here!
            // - Save action will become unavailable (HasPendingChanges = false)
            // No manual code needed - ActionExecuted event handles it
        }

        #endregion
    }
}
