using System;
using System.Collections.Generic;
using System.Linq;
using WinformsMVP.MVP.ViewActions;
using WinformsMVP.Samples.EmailDemo;
using WinformsMVP.Samples.EmailDemo.Models;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IMainEmailView for testing.
    /// Records all method calls and simulates view behavior.
    /// </summary>
    public class MockMainEmailView : IMainEmailView
    {
        // Internal state
        private List<EmailMessage> _emails = new List<EmailMessage>();
        private EmailMessage _selectedEmail = null;
        private EmailFolder _currentFolder = EmailFolder.Inbox;
        private int _unreadCount = 0;
        private string _statusMessage = string.Empty;
        private bool _isLoading = false;

        // Record method calls
        public List<string> MethodCalls { get; } = new List<string>();

        // IWindowView members
        public bool IsDisposed { get; private set; } = false;
        public IntPtr Handle => IntPtr.Zero;

        public void Activate()
        {
            MethodCalls.Add("Activate()");
        }

        // IMainEmailView implementation
        public IEnumerable<EmailMessage> Emails
        {
            set
            {
                MethodCalls.Add($"Emails = [{value?.Count() ?? 0} items]");
                _emails = value?.ToList() ?? new List<EmailMessage>();
            }
        }

        public EmailMessage SelectedEmail
        {
            get => _selectedEmail;
        }

        public bool HasSelection => _selectedEmail != null;

        public EmailFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                MethodCalls.Add($"CurrentFolder = {value}");
                _currentFolder = value;
            }
        }

        public int UnreadCount
        {
            set
            {
                MethodCalls.Add($"UnreadCount = {value}");
                _unreadCount = value;
            }
        }

        public string StatusMessage
        {
            set
            {
                MethodCalls.Add($"StatusMessage = {value}");
                _statusMessage = value;
            }
        }

        public bool IsLoading
        {
            set
            {
                MethodCalls.Add($"IsLoading = {value}");
                _isLoading = value;
            }
        }

        public void ShowEmailPreview(EmailMessage email)
        {
            MethodCalls.Add($"ShowEmailPreview({email?.Subject ?? "null"})");
        }

        public void ClearEmailPreview()
        {
            MethodCalls.Add("ClearEmailPreview()");
        }

        public void RefreshEmailList()
        {
            MethodCalls.Add("RefreshEmailList()");
        }

        public IViewActionBinder ActionBinder { get; } = NullViewActionBinder.Instance;

        // Events
        public event EventHandler EmailSelectionChanged;
        public event EventHandler<EmailFolder> FolderChanged;

        // Test helper methods
        /// <summary>
        /// Simulate user selecting an email
        /// </summary>
        public void SimulateEmailSelection(EmailMessage email)
        {
            _selectedEmail = email;
            EmailSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Simulate user changing folder
        /// </summary>
        public void SimulateFolderChange(EmailFolder folder)
        {
            _currentFolder = folder;
            FolderChanged?.Invoke(this, folder);
        }

        /// <summary>
        /// Get current email count
        /// </summary>
        public int EmailCount => _emails.Count;

        /// <summary>
        /// Get email at specified index
        /// </summary>
        public EmailMessage GetEmail(int index) => _emails[index];

        /// <summary>
        /// Get current unread count value
        /// </summary>
        public int GetUnreadCount() => _unreadCount;

        /// <summary>
        /// Get current status message value
        /// </summary>
        public string GetStatusMessage() => _statusMessage;

        /// <summary>
        /// Get current loading state value
        /// </summary>
        public bool GetIsLoading() => _isLoading;

        /// <summary>
        /// Clear all state and method calls
        /// </summary>
        public void Clear()
        {
            _emails.Clear();
            _selectedEmail = null;
            _currentFolder = EmailFolder.Inbox;
            _unreadCount = 0;
            _statusMessage = string.Empty;
            _isLoading = false;
            MethodCalls.Clear();
        }
    }
}
