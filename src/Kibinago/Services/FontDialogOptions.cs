using System.Drawing;

namespace Kibinago.Services
{
    /// <summary>Settings for <see cref="IWinFormsDialogProvider.ShowFontDialog"/>.</summary>
    public class FontDialogOptions
    {
        /// <summary>The font selected when the dialog opens; <c>null</c> uses the system default font.</summary>
        public Font InitialFont { get; set; }
        /// <summary>Whether the dialog offers a color choice. Defaults to <c>false</c>.</summary>
        public bool ShowColor { get; set; } = false;
    }
}
