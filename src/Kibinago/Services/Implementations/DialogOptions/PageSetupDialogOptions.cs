using System.Drawing.Printing;

namespace Kibinago.Services.Implementations.DialogOptions
{
    /// <summary>Settings for <see cref="IWinFormsDialogProvider.ShowPageSetupDialog"/>.</summary>
    public class PageSetupDialogOptions
    {
        /// <summary>The page settings to edit; <c>null</c> starts from new default settings.</summary>
        public PageSettings PageSettings { get; set; }
        /// <summary>The printer settings for the dialog. Not applied by the built-in <see cref="DialogProvider"/>.</summary>
        public PrinterSettings PrinterSettings { get; set; }
    }
}
