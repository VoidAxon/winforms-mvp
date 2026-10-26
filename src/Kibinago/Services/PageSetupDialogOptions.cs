using System.Drawing.Printing;

namespace Kibinago.Services
{
    /// <summary>Settings for <see cref="IWinFormsDialogProvider.ShowPageSetupDialog"/>.</summary>
    public class PageSetupDialogOptions
    {
        /// <summary>The page settings to edit; <c>null</c> starts from new default settings.</summary>
        public PageSettings PageSettings { get; set; }
        /// <summary>The printer settings the page setup applies to; <c>null</c> uses those of <see cref="PageSettings"/>.</summary>
        public PrinterSettings PrinterSettings { get; set; }
    }
}
