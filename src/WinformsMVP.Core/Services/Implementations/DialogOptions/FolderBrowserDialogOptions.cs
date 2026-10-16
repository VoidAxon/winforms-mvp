namespace WinformsMVP.Services.Implementations.DialogOptions
{
    public class FolderBrowserDialogOptions
    {
        public string Description { get; set; } = "Select Folder";
        public bool ShowNewFolderButton { get; set; } = true;
        public string SelectedPath { get; set; }
    }
}
