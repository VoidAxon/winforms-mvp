using System.Drawing;
using System.Drawing.Printing;
using Kibinago.Common;
using Kibinago.Services.Implementations.DialogOptions;

namespace Kibinago.Services
{
    /// <summary>
    /// WinForms-specific dialogs whose results are GDI+/printing types. Kept off the
    /// presenter-facing <see cref="IDialogProvider"/> so that interface stays platform-neutral;
    /// resolve this one from view-side code (or a WinForms-aware service) that needs it.
    /// </summary>
    public interface IWinFormsDialogProvider : IDialogProvider
    {
        /// <summary>Shows a font dialog.</summary>
        /// <param name="options">Dialog settings, or <c>null</c> for the defaults.</param>
        /// <returns><c>Ok</c> with the chosen font, <c>Cancel</c> when the user cancels, or <c>Error</c> when the dialog fails.</returns>
        InteractionResult<Font> ShowFontDialog(FontDialogOptions options = null);

        /// <summary>Shows a print dialog.</summary>
        /// <param name="options">Dialog settings, or <c>null</c> for the defaults.</param>
        /// <returns><c>Ok</c> with the chosen printer settings, <c>Cancel</c> when the user cancels, or <c>Error</c> when the dialog fails.</returns>
        InteractionResult<PrinterSettings> ShowPrintDialog(PrintDialogOptions options = null);
        /// <summary>Shows a page setup dialog.</summary>
        /// <param name="options">Dialog settings, or <c>null</c> for the defaults.</param>
        /// <returns><c>Ok</c> with the chosen page settings, <c>Cancel</c> when the user cancels, or <c>Error</c> when the dialog fails.</returns>
        InteractionResult<PageSettings> ShowPageSetupDialog(PageSetupDialogOptions options = null);
        /// <summary>Shows a print preview of <see cref="PrintPreviewDialogOptions.Document"/>.</summary>
        /// <param name="options">The document to preview and the window title.</param>
        /// <returns><c>Ok(true)</c> once the preview has been shown and closed (the preview has no OK/Cancel answer), or <c>Error</c> when the dialog fails.</returns>
        InteractionResult<bool> ShowPrintPreviewDialog(PrintPreviewDialogOptions options = null);
    }
}
