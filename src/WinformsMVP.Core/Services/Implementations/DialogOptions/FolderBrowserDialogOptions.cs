namespace WinformsMVP.Services.Implementations.DialogOptions
{
    public class FolderBrowserDialogOptions
    {
        /// <summary>Text shown above the folder tree. <c>null</c> = the provider's configured description.</summary>
        public string Description { get; set; }
        public bool ShowNewFolderButton { get; set; } = true;
        public string SelectedPath { get; set; }
    }
}
