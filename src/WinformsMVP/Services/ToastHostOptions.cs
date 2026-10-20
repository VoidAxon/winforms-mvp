using System.Drawing;
using WinformsMVP.Common;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Defaults for the toast service (<see cref="Implementations.WinFormsToastService"/>), set once
    /// at startup through <c>mvp.Toasts</c> (or by registering an instance). Individual calls can
    /// override the per-toast appearance via <see cref="ToastOptions"/>.
    /// </summary>
    /// <remarks>
    /// The appearance values (<see cref="Position"/>, <see cref="Size"/>, <see cref="Font"/>,
    /// <see cref="Duration"/>) are also overridable per call. The stacking-policy values
    /// (<see cref="Margin"/>, <see cref="Gap"/>, <see cref="MaxVisibleToasts"/>,
    /// <see cref="Opacity"/>) apply to the whole stack and are configured here only.
    /// The service validates and copies these values when it is created; later edits to this
    /// object have no effect. The app-wide custom painter is a separate service
    /// (<see cref="ToastRenderer"/>, <c>mvp.Toasts.UseRenderer</c>).
    /// </remarks>
    public sealed class ToastHostOptions
    {
        /// <summary>Screen corner toasts appear in. Default: bottom-right.</summary>
        public ToastPosition Position { get; set; } = ToastPosition.BottomRight;

        /// <summary>Toast size in pixels. Default: 350 x 80. When <see cref="AutoHeight"/> is on,
        /// only the width is used and the height is computed from the content.</summary>
        public Size Size { get; set; } = new Size(350, 80);

        /// <summary>Whether toasts size their height to content by default. Default: <c>false</c>
        /// (fixed <see cref="Size"/> height).</summary>
        public bool AutoHeight { get; set; } = false;

        /// <summary>Floor for the auto-sized height in pixels. Default: 40.</summary>
        public int MinHeight { get; set; } = 40;

        /// <summary>Cap for the auto-sized height in pixels. Default: 220.</summary>
        public int MaxHeight { get; set; } = 220;

        /// <summary>Message font. Default: <c>null</c> = Segoe UI 10pt, created and disposed by the service.
        /// A font set here belongs to the caller and must outlive the service.</summary>
        public Font Font { get; set; }

        /// <summary>Milliseconds a toast stays fully visible before fading. Default: 3000.</summary>
        public int Duration { get; set; } = 3000;

        /// <summary>Gap in pixels between the toast stack and the screen edge. Default: 20.</summary>
        public int Margin { get; set; } = 20;

        /// <summary>Vertical gap in pixels between stacked toasts. Default: 10.</summary>
        public int Gap { get; set; } = 10;

        /// <summary>Maximum toasts shown at once; older ones are evicted immediately. Default: 5.</summary>
        public int MaxVisibleToasts { get; set; } = 5;

        /// <summary>Opacity while visible, 0..1. Default: 0.95.</summary>
        public double Opacity { get; set; } = 0.95;

        /// <summary>App-wide toast style. Default: <see cref="ToastStyle.Soft"/>.</summary>
        public ToastStyle Style { get; set; } = ToastStyle.Soft;

        /// <summary>Whether toasts show a close glyph by default. Default: <c>true</c>.</summary>
        public bool ShowCloseButton { get; set; } = true;

        internal ToastHostOptions Copy() => (ToastHostOptions)MemberwiseClone();

        /// <summary>Throws <see cref="System.ArgumentException"/> when a value cannot work.</summary>
        internal void Validate()
        {
            if (Size.Width <= 0 || Size.Height <= 0) throw Invalid(nameof(Size), "width and height must be positive");
            if (Duration <= 0) throw Invalid(nameof(Duration), "must be positive");
            if (MinHeight <= 0) throw Invalid(nameof(MinHeight), "must be positive");
            if (MaxHeight < MinHeight) throw Invalid(nameof(MaxHeight), "must be at least MinHeight");
            if (Margin < 0) throw Invalid(nameof(Margin), "must not be negative");
            if (Gap < 0) throw Invalid(nameof(Gap), "must not be negative");
            if (MaxVisibleToasts < 1) throw Invalid(nameof(MaxVisibleToasts), "must be at least 1");
            if (!(Opacity > 0 && Opacity <= 1)) throw Invalid(nameof(Opacity), "must be greater than 0 and at most 1");
        }

        private static System.ArgumentException Invalid(string property, string rule)
            => new System.ArgumentException("ToastHostOptions." + property + " " + rule + ".", property);
    }
}
