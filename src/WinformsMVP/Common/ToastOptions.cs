using System.Drawing;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Per-call appearance overrides for a single toast notification. Any property left
    /// <c>null</c> falls back to the matching value in <c>WinformsMVP.Services.ToastDefaults</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Font"/> is owned by the caller — the toast never disposes it — so a font passed
    /// here (or set on <c>ToastDefaults</c>) can be safely reused across many toasts.
    /// </remarks>
    public class ToastOptions
    {
        /// <summary>Screen corner the toast appears in. <c>null</c> = use the default.</summary>
        public ToastPosition? Position { get; set; }

        /// <summary>Toast size in pixels. <c>null</c> = use the default. When <see cref="AutoHeight"/>
        /// is on, only the width is used and the height is computed from the content.</summary>
        public Size? Size { get; set; }

        /// <summary>
        /// When <c>true</c>, the toast height is sized to its content (width stays fixed): a single
        /// line is compact and multi-line text grows, clamped to <see cref="MinHeight"/>..
        /// <see cref="MaxHeight"/>. <c>null</c> = use <c>ToastDefaults.AutoHeight</c>.
        /// </summary>
        public bool? AutoHeight { get; set; }

        /// <summary>Floor for the auto-sized height in pixels. <c>null</c> = use <c>ToastDefaults.MinHeight</c>.</summary>
        public int? MinHeight { get; set; }

        /// <summary>Cap for the auto-sized height in pixels (text beyond it is ellipsized).
        /// <c>null</c> = use <c>ToastDefaults.MaxHeight</c>.</summary>
        public int? MaxHeight { get; set; }

        /// <summary>Message font. <c>null</c> = use the default.</summary>
        public Font Font { get; set; }

        /// <summary>Milliseconds to stay fully visible before fading out. <c>null</c> = use the default.</summary>
        public int? Duration { get; set; }

        /// <summary>
        /// Custom painter for this toast. <c>null</c> = use <c>ToastDefaults.Renderer</c>. Set it to
        /// take over the toast's appearance (the framework still owns position, size, and lifecycle).
        /// </summary>
        public ToastRenderer Renderer { get; set; }

        /// <summary>
        /// Built-in style for this toast. <c>null</c> = use <c>ToastDefaults.Style</c>. Ignored when
        /// <see cref="Renderer"/> is set (a custom renderer always wins).
        /// </summary>
        public ToastStyle? Style { get; set; }

        /// <summary>
        /// Whether this toast draws a close glyph. <c>null</c> = use <c>ToastDefaults.ShowCloseButton</c>.
        /// Display only — clicking anywhere on the toast still dismisses it regardless.
        /// </summary>
        public bool? ShowCloseButton { get; set; }
    }
}
