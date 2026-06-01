using System;
using System.Windows.Forms;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Services;

namespace WinformsMVP.Samples
{
    /// <summary>
    /// Global ActionKeys used across the entire application.
    /// These represent common actions that might be used in multiple modules.
    /// </summary>
    public static class CommonActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("Common");

        public static readonly ViewAction Save = Factory.Create("Save");
        public static readonly ViewAction Cancel = Factory.Create("Cancel");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction Refresh = Factory.Create("Refresh");
        public static readonly ViewAction Help = Factory.Create("Help");
    }

    /// <summary>
    /// Module-specific ActionKeys for user editing functionality.
    /// Grouped by feature/module to improve organization.
    /// </summary>
    public static class UserEditorActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("UserEditor");

        public static readonly ViewAction EditUser = Factory.Create("Edit");
        public static readonly ViewAction AddUser = Factory.Create("Add");
        public static readonly ViewAction RemoveUser = Factory.Create("Remove");
        public static readonly ViewAction ChangePassword = Factory.Create("ChangePassword");
        public static readonly ViewAction ResetPermissions = Factory.Create("ResetPermissions");
    }

    #region View Interface

    /// <summary>
    /// View interface for user editor.
    /// Demonstrates proper MVP separation - no UI elements exposed.
    ///
    /// IMPORTANT MVP PRINCIPLES:
    /// - No Button, ToolStripMenuItem, or any UI control types
    /// - Only data properties (string, bool, etc.)
    /// - Behavior methods for UI updates
    /// - BindActions method for ViewAction integration (framework-level abstraction)
    /// </summary>
    public interface IUserEditorView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        string UserName { get; set; }
        bool HasUnsavedChanges { get; }
        bool HasSelectedUser { get; }

        // ========================================
        // ViewAction Integration
        // ========================================

        /// <summary>
        /// Bind UI controls to the ViewActionDispatcher.
        /// Form implementation will map buttons/menu items to actions internally.
        /// Presenter has ZERO knowledge of Button or ToolStripMenuItem.
        ///
        /// CRITICAL: This method must contain ONLY UI binding code.
        /// DO NOT include:
        /// - Database operations (use Presenter.OnInitialize instead)
        /// - Business logic (belongs in Presenter)
        /// - Expensive computations
        /// - Network calls
        ///
        /// Think of this like WPF's InitializeComponent() - pure UI infrastructure only.
        /// </summary>
    }

    #endregion

    /// <summary>
    /// Example presenter demonstrating ViewAction best practices:
    /// 1. Using static ActionKey classes instead of raw strings
    /// 2. Implementing CanExecute predicates for dynamic enable/disable
    /// 3. Using BindActions() pattern for proper MVP separation
    /// 4. Automatic UI state updates after action execution
    /// 5. Using IMessageService instead of MessageBox (follows MVP principle)
    /// 6. ZERO knowledge of UI controls (Button, ToolStripMenuItem, etc.)
    /// </summary>
    public class UserEditorPresenter : WindowPresenterBase<IUserEditorView>
    {
        private readonly IMessageService _messageService;

        public UserEditorPresenter(IMessageService messageService)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        }

        protected override void OnViewAttached()
        {
            // Nothing to do here - View will handle UI binding internally
            // Presenter has NO access to Button, ToolStripMenuItem, or any UI elements
        }

        protected override void RegisterViewActions()
        {
            // Register action handlers with CanExecute predicates
            // The View's BindActions implementation will automatically enable/disable
            // controls based on these predicates

            // Save is only enabled when there are unsaved changes
            _dispatcher.Register(
                CommonActions.Save,
                OnSave,
                canExecute: () => View.HasUnsavedChanges);

            // Cancel is always enabled
            _dispatcher.Register(
                CommonActions.Cancel,
                OnCancel);

            // Delete is only enabled when a user is selected
            _dispatcher.Register(
                CommonActions.Delete,
                OnDelete,
                canExecute: () => View.HasSelectedUser);

            // Add user is always enabled
            _dispatcher.Register(
                UserEditorActions.AddUser,
                OnAddUser);

            // Edit user is only enabled when a user is selected
            _dispatcher.Register(
                UserEditorActions.EditUser,
                OnEditUser,
                canExecute: () => View.HasSelectedUser);

            // Change password is only enabled when a user is selected
            _dispatcher.Register(
                UserEditorActions.ChangePassword,
                OnChangePassword,
                canExecute: () => View.HasSelectedUser);

            // Note: View.ActionBinder.Bind(_dispatcher) is now called automatically by the base class
        }

        protected override void OnInitialize()
        {
            // Subscribe to view events if needed
            SubscribeToStateChanges();
        }

        private void SubscribeToStateChanges()
        {
            // TWO PATTERNS FOR UI STATE UPDATES:
            //
            // 1. ACTION-DRIVEN (automatic):
            //    When actions execute, UI automatically updates via ActionExecuted event.
            //    No code needed - this example uses this pattern.
            //
            // 2. STATE-DRIVEN (manual trigger):
            //    When state changes from view events, call RaiseCanExecuteChanged():
            //
            // View.SelectionChanged += (s, e) =>
            // {
            //     // State changed outside of action - notify dispatcher
            //     _dispatcher.RaiseCanExecuteChanged();
            // };
            //
            // See ViewActionStateChangedExample.cs for a complete state-driven example.
        }

        #region Action Handlers

        private void OnSave()
        {
            // Save logic here
            _messageService.ShowInfo($"Saving user: {View.UserName}");

            // Note: UI state automatically updates after action execution
        }

        private void OnCancel()
        {
            // Cancel logic here
            _messageService.ShowInfo("Cancelled");
        }

        private void OnDelete()
        {
            // Best practice: Use IMessageService for confirmations (MVP principle)
            if (_messageService.ConfirmYesNo("Are you sure?", "Confirm Delete"))
            {
                // Delete logic here
                _messageService.ShowInfo("User deleted");

                // Note: UI state automatically updates after action execution
            }
        }

        private void OnAddUser()
        {
            // Add user logic here
            _messageService.ShowInfo("Adding new user");

            // Note: UI state automatically updates after action execution
        }

        private void OnEditUser()
        {
            // Edit user logic here
            _messageService.ShowInfo("Editing selected user");
        }

        private void OnChangePassword()
        {
            // Change password logic here
            _messageService.ShowInfo("Changing password for selected user");
        }

        #endregion
    }
}
