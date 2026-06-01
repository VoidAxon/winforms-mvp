using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinformsMVP.Samples.EmailDemo.Models;

namespace WinformsMVP.Samples.EmailDemo.Services
{
    /// <summary>
    /// In-memory email repository implementation (for demo)
    /// </summary>
    public class InMemoryEmailRepository : IEmailRepository
    {
        private readonly List<EmailMessage> _emails = new List<EmailMessage>();
        private int _nextId = 1;
        private readonly object _lock = new object();

        public InMemoryEmailRepository()
        {
            SeedSampleData();
        }

        public Task<IEnumerable<EmailMessage>> GetEmailsByFolderAsync(EmailFolder folder)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    return _emails.Where(e => e.Folder == folder).OrderByDescending(e => e.Date).ToList().AsEnumerable();
                }
            });
        }

        public Task<EmailMessage> GetEmailByIdAsync(int id)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    return _emails.FirstOrDefault(e => e.Id == id);
                }
            });
        }

        public Task<bool> SendEmailAsync(EmailMessage email)
        {
            return Task.Run(() =>
            {
                // Simulate network delay
                Task.Delay(500).Wait();

                lock (_lock)
                {
                    email.Date = DateTime.Now;
                    email.Folder = EmailFolder.Sent;
                    email.IsRead = true;

                    if (email.Id == 0)
                    {
                        email.Id = _nextId++;
                        _emails.Add(email);
                    }
                    else
                    {
                        var existing = _emails.FirstOrDefault(e => e.Id == email.Id);
                        if (existing != null)
                        {
                            _emails.Remove(existing);
                            _emails.Add(email);
                        }
                    }

                    return true;
                }
            });
        }

        public Task<int> SaveDraftAsync(EmailMessage email)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    email.Folder = EmailFolder.Drafts;
                    email.Date = DateTime.Now;

                    if (email.Id == 0)
                    {
                        email.Id = _nextId++;
                        _emails.Add(email);
                    }
                    else
                    {
                        var existing = _emails.FirstOrDefault(e => e.Id == email.Id);
                        if (existing != null)
                        {
                            existing.To = email.To;
                            existing.Subject = email.Subject;
                            existing.Body = email.Body;
                            existing.Date = DateTime.Now;
                        }
                    }

                    return email.Id;
                }
            });
        }

        public Task<bool> DeleteEmailAsync(int id)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var email = _emails.FirstOrDefault(e => e.Id == id);
                    if (email != null)
                    {
                        email.Folder = EmailFolder.Trash;
                        return true;
                    }
                    return false;
                }
            });
        }

        public Task<bool> PermanentlyDeleteEmailAsync(int id)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var email = _emails.FirstOrDefault(e => e.Id == id);
                    if (email != null)
                    {
                        _emails.Remove(email);
                        return true;
                    }
                    return false;
                }
            });
        }

        public Task<bool> MarkAsReadAsync(int id, bool isRead)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var email = _emails.FirstOrDefault(e => e.Id == id);
                    if (email != null)
                    {
                        email.IsRead = isRead;
                        return true;
                    }
                    return false;
                }
            });
        }

        public Task<bool> ToggleStarAsync(int id)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var email = _emails.FirstOrDefault(e => e.Id == id);
                    if (email != null)
                    {
                        email.IsStarred = !email.IsStarred;
                        return true;
                    }
                    return false;
                }
            });
        }

        public Task<IEnumerable<EmailMessage>> SearchEmailsAsync(string keyword, EmailFolder? folder = null)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    var query = _emails.AsEnumerable();

                    if (folder.HasValue)
                        query = query.Where(e => e.Folder == folder.Value);

                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        keyword = keyword.ToLower();
                        query = query.Where(e =>
                            e.Subject?.ToLower().Contains(keyword) == true ||
                            e.Body?.ToLower().Contains(keyword) == true ||
                            e.From?.ToLower().Contains(keyword) == true ||
                            e.To?.ToLower().Contains(keyword) == true);
                    }

                    return query.OrderByDescending(e => e.Date).ToList().AsEnumerable();
                }
            });
        }

        public Task<int> GetUnreadCountAsync(EmailFolder folder)
        {
            return Task.Run(() =>
            {
                lock (_lock)
                {
                    return _emails.Count(e => e.Folder == folder && !e.IsRead);
                }
            });
        }

        /// <summary>
        /// Seed sample data
        /// </summary>
        private void SeedSampleData()
        {
            var now = DateTime.Now;

            // Inbox emails
            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "john.doe@example.com",
                To = "me@mycompany.com",
                Subject = "Welcome to WinForms MVP Framework!",
                Body = "Hi,\n\nWelcome to the WinForms MVP Framework email demo. This is a complete example showing all framework capabilities.\n\nBest regards,\nJohn",
                Date = now.AddHours(-2),
                IsRead = false,
                IsStarred = true,
                Folder = EmailFolder.Inbox
            });

            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "alice.smith@company.com",
                To = "me@mycompany.com",
                Subject = "Project Update - Q4 2026",
                Body = "Dear Team,\n\nHere's the quarterly update for our project. We've made significant progress on all milestones.\n\nPlease review the attached report.\n\nThanks,\nAlice",
                Date = now.AddHours(-5),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Inbox
            });

            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "notifications@github.com",
                To = "me@mycompany.com",
                Subject = "[GitHub] New pull request in winforms-mvp",
                Body = "A new pull request has been opened in your repository.\n\nPR #42: Add email demo to showcase framework capabilities\n\nClick here to review.",
                Date = now.AddDays(-1),
                IsRead = false,
                IsStarred = false,
                Folder = EmailFolder.Inbox
            });

            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "support@microsoft.com",
                To = "me@mycompany.com",
                Subject = "Your .NET subscription renewal",
                Body = "Hello,\n\nThis is a reminder that your .NET developer subscription will expire soon.\n\nRenew now to continue enjoying all benefits.\n\nMicrosoft Team",
                Date = now.AddDays(-2),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Inbox
            });

            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "newsletter@techblog.com",
                To = "me@mycompany.com",
                Subject = "Weekly Tech Digest - Top 10 MVP Patterns",
                Body = "This week's highlights:\n\n1. Model-View-Presenter Best Practices\n2. SOLID Principles in Action\n3. Clean Architecture Tips\n\nRead more on our blog.",
                Date = now.AddDays(-3),
                IsRead = false,
                IsStarred = true,
                Folder = EmailFolder.Inbox
            });

            // Sent emails
            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "me@mycompany.com",
                To = "bob.wilson@client.com",
                Subject = "Re: Meeting Schedule for Next Week",
                Body = "Hi Bob,\n\nTuesday at 2 PM works perfectly for me. I'll send the meeting invite shortly.\n\nLooking forward to our discussion.\n\nBest,\nMe",
                Date = now.AddHours(-1),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Sent
            });

            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "me@mycompany.com",
                To = "team@mycompany.com",
                Subject = "Code Review Guidelines Updated",
                Body = "Team,\n\nI've updated our code review guidelines based on recent feedback. Please review the changes in the wiki.\n\nLet me know if you have questions.\n\nThanks!",
                Date = now.AddDays(-1).AddHours(-3),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Sent
            });

            // Drafts
            _emails.Add(new EmailMessage
            {
                Id = _nextId++,
                From = "me@mycompany.com",
                To = "",
                Subject = "Draft: Ideas for next sprint",
                Body = "Some initial thoughts...\n\n- Feature A\n- Feature B\n- Bug fixes\n\n(Work in progress)",
                Date = now.AddHours(-6),
                IsRead = true,
                IsStarred = false,
                Folder = EmailFolder.Drafts
            });
        }
    }
}
