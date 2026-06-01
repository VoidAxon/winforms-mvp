using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MVPComparisonDemo.PassiveView
{
    /// <summary>
    /// Passive View interface for user editor.
    ///
    /// In Passive View pattern:
    /// - View is completely passive - no logic, no data binding
    /// - Presenter controls ALL aspects of the view
    /// - View only exposes properties and methods, no UI controls
    /// - All data flows through Presenter
    /// </summary>
    public interface IUserEditorView : IWindowView
    {
        // ========================================
        // Data Properties (Presenter reads/writes these)
        // ========================================

        string UserName { get; set; }
        string Email { get; set; }
        int Age { get; set; }
        bool IsActive { get; set; }

        // ========================================
        // Display Methods (Presenter calls these)
        // ========================================

        /// <summary>
        /// Display validation errors to the user
        /// </summary>
        void ShowValidationErrors(string errors);

        /// <summary>
        /// Clear validation error display
        /// </summary>
        void ClearValidationErrors();

        /// <summary>
        /// Update the status message
        /// </summary>
        void UpdateStatus(string message);
    }
}
