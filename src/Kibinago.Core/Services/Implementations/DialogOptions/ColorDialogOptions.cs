using System.Drawing;

namespace Kibinago.Services.Implementations.DialogOptions
{
    public class ColorDialogOptions
    {
        public Color? InitialColor { get; set; }
        public bool AllowFullOpen { get; set; } = true;
        public bool AnyColor { get; set; } = false;
    }
}
