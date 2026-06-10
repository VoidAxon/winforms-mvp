using System.Drawing;

namespace WinformsMVP.Common
{
    /// <summary>
    /// Draws the content of a toast notification. Assign a custom renderer via
    /// <see cref="ToastOptions.Renderer"/> (per toast) or <c>ToastDefaults.Renderer</c>
    /// (app-wide) to take over a toast's appearance — the toast equivalent of
    /// <c>System.Windows.Forms.ToolStrip.Renderer</c>.
    /// </summary>
    /// <remarks>
    /// A renderer only paints the surface it is given (see <see cref="ToastRenderContext"/>). The
    /// framework still owns the window: position, size, stacking, the single-anchored slot, fade
    /// out, on-screen clamping, and staying out of <c>Application.OpenForms</c>.
    /// <para>
    /// To tweak just the colors or icons, subclass the built-in
    /// <c>DefaultToastRenderer</c> and override its hooks. For a completely custom look, subclass
    /// this type and implement <see cref="Render"/> from scratch.
    /// </para>
    /// </remarks>
    public abstract class ToastRenderer
    {
        /// <summary>
        /// Corner radius in pixels for the toast's rounded background. <c>0</c> (the default) means
        /// square. The renderer paints its background to this radius; the framework composites the
        /// toast with per-pixel alpha so the corners come out smooth (anti-aliased). The shape thus
        /// travels with the renderer — custom renderers round themselves by overriding this.
        /// </summary>
        public virtual int CornerRadius
        {
            get { return 0; }
        }

        /// <summary>Paints the toast onto the surface described by <paramref name="context"/>.</summary>
        public abstract void Render(ToastRenderContext context);

        /// <summary>
        /// Returns the height in pixels this renderer wants for the given content and fixed width,
        /// used only when a toast opts into auto-height. The framework clamps the result to the
        /// toast's min/max. Override to mirror your <see cref="Render"/> layout exactly (same text
        /// area and vertical padding) so the rendered content fits the measured height with no
        /// clipping. The default measures the message wrapped across the width with a small inset.
        /// </summary>
        public virtual int MeasureHeight(ToastMeasureContext context)
        {
            int textWidth = context.Width - 40;
            if (textWidth < 1) textWidth = 1;

            SizeF size = context.Graphics.MeasureString(context.Message ?? string.Empty, context.Font, textWidth);
            return (int)System.Math.Ceiling(size.Height) + 20;
        }
    }

    /// <summary>
    /// The surface and data handed to a <see cref="ToastRenderer"/> for one paint pass.
    /// </summary>
    public sealed class ToastRenderContext
    {
        internal ToastRenderContext(Graphics graphics, Rectangle bounds, string message, ToastType type, Font font, int cornerRadius, bool showCloseButton)
        {
            Graphics = graphics;
            Bounds = bounds;
            Message = message;
            Type = type;
            Font = font;
            CornerRadius = cornerRadius;
            ShowCloseButton = showCloseButton;
        }

        /// <summary>The GDI+ surface to draw on.</summary>
        public Graphics Graphics { get; }

        /// <summary>The toast's client rectangle (origin at 0,0).</summary>
        public Rectangle Bounds { get; }

        /// <summary>The message text.</summary>
        public string Message { get; }

        /// <summary>The toast kind (Info / Success / Warning / Error).</summary>
        public ToastType Type { get; }

        /// <summary>The resolved message font.</summary>
        public Font Font { get; }

        /// <summary>The corner radius (px) of the toast's rounded background; <c>0</c> means
        /// square. Paint the background (and any border) to this radius.</summary>
        public int CornerRadius { get; }

        /// <summary>Whether the renderer should draw a close glyph. Display only — clicking
        /// anywhere on the toast dismisses it regardless of whether the glyph is drawn.</summary>
        public bool ShowCloseButton { get; }
    }

    /// <summary>
    /// The inputs handed to <see cref="ToastRenderer.MeasureHeight"/> to compute a toast's
    /// content height. The width is fixed; the renderer returns the height it needs.
    /// </summary>
    public sealed class ToastMeasureContext
    {
        internal ToastMeasureContext(Graphics graphics, string message, ToastType type, Font font, int width, bool showCloseButton)
        {
            Graphics = graphics;
            Message = message;
            Type = type;
            Font = font;
            Width = width;
            ShowCloseButton = showCloseButton;
        }

        /// <summary>A GDI+ surface for text measurement (<see cref="Graphics.MeasureString(string, Font, int)"/>).</summary>
        public Graphics Graphics { get; }

        /// <summary>The message text.</summary>
        public string Message { get; }

        /// <summary>The toast kind (Info / Success / Warning / Error).</summary>
        public ToastType Type { get; }

        /// <summary>The resolved message font.</summary>
        public Font Font { get; }

        /// <summary>The fixed toast width in pixels.</summary>
        public int Width { get; }

        /// <summary>Whether a close glyph will be drawn (affects the available text width).</summary>
        public bool ShowCloseButton { get; }
    }
}
