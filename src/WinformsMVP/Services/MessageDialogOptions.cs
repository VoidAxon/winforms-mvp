namespace WinformsMVP.Services
{
    /// <summary>
    /// Settings for message boxes (<c>mvp.Messages</c>). Copied when the message service is created;
    /// later edits have no effect.
    /// </summary>
    public sealed class MessageDialogOptions
    {
        /// <summary>Title used when a call passes no caption. Default: "System".</summary>
        public string DefaultCaption { get; set; } = "System";

        internal MessageDialogOptions Copy() => (MessageDialogOptions)MemberwiseClone();
    }
}
