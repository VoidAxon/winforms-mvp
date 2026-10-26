namespace Kibinago.Services
{
    /// <summary>
    /// Default titles, filter and error messages for the built-in dialog provider
    /// (<see cref="DialogProvider"/>), set through <c>mvp.Dialogs</c>. A value
    /// given per call (e.g. <c>OpenFileDialogOptions.Title</c>) wins. Copied when the provider is
    /// created; later edits have no effect.
    /// </summary>
    public sealed class DialogProviderOptions
    {
        /// <summary>Title of the open-file dialog.</summary>
        public string OpenFileDialogTitle { get; set; } = "Open File";

        /// <summary>Title of the save-file dialog.</summary>
        public string SaveFileDialogTitle { get; set; } = "Save File";

        /// <summary>Filter of the file dialogs.</summary>
        public string DefaultFileFilter { get; set; } = "All Files (*.*)|*.*";

        /// <summary>Description shown in the folder browser.</summary>
        public string FolderBrowserDescription { get; set; } = "Please select a folder";

        /// <summary>Title of the print preview dialog.</summary>
        public string PrintPreviewDialogTitle { get; set; } = "Print Preview";

        /// <summary>Error message when the open-file dialog fails.</summary>
        public string FileOpenErrorMessage { get; set; } = "Failed to open file";

        /// <summary>Error message when the save-file dialog fails.</summary>
        public string FileSaveErrorMessage { get; set; } = "Failed to save file";

        /// <summary>Error message when the folder browser fails.</summary>
        public string FolderSelectErrorMessage { get; set; } = "Failed to select folder";

        /// <summary>Error message when the color dialog fails.</summary>
        public string ColorSelectErrorMessage { get; set; } = "Failed to select color";

        /// <summary>Error message when the font dialog fails.</summary>
        public string FontSelectErrorMessage { get; set; } = "Failed to select font";

        /// <summary>Error message when the print dialog fails.</summary>
        public string PrintDialogErrorMessage { get; set; } = "Print dialog failed";

        /// <summary>Error message when page setup fails.</summary>
        public string PageSetupErrorMessage { get; set; } = "Page setup failed";

        /// <summary>Error message when print preview fails.</summary>
        public string PrintPreviewErrorMessage { get; set; } = "Print preview failed";

        internal DialogProviderOptions Copy() => (DialogProviderOptions)MemberwiseClone();
    }
}
