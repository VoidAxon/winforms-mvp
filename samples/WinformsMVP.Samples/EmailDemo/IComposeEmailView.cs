using System;
using WinformsMVP.Core.Views;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// Compose email view interface
    /// Demonstrates Supervising Controller pattern with semantic events
    /// </summary>
    public interface IComposeEmailView : IWindowView
    {
        /// <summary>
        /// Email recipient
        /// </summary>
        string To { get; set; }

        /// <summary>
        /// Email subject
        /// </summary>
        string Subject { get; set; }

        /// <summary>
        /// Email body
        /// </summary>
        string Body { get; set; }

        /// <summary>
        /// Compose mode (New/Reply/Forward)
        /// </summary>
        ComposeMode Mode { get; set; }

        /// <summary>
        /// Whether the email is being saved
        /// </summary>
        bool IsSaving { set; }

        /// <summary>
        /// Whether the email has been modified (for close confirmation)
        /// </summary>
        bool IsDirty { get; set; }

        /// <summary>
        /// Enable/disable input controls
        /// </summary>
        bool EnableInput { set; }

        /// <summary>
        /// Raised when any email data (To, Subject, Body) changes.
        /// This follows the semantic event pattern used throughout the framework
        /// (see ToDoDemo.DataChanged, MainEmailView.EmailSelectionChanged).
        /// Presenter subscribes to this event to update ChangeTracker and trigger CanExecute updates.
        /// </summary>
        event EventHandler EmailDataChanged;
    }
}
