using System.Drawing.Printing;

namespace Kibinago.Services.Implementations.DialogOptions
{
    public class PrintPreviewDialogOptions
    {
        public PrintDocument Document { get; set; }
        public string Title { get; set; } = "Print Preview";
    }
}
