using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Kibinago.Services
{
    /// <summary>
    /// Settings of the WinForms busy indicator (<c>mvp.Busy</c>). The overlay itself is chosen with
    /// <c>mvp.Busy.UseOverlay(...)</c>; without one, a busy view shows only the wait cursor.
    /// </summary>
    public sealed class BusyOptions
    {
        /// <summary>
        /// Milliseconds before the overlay appears, so quick work does not flash (default 150). The wait cursor
        /// and the input block start at once. A UI thread blocked by synchronous work cannot run this timer:
        /// such work shows the cursor only.
        /// </summary>
        public int Delay { get; set; } = 150;

        internal BusyOptions Copy() => new BusyOptions { Delay = Delay };

        internal void Validate()
        {
            if (Delay < 0) throw new ArgumentOutOfRangeException(nameof(Delay), Delay, "Delay must be 0 or more milliseconds.");
        }
    }

    /// <summary>
    /// Paints the busy overlay. The overlay is two windows of its own laid over the view (never controls of
    /// the view): a backdrop covering the view, painted when its size changes, and a small indicator,
    /// repainted about 25 times a second. Both paint onto transparent surfaces with per-pixel alpha, so
    /// translucent colours show the view behind. Derive from it for a look of your own and pass it to
    /// <c>mvp.Busy.UseOverlay(renderer)</c>; <see cref="HudBusyRenderer"/> is the built-in one.
    /// </summary>
    public abstract class BusyRenderer
    {
        /// <summary>Whether <see cref="PaintBackdrop"/> paints anything (no backdrop window otherwise).</summary>
        public virtual bool HasBackdrop => false;

        /// <summary>Paints the backdrop over the whole view (its client area); called when its size changes.</summary>
        /// <param name="context">The surface (the view's size) and what to paint with.</param>
        public virtual void PaintBackdrop(BusyPaintContext context) { }

        /// <summary>
        /// Where the indicator goes, in the view's client coordinates (<see cref="Rectangle.Empty"/>: none).
        /// Called when the view's size or the message changes.
        /// </summary>
        /// <param name="context">The view's size, DPI scale, font and message.</param>
        /// <returns>The indicator's rectangle.</returns>
        public abstract Rectangle GetIndicatorBounds(BusyLayoutContext context);

        /// <summary>Paints one frame of the indicator onto its surface (the size <see cref="GetIndicatorBounds"/> gave).</summary>
        /// <param name="context">The surface, the elapsed time to animate from, and what to paint with.</param>
        public abstract void PaintIndicator(BusyPaintContext context);
    }

    /// <summary>What a <see cref="BusyRenderer"/> lays out with.</summary>
    public sealed class BusyLayoutContext
    {
        internal BusyLayoutContext(Size viewSize, float scale, Font font, string message)
        {
            ViewSize = viewSize; Scale = scale; Font = font; Message = message;
        }

        /// <summary>The view's client size in pixels.</summary>
        public Size ViewSize { get; }

        /// <summary>DPI scale of the view (1.0 at 96 dpi): multiply pixel sizes by it.</summary>
        public float Scale { get; }

        /// <summary>The view's font.</summary>
        public Font Font { get; }

        /// <summary>The text given to <c>ShowBusy</c>, or <c>null</c>.</summary>
        public string Message { get; }
    }

    /// <summary>What a <see cref="BusyRenderer"/> paints with.</summary>
    public sealed class BusyPaintContext
    {
        internal BusyPaintContext(Graphics graphics, Rectangle bounds, TimeSpan elapsed, float scale, Font font, string message)
        {
            Graphics = graphics; Bounds = bounds; Elapsed = elapsed; Scale = scale; Font = font; Message = message;
        }

        /// <summary>Draws onto a transparent surface, cleared before each paint.</summary>
        public Graphics Graphics { get; }

        /// <summary>The whole surface, from (0, 0).</summary>
        public Rectangle Bounds { get; }

        /// <summary>Time since the overlay appeared: animate from it, so a late timer does not slow the animation.</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>DPI scale of the view (1.0 at 96 dpi).</summary>
        public float Scale { get; }

        /// <summary>The view's font.</summary>
        public Font Font { get; }

        /// <summary>The text given to <c>ShowBusy</c>, or <c>null</c>.</summary>
        public string Message { get; }

        /// <summary>The system highlight colour.</summary>
        public Color Accent => SystemColors.Highlight;

        /// <summary>
        /// Draws text without breaking the surface's transparency (GDI text — <c>TextRenderer</c> — would):
        /// wrapped inside <paramref name="box"/>, at most <paramref name="maxLines"/> lines, the rest ended with "…".
        /// </summary>
        /// <param name="text">The text; nothing is drawn when it is empty.</param>
        /// <param name="box">Where the text goes.</param>
        /// <param name="color">The text colour.</param>
        /// <param name="maxLines">The most lines shown.</param>
        /// <param name="alignment">The horizontal alignment.</param>
        public void DrawText(string text, RectangleF box, Color color, int maxLines = 2, StringAlignment alignment = StringAlignment.Center)
        {
            if (string.IsNullOrEmpty(text) || box.Width <= 0 || box.Height <= 0) return;
            var lineHeight = Font.GetHeight(Graphics);
            var lines = Math.Max(1, Math.Min(maxLines, (int)(box.Height / lineHeight)));
            var clipped = new RectangleF(box.X, box.Y, box.Width, lines * lineHeight + 1);
            using (var format = new StringFormat { Alignment = alignment, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
            using (var brush = new SolidBrush(color))
            {
                if (lines == 1) format.FormatFlags |= StringFormatFlags.NoWrap;
                var hint = Graphics.TextRenderingHint;
                Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                Graphics.DrawString(text, Font, brush, clipped, format);
                Graphics.TextRenderingHint = hint;
            }
        }
    }

    /// <summary>
    /// The built-in overlay (<c>mvp.Busy.UseOverlay()</c>): the view washed out with <see cref="Backdrop"/>
    /// (white by default) and, in its middle, a white rounded square with a ring like the Windows 11
    /// ProgressRing (an arc that grows and shrinks while it turns). It shows no text; derive from
    /// <see cref="BusyRenderer"/> to show the message.
    /// </summary>
    public class HudBusyRenderer : BusyRenderer
    {
        /// <summary>
        /// The color laid over the whole view, its alpha the opacity (default white at 115 of 255). A fully
        /// transparent color (<see cref="Color.Transparent"/>, alpha 0) means no backdrop: only the square
        /// shows and no window covers the rest of the view. Set it before the renderer is used.
        /// </summary>
        public Color Backdrop { get; set; } = Color.FromArgb(115, 255, 255, 255);

        /// <inheritdoc />
        public override bool HasBackdrop => Backdrop.A > 0;

        /// <summary>The square's size at 96 dpi (default 72); smaller in a small view.</summary>
        public int Size { get; set; } = 72;

        /// <inheritdoc />
        public override void PaintBackdrop(BusyPaintContext context)
        {
            using (var wash = new SolidBrush(Backdrop)) context.Graphics.FillRectangle(wash, context.Bounds);
        }

        /// <inheritdoc />
        public override Rectangle GetIndicatorBounds(BusyLayoutContext context)
        {
            var s = context.Scale;
            var size = Math.Min(Size * s, Math.Min(context.ViewSize.Width, context.ViewSize.Height) - 16 * s);
            if (size < 20 * s) return Rectangle.Empty;   // no room for it
            var margin = Margin(s);
            var outer = (int)Math.Ceiling(size + 2 * margin);
            return new Rectangle((context.ViewSize.Width - outer) / 2, (context.ViewSize.Height - outer) / 2, outer, outer);
        }

        /// <inheritdoc />
        public override void PaintIndicator(BusyPaintContext context)
        {
            var g = context.Graphics;
            var s = context.Scale;
            var margin = Margin(s);
            var box = new RectangleF(margin, margin, context.Bounds.Width - 2 * margin, context.Bounds.Height - 2 * margin);
            var radius = Math.Min(12 * s, box.Width / 4);

            for (var i = 3; i >= 1; i--)   // a soft shadow: widening, fading layers, a little lower
            {
                var spread = i * 1.5f * s;
                using (var shade = new SolidBrush(Color.FromArgb(9 + 7 * (4 - i), 0, 0, 0)))
                using (var path = Rounded(new RectangleF(box.X - spread, box.Y - spread + 2 * s, box.Width + 2 * spread, box.Height + 2 * spread), radius + spread))
                    g.FillPath(shade, path);
            }
            using (var fill = new SolidBrush(Color.White))
            using (var path = Rounded(box, radius))
                g.FillPath(fill, path);

            var ring = box.Width * 0.44f;
            var ringBox = new RectangleF(box.X + (box.Width - ring) / 2, box.Y + (box.Height - ring) / 2, ring, ring);
            var (start, sweep) = Arc(context.Elapsed.TotalMilliseconds);
            using (var pen = new Pen(context.Accent, Math.Max(2f, ring * 0.12f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, ringBox, start, sweep);
        }

        private static float Margin(float scale) => 8 * scale;   // room for the shadow

        // The head runs ahead while the arc grows (0.8 s), then the tail catches up while it shrinks (0.8 s);
        // the ring turns once every 1.3 s.
        internal static (float Start, float Sweep) Arc(double ms)
        {
            const double Cycle = 1600, Max = 250, Min = 4;
            var turns = ms / 1300 * 360;
            var n = Math.Floor(ms / Cycle);
            var p = (ms - n * Cycle) / Cycle;
            double sweep, tail;
            if (p < 0.5) { sweep = Min + (Max - Min) * Ease(p * 2); tail = 0; }
            else { var k = Ease((p - 0.5) * 2); sweep = Max - (Max - Min) * k; tail = (Max - Min) * k; }
            return ((float)((turns + n * (Max - Min) + tail - 90) % 360), (float)sweep);
        }

        private static double Ease(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var d = Math.Max(1f, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
