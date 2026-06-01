using System;

namespace WinformsMVP.Samples.EmailDemo.Models
{
    /// <summary>
    /// Email message data model.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IEquatable{T}"/> for value-based equality so that
    /// <see cref="WinformsMVP.Common.ChangeTracker{T}"/> can correctly detect when
    /// <c>AcceptChanges</c> / <c>RejectChanges</c> have synchronized the current value
    /// with the baseline (otherwise the default reference-equality comparison would
    /// always report <c>IsChanged == true</c> after any clone).
    /// </remarks>
    public class EmailMessage : ICloneable, IEquatable<EmailMessage>
    {
        /// <summary>Email ID</summary>
        public int Id { get; set; }

        /// <summary>Sender email address</summary>
        public string From { get; set; }

        /// <summary>Recipient email address</summary>
        public string To { get; set; }

        /// <summary>Email subject</summary>
        public string Subject { get; set; }

        /// <summary>Email body</summary>
        public string Body { get; set; }

        /// <summary>Received/sent date</summary>
        public DateTime Date { get; set; }

        /// <summary>Whether the email has been read</summary>
        public bool IsRead { get; set; }

        /// <summary>Whether the email is starred</summary>
        public bool IsStarred { get; set; }

        /// <summary>Folder this email belongs to</summary>
        public EmailFolder Folder { get; set; }

        /// <summary>
        /// Create deep copy of email (for ChangeTracker)
        /// </summary>
        public object Clone()
        {
            return new EmailMessage
            {
                Id = this.Id,
                From = this.From,
                To = this.To,
                Subject = this.Subject,
                Body = this.Body,
                Date = this.Date,
                IsRead = this.IsRead,
                IsStarred = this.IsStarred,
                Folder = this.Folder
            };
        }

        /// <summary>
        /// Get email body preview (for list display)
        /// </summary>
        public string GetBodyPreview(int maxLength = 50)
        {
            if (string.IsNullOrWhiteSpace(Body))
                return "(No content)";

            var preview = Body.Replace("\r\n", " ").Replace("\n", " ");
            if (preview.Length > maxLength)
                return preview.Substring(0, maxLength) + "...";

            return preview;
        }

        /// <summary>
        /// Get sender display name (simplified display)
        /// </summary>
        public string GetFromDisplayName()
        {
            if (string.IsNullOrWhiteSpace(From))
                return "Unknown";

            // If contains @, extract username portion
            int atIndex = From.IndexOf('@');
            if (atIndex > 0)
                return From.Substring(0, atIndex);

            return From;
        }

        public bool Equals(EmailMessage other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id &&
                   From == other.From &&
                   To == other.To &&
                   Subject == other.Subject &&
                   Body == other.Body &&
                   Date == other.Date &&
                   IsRead == other.IsRead &&
                   IsStarred == other.IsStarred &&
                   Folder == other.Folder;
        }

        public override bool Equals(object obj) => obj is EmailMessage other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Id.GetHashCode();
                hash = hash * 23 + (From?.GetHashCode() ?? 0);
                hash = hash * 23 + (To?.GetHashCode() ?? 0);
                hash = hash * 23 + (Subject?.GetHashCode() ?? 0);
                hash = hash * 23 + (Body?.GetHashCode() ?? 0);
                hash = hash * 23 + Date.GetHashCode();
                hash = hash * 23 + IsRead.GetHashCode();
                hash = hash * 23 + IsStarred.GetHashCode();
                hash = hash * 23 + Folder.GetHashCode();
                return hash;
            }
        }
    }
}
