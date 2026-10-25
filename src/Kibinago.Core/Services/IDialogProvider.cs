using System.Drawing;
using Kibinago.Common;
using Kibinago.Services.Implementations.DialogOptions;

namespace Kibinago.Services
{
    /// <summary>
    /// Provides presenter-facing dialog abstractions for file, folder and color selection.
    /// </summary>
    /// <remarks>
    /// Font and print dialogs return GDI+/WinForms types (<c>Font</c>, <c>PrinterSettings</c>),
    /// so they live on <c>IWinFormsDialogProvider</c> in the Kibinago package instead.
    /// </remarks>
    public interface IDialogProvider
    {
        // File dialogs
        /// <summary>Lets the user pick one existing file to open.</summary>
        /// <param name="options">Dialog settings; <c>null</c> uses the provider's configured defaults.</param>
        /// <returns><see cref="InteractionStatus.Ok"/> with the selected file path; <see cref="InteractionStatus.Cancel"/>
        /// when the user cancels; <see cref="InteractionStatus.Error"/> when the dialog could not be shown.</returns>
        InteractionResult<string> ShowOpenFileDialog(OpenFileDialogOptions options = null);
        /// <summary>Lets the user pick one or more existing files to open.</summary>
        /// <param name="options">Dialog settings; <c>null</c> uses the provider's configured defaults.</param>
        /// <returns><see cref="InteractionStatus.Ok"/> with the selected file paths; <see cref="InteractionStatus.Cancel"/>
        /// when the user cancels; <see cref="InteractionStatus.Error"/> when the dialog could not be shown.</returns>
        InteractionResult<string[]> ShowOpenMultipleFilesDialog(OpenFileDialogOptions options = null);
        /// <summary>Lets the user choose a file path to save to.</summary>
        /// <param name="options">Dialog settings; <c>null</c> uses the provider's configured defaults.</param>
        /// <returns><see cref="InteractionStatus.Ok"/> with the chosen file path; <see cref="InteractionStatus.Cancel"/>
        /// when the user cancels; <see cref="InteractionStatus.Error"/> when the dialog could not be shown.</returns>
        InteractionResult<string> ShowSaveFileDialog(SaveFileDialogOptions options = null);
        /// <summary>Lets the user pick a folder.</summary>
        /// <param name="options">Dialog settings; <c>null</c> uses the provider's configured defaults.</param>
        /// <returns><see cref="InteractionStatus.Ok"/> with the selected folder path; <see cref="InteractionStatus.Cancel"/>
        /// when the user cancels; <see cref="InteractionStatus.Error"/> when the dialog could not be shown.</returns>
        InteractionResult<string> ShowFolderBrowserDialog(FolderBrowserDialogOptions options = null);

        // Style dialogs
        /// <summary>Lets the user pick a color.</summary>
        /// <param name="options">Dialog settings; <c>null</c> uses the provider's configured defaults.</param>
        /// <returns><see cref="InteractionStatus.Ok"/> with the selected color; <see cref="InteractionStatus.Cancel"/>
        /// when the user cancels; <see cref="InteractionStatus.Error"/> when the dialog could not be shown.</returns>
        InteractionResult<Color> ShowColorDialog(ColorDialogOptions options = null);
    }
}
