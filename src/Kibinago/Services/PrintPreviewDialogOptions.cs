using System.Drawing.Printing;

namespace Kibinago.Services
{
    /// <summary>Settings for <see cref="IWinFormsDialogProvider.ShowPrintPreviewDialog"/>.</summary>
    public class PrintPreviewDialogOptions
    {
        /// <summary>The document to preview.</summary>
        public PrintDocument Document { get; set; }
        /// <summary>The window title; <c>null</c> or empty uses <see cref="DialogProviderOptions.PrintPreviewDialogTitle"/>.</summary>
        public string Title { get; set; } = "Print Preview";
    }
}
