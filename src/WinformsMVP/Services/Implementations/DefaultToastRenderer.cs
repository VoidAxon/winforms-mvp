using System.Drawing;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// The built-in toast appearance: a solid color background (by type), an icon in the left
    /// gutter, the message in the middle, and a close glyph top-right. Long text is truncated with
    /// an ellipsis.
    /// </summary>
    /// <remarks>
    /// Subclass and override <see cref="GetBackgroundColor"/> / <see cref="GetIcon"/> to recolor or
    /// re-icon without rewriting the layout. For a fully custom look, subclass
    /// <see cref="ToastRenderer"/> directly instead.
    /// </remarks>
    public class DefaultToastRenderer : ToastRenderer
    {
        public override void Render(ToastRenderContext context)
        {
            Graphics g = context.Graphics;
            int width = context.Bounds.Width;
            int height = context.Bounds.Height;
            Font font = context.Font;

            // Square (CornerRadius 0) → the framework paints this renderer onto an opaque window DC,
            // so the default (system) text hint gives crisp ClearType. No TextRenderingHint override.
            g.Clear(GetBackgroundColor(context.Type));

            // Icon scales with the message font (10pt text -> 18pt icon). context.Font is shared
            // and must NOT be disposed here, so it is used directly rather than in a using block.
            using (var iconFont = new Font(font.FontFamily, font.Size * 1.8f, FontStyle.Bold))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var leftMiddle = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter, // long text gets an ellipsis instead of a hard cut
                // Lay out only whole lines so a centered multi-line block is never clipped at top/bottom.
                FormatFlags = StringFormatFlags.LineLimit
            })
            {
                // Icon (left gutter)
                g.DrawString(GetIcon(context.Type), iconFont, Brushes.White, new RectangleF(10, 10, 40, height - 20), centered);
                // Message (fills the middle; reclaims the close-glyph gutter when it is hidden)
                int messageRight = context.ShowCloseButton ? width - 30 : width - 20;
                g.DrawString(context.Message, font, Brushes.White, new RectangleF(60, 10, messageRight - 60, height - 20), leftMiddle);
                // Close glyph (top-right corner) — only when requested
                if (context.ShowCloseButton)
                {
                    using (var closeFont = new Font(font.FontFamily, font.Size, FontStyle.Bold))
                    {
                        g.DrawString("✖", closeFont, Brushes.White, new RectangleF(width - 30, 5, 20, 20), centered);
                    }
                }
            }
        }

        /// <summary>The background color for a toast kind. Override to recolor.</summary>
        protected virtual Color GetBackgroundColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return Color.FromArgb(16, 137, 62); // Green
                case ToastType.Warning:
                    return Color.FromArgb(255, 140, 0); // Orange
                case ToastType.Error:
                    return Color.FromArgb(232, 17, 35); // Red
                case ToastType.Info:
                default:
                    return Color.FromArgb(0, 120, 215); // Blue
            }
        }

        /// <summary>The icon glyph for a toast kind. Override to re-icon.</summary>
        protected virtual string GetIcon(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return "✓"; // check mark
                case ToastType.Warning:
                    return "⚠"; // warning sign
                case ToastType.Error:
                    return "✖"; // heavy multiplication x
                case ToastType.Info:
                default:
                    return "ℹ"; // information source
            }
        }
    }
}
