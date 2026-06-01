using System;
using System.Collections.Generic;
using WinformsMVP.Core.Views;
using WinformsMVP.Samples.EmailDemo.Models;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// Main email view interface
    /// </summary>
    public interface IMainEmailView : IWindowView
    {
        /// <summary>
        /// Email list
        /// </summary>
        IEnumerable<EmailMessage> Emails { set; }

        /// <summary>
        /// Currently selected email
        /// </summary>
        EmailMessage SelectedEmail { get; }

        /// <summary>
        /// Whether an email is selected
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Current folder
        /// </summary>
        EmailFolder CurrentFolder { get; set; }

        /// <summary>
        /// Unread email count
        /// </summary>
        int UnreadCount { set; }

        /// <summary>
        /// Status bar message
        /// </summary>
        string StatusMessage { set; }

        /// <summary>
        /// Whether data is loading
        /// </summary>
        bool IsLoading { set; }

        /// <summary>
        /// Show email preview panel
        /// </summary>
        void ShowEmailPreview(EmailMessage email);

        /// <summary>
        /// Clear email preview
        /// </summary>
        void ClearEmailPreview();

        /// <summary>
        /// Refresh email list display
        /// </summary>
        void RefreshEmailList();

        /// <summary>
        /// Email selection changed event
        /// </summary>
        event EventHandler EmailSelectionChanged;

        /// <summary>
        /// Folder changed event
        /// </summary>
        event EventHandler<EmailFolder> FolderChanged;
    }
}
