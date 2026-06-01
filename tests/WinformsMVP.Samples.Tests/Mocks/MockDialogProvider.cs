using System.Drawing;
using System.Drawing.Printing;
using WinformsMVP.Common;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations.DialogOptions;

namespace WinformsMVP.Samples.Tests.Mocks
{
    /// <summary>
    /// Mock implementation of IDialogProvider for testing.
    /// </summary>
    public class MockDialogProvider : IDialogProvider
    {
        // Control return values
        public string OpenFileDialogResult { get; set; }
        public string[] OpenMultipleFilesDialogResult { get; set; }
        public string SaveFileDialogResult { get; set; }
        public string FolderBrowserDialogResult { get; set; }
        public bool PrintPreviewDialogResult { get; set; }

        public InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null)
        {
            if (string.IsNullOrEmpty(OpenFileDialogResult))
                return InteractionResult<string>.Cancel();

            return InteractionResult<string>.Ok(OpenFileDialogResult);
        }

        public InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null)
        {
            if (OpenMultipleFilesDialogResult == null || OpenMultipleFilesDialogResult.Length == 0)
                return InteractionResult<string[]>.Cancel();

            return InteractionResult<string[]>.Ok(OpenMultipleFilesDialogResult);
        }

        public InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null)
        {
            if (string.IsNullOrEmpty(SaveFileDialogResult))
                return InteractionResult<string>.Cancel();

            return InteractionResult<string>.Ok(SaveFileDialogResult);
        }

        public InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null)
        {
            if (string.IsNullOrEmpty(FolderBrowserDialogResult))
                return InteractionResult<string>.Cancel();

            return InteractionResult<string>.Ok(FolderBrowserDialogResult);
        }

        public InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null)
        {
            return InteractionResult<Color>.Cancel();
        }

        public InteractionResult<Font> ShowFontDialog(FontDialogOptions options = null)
        {
            return InteractionResult<Font>.Cancel();
        }

        public InteractionResult<PrinterSettings> ShowPrintDialog(PrintDialogOptions options = null)
        {
            return InteractionResult<PrinterSettings>.Cancel();
        }

        public InteractionResult<PageSettings> ShowPageSetupDialog(PageSetupDialogOptions options = null)
        {
            return InteractionResult<PageSettings>.Cancel();
        }

        public InteractionResult<bool> ShowPrintPreviewDialog(PrintPreviewDialogOptions options = null)
        {
            if (!PrintPreviewDialogResult)
                return InteractionResult<bool>.Cancel();

            return InteractionResult<bool>.Ok(PrintPreviewDialogResult);
        }
    }
}
