using System.Drawing;
using System.Drawing.Printing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations.DialogOptions;

namespace WinformsMVP.Services
{
    /// <summary>
    /// WinForms-specific dialogs whose results are GDI+/printing types. Kept off the
    /// presenter-facing <see cref="IDialogProvider"/> so that interface stays platform-neutral;
    /// resolve this one from view-side code (or a WinForms-aware service) that needs it.
    /// </summary>
    public interface IWinFormsDialogProvider : IDialogProvider
    {
        InteractionResult<Font> ShowFontDialog(FontDialogOptions options = null);

        InteractionResult<PrinterSettings> ShowPrintDialog(PrintDialogOptions options = null);
        InteractionResult<PageSettings> ShowPageSetupDialog(PageSetupDialogOptions options = null);
        InteractionResult<bool> ShowPrintPreviewDialog(PrintPreviewDialogOptions options = null);
    }
}
