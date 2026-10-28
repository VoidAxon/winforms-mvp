using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kibinago.Views;
using Kibinago.ViewActions;
using Kibinago.Samples.EmailDemo.Models;

namespace Kibinago.Samples.EmailDemo
{
    /// <summary>
    /// Main email view Form implementation
    /// Demonstrates complete email client UI
    /// </summary>
    public partial class MainEmailForm : Form, IMainEmailView
    {
        private ViewActionBinder _binder;
        private EmailMessage _selectedEmail;
        private IEnumerable<EmailMessage> _emails;

        public MainEmailForm()
        {
            InitializeComponent();
            SetupUI();
            InitializeActionBindings();
        }

        private void InitializeActionBindings()
        {
            _binder = new ViewActionBinder();

            // Email operations
            _binder.Add(EmailActions.Compose, composeButton);
            _binder.Add(EmailActions.Reply, replyButton);
            _binder.Add(EmailActions.Forward, forwardButton);
            _binder.Add(EmailActions.Delete, deleteButton);
            _binder.Add(EmailActions.Refresh, refreshButton);

            // Email status operations
            _binder.Add(EmailActions.MarkAsRead, markReadButton);
            _binder.Add(EmailActions.MarkAsUnread, markUnreadButton);
            _binder.Add(EmailActions.ToggleStar, toggleStarButton);

            // No Bind() call here - Presenter will call View.ActionBinder.Bind()
        }

        #region IMainEmailView Implementation

        public IEnumerable<EmailMessage> Emails
        {
            set
            {
                _emails = value;
            }
        }

        public EmailMessage SelectedEmail => _selectedEmail;

        public bool HasSelection => _selectedEmail != null;

        private EmailFolder _currentFolder = EmailFolder.Inbox;
        public EmailFolder CurrentFolder
        {
            get => _currentFolder;
            set
            {
                _currentFolder = value;
                UpdateFolderSelection();
            }
        }

        public int UnreadCount
        {
            set
            {
                // Update unread count display in folder list
                UpdateFolderList();
            }
        }

        public string StatusMessage
        {
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => statusLabel.Text = value));
                }
                else
                {
                    statusLabel.Text = value;
                }
            }
        }

        public bool IsLoading
        {
            set
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        progressBar.Visible = value;
                        toolStrip.Enabled = !value;
                    }));
                }
                else
                {
                    progressBar.Visible = value;
                    toolStrip.Enabled = !value;
                }
            }
        }

        public void ShowEmailPreview(EmailMessage email)
        {
            if (email == null) return;

            previewFromLabel.Text = $"From: {email.From}";
            previewSubjectLabel.Text = email.Subject;
            previewDateLabel.Text = email.Date.ToString("yyyy-MM-dd HH:mm");
            previewTextBox.Text = email.Body;

            // Display unread emails in bold
            previewSubjectLabel.Font = email.IsRead
                ? new Font(previewSubjectLabel.Font, FontStyle.Regular)
                : new Font(previewSubjectLabel.Font, FontStyle.Bold);
        }

        public void ClearEmailPreview()
        {
            previewFromLabel.Text = "";
            previewSubjectLabel.Text = "";
            previewDateLabel.Text = "";
            previewTextBox.Text = "";
        }

        public void RefreshEmailList()
        {
            emailListView.Items.Clear();

            if (_emails == null) return;

            foreach (var email in _emails)
            {
                var item = new ListViewItem(new[]
                {
                    email.IsStarred ? "★" : "",
                    email.IsRead ? "" : "●",
                    email.GetFromDisplayName(),
                    email.Subject,
                    email.GetBodyPreview(50),
                    email.Date.ToString("MM/dd HH:mm")
                })
                {
                    Tag = email,
                    Font = email.IsRead
                        ? emailListView.Font
                        : new Font(emailListView.Font, FontStyle.Bold)
                };

                emailListView.Items.Add(item);
            }

            // Auto-resize columns
            emailListView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        public event EventHandler EmailSelectionChanged;
        public event EventHandler<EmailFolder> FolderChanged;

        public IViewActionBinder ActionBinder => _binder;

        #endregion
        #region UI Setup

        private void UpdateFolderList()
        {
            // Can update unread count display here
            // e.g.: "📥 Inbox (5)"
        }

        private void UpdateFolderSelection()
        {
            folderListBox.SelectedIndex = (int)_currentFolder;
        }

        #endregion

        #region Event Handlers

        private void FolderListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (folderListBox.SelectedIndex < 0) return;

            var folder = (EmailFolder)folderListBox.SelectedIndex;
            FolderChanged?.Invoke(this, folder);
        }

        private void EmailListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (emailListView.SelectedItems.Count > 0)
            {
                _selectedEmail = emailListView.SelectedItems[0].Tag as EmailMessage;
            }
            else
            {
                _selectedEmail = null;
            }

            EmailSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EmailListView_DoubleClick(object sender, EventArgs e)
        {
            if (HasSelection && _binder != null)
            {
                // Double-click to open email details
                // Note: Need access to dispatcher for this action
                // This is a View-level behavior that could trigger presenter action
            }
        }

        #endregion
        #region IDisposable

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
