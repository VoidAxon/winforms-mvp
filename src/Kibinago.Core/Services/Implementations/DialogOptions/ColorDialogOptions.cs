using System.Drawing;

namespace Kibinago.Services.Implementations.DialogOptions
{
    /// <summary>
    /// Option settings for <see cref="IDialogProvider.ShowColorDialog"/>.
    /// </summary>
    public class ColorDialogOptions
    {
        /// <summary>Color selected when the dialog opens; <c>null</c> = black.</summary>
        public Color? InitialColor { get; set; }
        /// <summary>Whether the user can expand the dialog to define custom colors. Defaults to <c>true</c>.</summary>
        public bool AllowFullOpen { get; set; } = true;
        /// <summary>Whether the dialog offers every available color in the basic set. Defaults to <c>false</c>.</summary>
        public bool AnyColor { get; set; } = false;
    }
}
