using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.MVPComparisonDemo.SupervisingController
{
    /// <summary>
    /// Supervising Controller interface for user editor.
    ///
    /// In Supervising Controller pattern:
    /// - View interface is SIMPLER than Passive View
    /// - View exposes the Model directly (not individual properties)
    /// - View handles simple data binding automatically
    /// - Presenter only handles complex logic and coordination
    /// - Less boilerplate code compared to Passive View
    /// </summary>
    public interface IUserEditorView : IWindowView
    {
        // ========================================
        // Model Property (View binds to this)
        // ========================================

        /// <summary>
        /// The user model that the view binds to.
        /// In Supervising Controller, we expose the model,
        /// not individual properties like in Passive View.
        /// </summary>
        UserModel Model { get; set; }

        // ========================================
        // Coordination Methods
        // ========================================

        /// <summary>
        /// Update the status message
        /// </summary>
        void UpdateStatus(string message);

        // Notice: No ShowValidationErrors/ClearValidationErrors needed!
        // The View binds directly to Model.ValidationErrors
        // When the model changes, the view updates automatically
    }
}
