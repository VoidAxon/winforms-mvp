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
        /// Corner radius in pixels the toast window should be rounded to. <c>0</c> (the default)
        /// means square. The framework reads this from the resolved renderer and applies a rounded
        /// window region, so the shape travels with the renderer — custom renderers can round
        /// themselves by overriding this.
        /// </summary>
        public virtual int CornerRadius
        {
            get { return 0; }
        }

        /// <summary>Paints the toast onto the surface described by <paramref name="context"/>.</summary>
        public abstract void Render(ToastRenderContext context);
    }

    /// <summary>
    /// The surface and data handed to a <see cref="ToastRenderer"/> for one paint pass.
    /// </summary>
    public sealed class ToastRenderContext
    {
        internal ToastRenderContext(Graphics graphics, Rectangle bounds, string message, ToastType type, Font font)
        {
            Graphics = graphics;
            Bounds = bounds;
            Message = message;
            Type = type;
            Font = font;
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
    }
}
