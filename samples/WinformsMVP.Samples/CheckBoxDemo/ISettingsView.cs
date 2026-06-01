using System;
using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.CheckBoxDemo
{
    /// <summary>
    /// View interface for settings demo.
    /// Demonstrates CheckBox/RadioButton with ViewAction system.
    ///
    /// IMPORTANT MVP PRINCIPLES:
    /// - No CheckBox or RadioButton types exposed
    /// - Only data properties (bool for checked state)
    /// - Behavior methods for UI updates
    /// </summary>
    public interface ISettingsView : IWindowView
    {
        // ========================================
        // Data Properties (no UI types!)
        // ========================================

        /// <summary>
        /// Whether auto-save is enabled
        /// </summary>
        bool IsAutoSaveEnabled { get; set; }

        /// <summary>
        /// Whether notifications are enabled
        /// </summary>
        bool IsNotificationsEnabled { get; set; }

        /// <summary>
        /// Selected theme: "Light", "Dark", or "Auto"
        /// </summary>
        string SelectedTheme { get; set; }

        /// <summary>
        /// Whether any settings have been applied (for CanExecute demo)
        /// </summary>
        bool HasSettings { get; }

        // ========================================
        // View Behaviors
        // ========================================

        /// <summary>
        /// Update the status message
        /// </summary>
        void UpdateStatus(string message);

        /// <summary>
        /// Show the current settings summary
        /// </summary>
        void ShowSettingsSummary(string summary);

        // ========================================
        // Events (for state-driven updates)
        // ========================================

        /// <summary>
        /// Raised when any setting changes
        /// </summary>
        event EventHandler SettingsChanged;
    }
}
