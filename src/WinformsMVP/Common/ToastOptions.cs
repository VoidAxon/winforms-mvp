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

        /// <summary>Toast size in pixels. <c>null</c> = use the default.</summary>
        public Size? Size { get; set; }

        /// <summary>Message font. <c>null</c> = use the default.</summary>
        public Font Font { get; set; }

        /// <summary>Milliseconds to stay fully visible before fading out. <c>null</c> = use the default.</summary>
        public int? Duration { get; set; }

        /// <summary>
        /// Custom painter for this toast. <c>null</c> = use <c>ToastDefaults.Renderer</c>. Set it to
        /// take over the toast's appearance (the framework still owns position, size, and lifecycle).
        /// </summary>
        public ToastRenderer Renderer { get; set; }
    }
}
