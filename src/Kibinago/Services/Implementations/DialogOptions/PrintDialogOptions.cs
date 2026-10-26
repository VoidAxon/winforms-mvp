using System.Drawing.Printing;

namespace Kibinago.Services.Implementations.DialogOptions
{
    /// <summary>Settings for <see cref="IWinFormsDialogProvider.ShowPrintDialog"/>.</summary>
    public class PrintDialogOptions
    {
        /// <summary>The printer settings to edit; <c>null</c> starts from new default settings.</summary>
        public PrinterSettings PrinterSettings { get; set; }
        /// <summary>Whether the "Selection" page range is offered. Defaults to <c>false</c>.</summary>
        public bool AllowSelection { get; set; } = false;
        /// <summary>Whether the "Pages" (from/to) range is offered. Defaults to <c>true</c>.</summary>
        public bool AllowSomePages { get; set; } = true;
    }
}
