using System.Collections.Generic;
using System.Threading.Tasks;
using Kibinago.Samples.EmailDemo.Models;

namespace Kibinago.Samples.EmailDemo.Services
{
    /// <summary>
    /// Email repository interface
    /// </summary>
    public interface IEmailRepository
    {
        /// <summary>
        /// Get all emails from specified folder
        /// </summary>
        Task<IEnumerable<EmailMessage>> GetEmailsByFolderAsync(EmailFolder folder);

        /// <summary>
        /// Get email by ID
        /// </summary>
        Task<EmailMessage> GetEmailByIdAsync(int id);

        /// <summary>
        /// Send email (move to Sent folder)
        /// </summary>
        Task<bool> SendEmailAsync(EmailMessage email);

        /// <summary>
        /// Save draft
        /// </summary>
        Task<int> SaveDraftAsync(EmailMessage email);

        /// <summary>
        /// Delete email (move to Trash)
        /// </summary>
        Task<bool> DeleteEmailAsync(int id);

        /// <summary>
        /// Permanently delete email
        /// </summary>
        Task<bool> PermanentlyDeleteEmailAsync(int id);

        /// <summary>
        /// Mark email as read/unread
        /// </summary>
        Task<bool> MarkAsReadAsync(int id, bool isRead);

        /// <summary>
        /// Toggle email star
        /// </summary>
        Task<bool> ToggleStarAsync(int id);

        /// <summary>
        /// Search emails
        /// </summary>
        Task<IEnumerable<EmailMessage>> SearchEmailsAsync(string keyword, EmailFolder? folder = null);

        /// <summary>
        /// Get unread email count
        /// </summary>
        Task<int> GetUnreadCountAsync(EmailFolder folder);
    }
}
