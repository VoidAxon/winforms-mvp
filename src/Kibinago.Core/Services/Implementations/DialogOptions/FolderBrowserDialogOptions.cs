namespace Kibinago.Services.Implementations.DialogOptions
{
    /// <summary>
    /// Option settings for <see cref="IDialogProvider.ShowFolderBrowserDialog"/>.
    /// </summary>
    public class FolderBrowserDialogOptions
    {
        /// <summary>Text shown above the folder tree. <c>null</c> = the provider's configured description.</summary>
        public string Description { get; set; }
        /// <summary>Whether the dialog offers a New Folder button. Defaults to <c>true</c>.</summary>
        public bool ShowNewFolderButton { get; set; } = true;
        /// <summary>Folder selected when the dialog opens.</summary>
        public string SelectedPath { get; set; }
    }
}
