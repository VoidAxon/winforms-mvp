using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinformsMVP.Samples.EmailDemo.Models;
using WinformsMVP.Samples.EmailDemo.Services;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IEmailRepository for testing.
    /// Provides in-memory storage and records all method calls.
    /// </summary>
    public class MockEmailRepository : IEmailRepository
    {
        // In-memory storage
        private readonly List<EmailMessage> _emails = new List<EmailMessage>();
        private int _nextId = 1;

        // Recording lists for verification
        public List<string> MethodCalls { get; } = new List<string>();
        public List<int> DeleteCalls { get; } = new List<int>();
        public List<(int id, bool isRead)> MarkAsReadCalls { get; } = new List<(int id, bool isRead)>();
        public List<int> ToggleStarCalls { get; } = new List<int>();
        public List<int> PermanentlyDeleteCalls { get; } = new List<int>();

        // Configurable return values
        public bool SendEmailAsyncResult { get; set; } = true;
        public int SaveDraftResult { get; set; } = -1; // -1 means auto-generate
        public bool DeleteAsyncResult { get; set; } = true;
        public bool PermanentlyDeleteAsyncResult { get; set; } = true;
        public bool MarkAsReadAsyncResult { get; set; } = true;
        public bool ToggleStarAsyncResult { get; set; } = true;

        /// <summary>
        /// Get all emails from specified folder
        /// </summary>
        public Task<IEnumerable<EmailMessage>> GetEmailsByFolderAsync(EmailFolder folder)
        {
            MethodCalls.Add($"GetEmailsByFolderAsync({folder})");
            var result = _emails.Where(e => e.Folder == folder).ToList();
            return Task.FromResult<IEnumerable<EmailMessage>>(result);
        }

        /// <summary>
        /// Get email by ID
        /// </summary>
        public Task<EmailMessage> GetEmailByIdAsync(int id)
        {
            MethodCalls.Add($"GetEmailByIdAsync({id})");
            var result = _emails.FirstOrDefault(e => e.Id == id);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Send email (move to Sent folder)
        /// </summary>
        public Task<bool> SendEmailAsync(EmailMessage email)
        {
            MethodCalls.Add($"SendEmailAsync({email?.Subject ?? "null"})");

            if (SendEmailAsyncResult && email != null)
            {
                if (email.Id == 0)
                {
                    email.Id = _nextId++;
                }
                email.Folder = EmailFolder.Sent;
                email.Date = DateTime.Now;

                var existing = _emails.FirstOrDefault(e => e.Id == email.Id);
                if (existing != null)
                {
                    _emails.Remove(existing);
                }
                _emails.Add(email);
            }

            return Task.FromResult(SendEmailAsyncResult);
        }

        /// <summary>
        /// Save draft
        /// </summary>
        public Task<int> SaveDraftAsync(EmailMessage email)
        {
            MethodCalls.Add($"SaveDraftAsync({email?.Subject ?? "null"})");

            if (email != null)
            {
                if (email.Id == 0)
                {
                    email.Id = SaveDraftResult == -1 ? _nextId++ : SaveDraftResult;
                }
                email.Folder = EmailFolder.Drafts;
                email.Date = DateTime.Now;

                var existing = _emails.FirstOrDefault(e => e.Id == email.Id);
                if (existing != null)
                {
                    _emails.Remove(existing);
                }
                _emails.Add(email);

                return Task.FromResult(email.Id);
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Delete email (move to Trash)
        /// </summary>
        public Task<bool> DeleteEmailAsync(int id)
        {
            MethodCalls.Add($"DeleteEmailAsync({id})");
            DeleteCalls.Add(id);

            if (DeleteAsyncResult)
            {
                var email = _emails.FirstOrDefault(e => e.Id == id);
                if (email != null)
                {
                    email.Folder = EmailFolder.Trash;
                }
            }

            return Task.FromResult(DeleteAsyncResult);
        }

        /// <summary>
        /// Permanently delete email
        /// </summary>
        public Task<bool> PermanentlyDeleteEmailAsync(int id)
        {
            MethodCalls.Add($"PermanentlyDeleteEmailAsync({id})");
            PermanentlyDeleteCalls.Add(id);

            if (PermanentlyDeleteAsyncResult)
            {
                var email = _emails.FirstOrDefault(e => e.Id == id);
                if (email != null)
                {
                    _emails.Remove(email);
                }
            }

            return Task.FromResult(PermanentlyDeleteAsyncResult);
        }

        /// <summary>
        /// Mark email as read/unread
        /// </summary>
        public Task<bool> MarkAsReadAsync(int id, bool isRead)
        {
            MethodCalls.Add($"MarkAsReadAsync({id}, {isRead})");
            MarkAsReadCalls.Add((id, isRead));

            if (MarkAsReadAsyncResult)
            {
                var email = _emails.FirstOrDefault(e => e.Id == id);
                if (email != null)
                {
                    email.IsRead = isRead;
                }
            }

            return Task.FromResult(MarkAsReadAsyncResult);
        }

        /// <summary>
        /// Toggle email star
        /// </summary>
        public Task<bool> ToggleStarAsync(int id)
        {
            MethodCalls.Add($"ToggleStarAsync({id})");
            ToggleStarCalls.Add(id);

            if (ToggleStarAsyncResult)
            {
                var email = _emails.FirstOrDefault(e => e.Id == id);
                if (email != null)
                {
                    email.IsStarred = !email.IsStarred;
                }
            }

            return Task.FromResult(ToggleStarAsyncResult);
        }

        /// <summary>
        /// Search emails
        /// </summary>
        public Task<IEnumerable<EmailMessage>> SearchEmailsAsync(string keyword, EmailFolder? folder = null)
        {
            MethodCalls.Add($"SearchEmailsAsync({keyword}, {folder})");

            var query = _emails.AsEnumerable();

            if (folder.HasValue)
            {
                query = query.Where(e => e.Folder == folder.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.ToLower();
                query = query.Where(e =>
                    (e.Subject?.ToLower().Contains(keyword) ?? false) ||
                    (e.Body?.ToLower().Contains(keyword) ?? false) ||
                    (e.From?.ToLower().Contains(keyword) ?? false) ||
                    (e.To?.ToLower().Contains(keyword) ?? false));
            }

            var result = query.ToList();
            return Task.FromResult<IEnumerable<EmailMessage>>(result);
        }

        /// <summary>
        /// Get unread email count
        /// </summary>
        public Task<int> GetUnreadCountAsync(EmailFolder folder)
        {
            MethodCalls.Add($"GetUnreadCountAsync({folder})");
            var count = _emails.Count(e => e.Folder == folder && !e.IsRead);
            return Task.FromResult(count);
        }

        // Test helper methods

        /// <summary>
        /// Add email to in-memory storage (for test setup)
        /// </summary>
        public void AddEmail(EmailMessage email)
        {
            if (email.Id == 0)
            {
                email.Id = _nextId++;
            }
            _emails.Add(email);
        }

        /// <summary>
        /// Get all emails in storage
        /// </summary>
        public IEnumerable<EmailMessage> GetAllEmails() => _emails.ToList();

        /// <summary>
        /// Get email count
        /// </summary>
        public int EmailCount => _emails.Count;

        /// <summary>
        /// Clear all state and method calls
        /// </summary>
        public void Clear()
        {
            _emails.Clear();
            _nextId = 1;
            MethodCalls.Clear();
            DeleteCalls.Clear();
            MarkAsReadCalls.Clear();
            ToggleStarCalls.Clear();
            PermanentlyDeleteCalls.Clear();

            // Reset configurable results to defaults
            SendEmailAsyncResult = true;
            SaveDraftResult = -1;
            DeleteAsyncResult = true;
            PermanentlyDeleteAsyncResult = true;
            MarkAsReadAsyncResult = true;
            ToggleStarAsyncResult = true;
        }
    }
}
