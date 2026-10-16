namespace WinformsMVP.Services.Implementations.DialogOptions
{
    /// <summary>
    /// Option settings for Open File Dialog.
    /// Title is optional (automatically uses default value if not specified).
    /// </summary>
    public class OpenFileDialogOptions
    {
        /// <summary>
        /// File filter (e.g., "Text Files|*.txt|All Files|*.*")
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Initial directory
        /// </summary>
        public string InitialDirectory { get; set; }

        /// <summary>
        /// Default file name
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Whether to display a warning if the file does not exist
        /// </summary>
        public bool CheckFileExists { get; set; } = true;

        /// <summary>
        /// Whether to display a warning if the path does not exist
        /// </summary>
        public bool CheckPathExists { get; set; } = true;

        /// <summary>
        /// Title (normally not required; if null, DialogDefaults.OpenFileDialogTitle is used)
        /// </summary>
        public string Title { get; set; }
    }
}
