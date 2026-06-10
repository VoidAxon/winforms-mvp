using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A white card toast: a colored rounded accent bar on the left, a filled-circle icon, dark
    /// left-aligned message text, and a gray close glyph. Rounded corners; no shadow.
    /// </summary>
    /// <remarks>Override the <c>GetXxx</c> hooks to recolor or re-icon without rewriting the layout.</remarks>
    public class CardToastRenderer : ToastRenderer
    {
        /// <summary>Rounded corners for the card style.</summary>
        public override int CornerRadius
        {
            get { return 10; }
        }

        public override void Render(ToastRenderContext context)
        {
            Graphics g = context.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int width = context.Bounds.Width;
            int height = context.Bounds.Height;
            Font font = context.Font;

            Color accent = GetAccentColor(context.Type);

            g.Clear(GetBackgroundColor());

            // Subtle rounded border in place of a shadow.
            using (var pen = new Pen(GetBorderColor(), 1f))
            using (var border = ToastDrawing.RoundedRectangle(new Rectangle(0, 0, width - 1, height - 1), context.CornerRadius))
            {
                g.DrawPath(pen, border);
            }

            int diameter = height - 28;
            if (diameter > 36) diameter = 36;
            if (diameter < 16) diameter = 16;
            var circle = new Rectangle(20, (height - diameter) / 2, diameter, diameter);

            int textLeft = circle.Right + 12;
            int textRight = context.ShowCloseButton ? width - 32 : width - 16;

            using (var iconFont = new Font(font.FontFamily, font.Size * 1.3f, FontStyle.Bold))
            using (var accentBrush = new SolidBrush(accent))
            using (var textBrush = new SolidBrush(GetTextColor()))
            using (var closeBrush = new SolidBrush(GetCloseColor()))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var message = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                // Left accent bar (rounded, inset vertically).
                using (var bar = ToastDrawing.RoundedRectangle(new Rectangle(6, 10, 6, height - 20), 3))
                {
                    g.FillPath(accentBrush, bar);
                }

                g.FillEllipse(accentBrush, circle);
                g.DrawString(GetIcon(context.Type), iconFont, Brushes.White, circle, centered);

                g.DrawString(context.Message, font, textBrush, new RectangleF(textLeft, 8, textRight - textLeft, height - 16), message);

                if (context.ShowCloseButton)
                {
                    using (var closeFont = new Font(font.FontFamily, font.Size, FontStyle.Bold))
                    {
                        g.DrawString("✖", closeFont, closeBrush, new RectangleF(width - 28, 6, 20, 20), centered);
                    }
                }
            }
        }

        /// <summary>The card background color. Override to recolor.</summary>
        protected virtual Color GetBackgroundColor()
        {
            return Color.White;
        }

        /// <summary>The card border color. Override to recolor.</summary>
        protected virtual Color GetBorderColor()
        {
            return Color.FromArgb(230, 230, 230);
        }

        /// <summary>The message text color. Override to recolor.</summary>
        protected virtual Color GetTextColor()
        {
            return Color.FromArgb(64, 64, 64);
        }

        /// <summary>The close glyph color. Override to recolor.</summary>
        protected virtual Color GetCloseColor()
        {
            return Color.FromArgb(153, 153, 153);
        }

        /// <summary>The strong accent color (icon circle, accent bar). Override to recolor.</summary>
        protected virtual Color GetAccentColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(16, 137, 62);
                case ToastType.Warning: return Color.FromArgb(255, 140, 0);
                case ToastType.Error: return Color.FromArgb(232, 17, 35);
                case ToastType.Info:
                default: return Color.FromArgb(0, 120, 215);
            }
        }

        /// <summary>The icon glyph for a toast kind. Override to re-icon.</summary>
        protected virtual string GetIcon(ToastType type)
        {
            // Plain ASCII glyphs read more cleanly than symbol glyphs inside a small filled circle.
            switch (type)
            {
                case ToastType.Success: return "✓";
                case ToastType.Warning: return "!";
                case ToastType.Error: return "✕";
                case ToastType.Info:
                default: return "i";
            }
        }
    }
}
