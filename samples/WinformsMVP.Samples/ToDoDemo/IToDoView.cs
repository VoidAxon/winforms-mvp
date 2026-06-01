using System;
using WinformsMVP.Core.Views;

namespace WinformsMVP.Samples.ToDoDemo
{
    /// <summary>
    /// View interface for the ToDo demo.
    /// Demonstrates proper MVP separation - no UI elements, only data and behavior.
    ///
    /// IMPORTANT:
    /// - This interface contains NO Button, TextBox or any UI control types
    /// - Only data properties, events, and behavior methods
    /// - Presenter has NO knowledge of how UI is implemented
    /// </summary>
    public interface IToDoView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        /// <summary>
        /// The text entered by user for a new task
        /// </summary>
        string TaskText { get; set; }

        // ========================================
        // State Properties (for CanExecute predicates)
        // ========================================

        /// <summary>
        /// Whether a task is currently selected
        /// </summary>
        bool HasSelectedTask { get; }

        /// <summary>
        /// Whether there are unsaved changes
        /// </summary>
        bool HasPendingChanges { get; }

        // ========================================
        // View Behaviors (actions the view can perform)
        // ========================================

        /// <summary>
        /// Add a task to the list
        /// </summary>
        void AddTaskToList(string task);

        /// <summary>
        /// Remove the currently selected task
        /// </summary>
        void RemoveSelectedTask();

        /// <summary>
        /// Mark the currently selected task as completed
        /// </summary>
        void CompleteSelectedTask();

        /// <summary>
        /// Clear the pending changes flag (after save)
        /// </summary>
        void ClearPendingChanges();

        /// <summary>
        /// Update the status message shown to user
        /// </summary>
        void UpdateStatus(string message);

        // ========================================
        // Events (for state-driven updates)
        // ========================================

        /// <summary>
        /// Raised when task selection changes
        /// </summary>
        event EventHandler SelectionChanged;

        /// <summary>
        /// Raised when data changes
        /// </summary>
        event EventHandler DataChanged;
    }
}
