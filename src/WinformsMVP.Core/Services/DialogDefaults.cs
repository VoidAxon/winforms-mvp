namespace WinformsMVP.Services
{
    /// <summary>
    /// Provides default values for dialog providers.
    /// These values can be customized at application startup.
    /// </summary>
    public static class DialogDefaults
    {
        #region File Dialog Defaults

        /// <summary>
        /// Default title for open file dialog
        /// </summary>
        public static string OpenFileDialogTitle { get; set; } = "Open File";

        /// <summary>
        /// Default title for save file dialog
        /// </summary>
        public static string SaveFileDialogTitle { get; set; } = "Save File";

        /// <summary>
        /// Default title for folder browser dialog
        /// </summary>
        public static string FolderBrowserDialogTitle { get; set; } = "Select Folder";

        /// <summary>
        /// Default filter for file dialogs
        /// </summary>
        public static string DefaultFileFilter { get; set; } = "All Files (*.*)|*.*";

        /// <summary>
        /// Default description for folder browser dialog
        /// </summary>
        public static string FolderBrowserDescription { get; set; } = "Please select a folder";

        #endregion

        #region Print Dialog Defaults

        /// <summary>
        /// Default title for print preview dialog
        /// </summary>
        public static string PrintPreviewDialogTitle { get; set; } = "Print Preview";

        #endregion

        #region Message Dialog Defaults

        /// <summary>
        /// Default caption for message boxes
        /// </summary>
        public static string DefaultMessageCaption { get; set; } = "System";

        #endregion

        #region Error Messages

        /// <summary>
        /// Error message when file open operation fails
        /// </summary>
        public static string FileOpenErrorMessage { get; set; } = "Failed to open file";

        /// <summary>
        /// Error message when file save operation fails
        /// </summary>
        public static string FileSaveErrorMessage { get; set; } = "Failed to save file";

        /// <summary>
        /// Error message when folder selection fails
        /// </summary>
        public static string FolderSelectErrorMessage { get; set; } = "Failed to select folder";

        /// <summary>
        /// Error message when color selection fails
        /// </summary>
        public static string ColorSelectErrorMessage { get; set; } = "Failed to select color";

        /// <summary>
        /// Error message when font selection fails
        /// </summary>
        public static string FontSelectErrorMessage { get; set; } = "Failed to select font";

        /// <summary>
        /// Error message when print dialog fails
        /// </summary>
        public static string PrintDialogErrorMessage { get; set; } = "Print dialog failed";

        /// <summary>
        /// Error message when page setup fails
        /// </summary>
        public static string PageSetupErrorMessage { get; set; } = "Page setup failed";

        /// <summary>
        /// Error message when print preview fails
        /// </summary>
        public static string PrintPreviewErrorMessage { get; set; } = "Print preview failed";

        #endregion
    }
}
