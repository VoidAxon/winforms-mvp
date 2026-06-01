using WinformsMVP.Samples.EmailDemo.Models;

namespace WinformsMVP.Samples.EmailDemo
{
    /// <summary>
    /// Compose email mode
    /// </summary>
    public enum ComposeMode
    {
        /// <summary>New email</summary>
        New,
        /// <summary>Reply to email</summary>
        Reply,
        /// <summary>Forward email</summary>
        Forward
    }

    /// <summary>
    /// Initialization parameters for compose email window
    /// </summary>
    public class ComposeEmailParameters
    {
        /// <summary>
        /// Compose mode
        /// </summary>
        public ComposeMode Mode { get; set; }

        /// <summary>
        /// Original email (for reply or forward)
        /// </summary>
        public EmailMessage OriginalEmail { get; set; }
    }
}
