using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations.DialogOptions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Provides presenter-facing dialog abstractions for file, folder and color selection.
    /// </summary>
    /// <remarks>
    /// Font and print dialogs return GDI+/WinForms types (<c>Font</c>, <c>PrinterSettings</c>),
    /// so they live on <c>IWinFormsDialogProvider</c> in the WinformsMVP package instead.
    /// </remarks>
    public interface IDialogProvider
    {
        // File dialogs
        InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null);
        InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null);
        InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null);
        InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null);

        // Style dialogs
        InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null);
    }
}
