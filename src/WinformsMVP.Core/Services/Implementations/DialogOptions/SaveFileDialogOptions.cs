namespace WinformsMVP.Services.Implementations.DialogOptions
{
    /// <summary>
    /// Option settings for Save File Dialog.
    /// Title is optional (automatically uses default value if not specified).
    /// </summary>
    public class SaveFileDialogOptions
    {
        /// <summary>
        /// File filter (e.g., "Text Files|*.txt|All Files|*.*")
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Default extension (e.g., "txt")
        /// </summary>
        public string DefaultExt { get; set; }

        /// <summary>
        /// Initial directory
        /// </summary>
        public string InitialDirectory { get; set; }

        /// <summary>
        /// Default file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Whether to display overwrite confirmation
        /// </summary>
        public bool OverwritePrompt { get; set; } = true;

        /// <summary>
        /// Whether to automatically create the path if it doesn't exist
        /// </summary>
        public bool CreatePrompt { get; set; } = false;

        /// <summary>
        /// Title (normally not required; if null, DialogDefaults.SaveFileDialogTitle is used)
        /// </summary>
        public string Title { get; set; }
    }
}
