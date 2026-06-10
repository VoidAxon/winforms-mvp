using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Application-wide defaults for toast notifications. Set these once at startup; individual
    /// calls can override the per-toast appearance via <see cref="ToastOptions"/>.
    /// </summary>
    /// <remarks>
    /// The appearance values (<see cref="Position"/>, <see cref="Size"/>, <see cref="Font"/>,
    /// <see cref="Duration"/>) are also overridable per call. The stacking-policy values
    /// (<see cref="Margin"/>, <see cref="Gap"/>, <see cref="MaxVisibleToasts"/>,
    /// <see cref="Opacity"/>) apply to the whole stack and are configured here only.
    /// </remarks>
    public static class ToastDefaults
    {
        /// <summary>Screen corner toasts appear in. Default: bottom-right.</summary>
        public static ToastPosition Position { get; set; } = ToastPosition.BottomRight;

        /// <summary>Toast size in pixels. Default: 350 x 80.</summary>
        public static Size Size { get; set; } = new Size(350, 80);

        /// <summary>Message font. Default: Segoe UI 10pt. Reused across toasts; never disposed by them.</summary>
        public static Font Font { get; set; } = new Font("Segoe UI", 10f);

        /// <summary>Milliseconds a toast stays fully visible before fading. Default: 3000.</summary>
        public static int Duration { get; set; } = 3000;

        /// <summary>Gap in pixels between the toast stack and the screen edge. Default: 20.</summary>
        public static int Margin { get; set; } = 20;

        /// <summary>Vertical gap in pixels between stacked toasts. Default: 10.</summary>
        public static int Gap { get; set; } = 10;

        /// <summary>Maximum toasts shown at once; older ones are evicted immediately. Default: 5.</summary>
        public static int MaxVisibleToasts { get; set; } = 5;

        /// <summary>Opacity while visible, 0..1. Default: 0.95.</summary>
        public static double Opacity { get; set; } = 0.95;

        /// <summary>App-wide toast style. Default: <see cref="ToastStyle.Default"/>.</summary>
        public static ToastStyle Style { get; set; } = ToastStyle.Default;

        /// <summary>Whether toasts show a close glyph by default. Default: <c>true</c>.</summary>
        public static bool ShowCloseButton { get; set; } = true;

        /// <summary>
        /// App-wide custom painter for toasts. Default: <c>null</c>, meaning "no custom override —
        /// resolve the painter from <see cref="Style"/>." Set it to take over every toast's
        /// appearance, or override per toast via <see cref="ToastOptions.Renderer"/>. A non-null
        /// value here wins over <see cref="Style"/> but loses to a per-toast renderer/style.
        /// </summary>
        public static ToastRenderer Renderer { get; set; }
    }
}
