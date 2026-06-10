using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A soft, light-tinted toast: a pale background colored by type, a filled-circle icon in the
    /// left gutter, dark left-aligned message text, and a colored close glyph. Rounded corners.
    /// </summary>
    /// <remarks>Override the <c>GetXxx</c> hooks to recolor or re-icon without rewriting the layout.</remarks>
    public class SoftToastRenderer : ToastRenderer
    {
        // Layout metrics shared by Render and MeasureHeight so the measured height fits the painted
        // content exactly. The icon diameter is font-based (not height-based) so it stays well-sized
        // even when the toast is compact (single line).
        private const int IconLeft = 16;
        private const int TextGap = 12;     // gap between the icon and the message
        private const int CloseGutter = 32; // right inset when the close glyph is shown
        private const int RightPad = 16;    // right inset when it is hidden
        private const int VerticalPad = 10; // top and bottom inset of the message

        /// <summary>Rounded corners for the soft style.</summary>
        public override int CornerRadius
        {
            get { return 10; }
        }

        public override void Render(ToastRenderContext context)
        {
            Graphics g = context.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Rounded (CornerRadius > 0) → the framework composites this with per-pixel alpha, where
            // ClearType cannot work. GDI+ AntiAlias (grayscale) is the correct, alpha-safe choice.
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            int width = context.Bounds.Width;
            int height = context.Bounds.Height;
            Font font = context.Font;

            Color tint = GetTintColor(context.Type);
            Color accent = GetAccentColor(context.Type);

            // Fill a rounded background; the area outside the path stays transparent so the
            // per-pixel-alpha layered window shows smooth, anti-aliased corners.
            using (var bgBrush = new SolidBrush(tint))
            using (var bgPath = ToastDrawing.RoundedRectangle(new Rectangle(0, 0, width - 1, height - 1), context.CornerRadius))
            {
                g.FillPath(bgBrush, bgPath);
            }

            // Anti-aliased rounded border on the same path crisps up the edge.
            using (var pen = new Pen(accent, 1.5f))
            using (var border = ToastDrawing.RoundedRectangle(new Rectangle(0, 0, width - 1, height - 1), context.CornerRadius))
            {
                g.DrawPath(pen, border);
            }

            int diameter = IconDiameter(font);
            var circle = new Rectangle(IconLeft, (height - diameter) / 2, diameter, diameter);

            int textLeft = TextLeft(font);
            int textRight = TextRight(width, context.ShowCloseButton);

            using (var iconFont = new Font(font.FontFamily, font.Size * 1.3f, FontStyle.Bold))
            using (var accentBrush = new SolidBrush(accent))
            using (var textBrush = new SolidBrush(GetTextColor()))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var message = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit })
            {
                g.FillEllipse(accentBrush, circle);
                g.DrawString(GetIcon(context.Type), iconFont, Brushes.White, circle, centered);

                g.DrawString(context.Message, font, textBrush, new RectangleF(textLeft, VerticalPad, textRight - textLeft, height - 2 * VerticalPad), message);

                if (context.ShowCloseButton)
                {
                    using (var closeFont = new Font(font.FontFamily, font.Size, FontStyle.Bold))
                    {
                        g.DrawString("✖", closeFont, accentBrush, new RectangleF(width - 28, 6, 20, 20), centered);
                    }
                }
            }
        }

        /// <summary>Mirrors <see cref="Render"/>'s layout: message wrapped in the same text column,
        /// floored at the icon diameter, plus the top and bottom padding.</summary>
        public override int MeasureHeight(ToastMeasureContext context)
        {
            Font font = context.Font;
            int textWidth = TextRight(context.Width, context.ShowCloseButton) - TextLeft(font);
            if (textWidth < 1) textWidth = 1;

            float textHeight = context.Graphics.MeasureString(context.Message ?? string.Empty, font, textWidth).Height;
            int content = (int)Math.Ceiling(Math.Max(textHeight, IconDiameter(font)));
            return content + VerticalPad * 2;
        }

        /// <summary>Filled-circle icon diameter, scaled from the message font so it stays well-sized
        /// even on a compact (single-line) toast.</summary>
        private static int IconDiameter(Font font)
        {
            int d = (int)Math.Round(font.Size * 2.2f);
            if (d < 18) d = 18;
            if (d > 40) d = 40;
            return d;
        }

        private static int TextLeft(Font font)
        {
            return IconLeft + IconDiameter(font) + TextGap;
        }

        private static int TextRight(int width, bool showCloseButton)
        {
            return showCloseButton ? width - CloseGutter : width - RightPad;
        }

        /// <summary>The pale background color for a toast kind. Override to recolor.</summary>
        protected virtual Color GetTintColor(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success: return Color.FromArgb(222, 243, 228);
                case ToastType.Warning: return Color.FromArgb(251, 243, 213);
                case ToastType.Error: return Color.FromArgb(251, 224, 225);
                case ToastType.Info:
                default: return Color.FromArgb(220, 235, 251);
            }
        }

        /// <summary>The strong accent color (icon circle, border, close glyph). Override to recolor.</summary>
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

        /// <summary>The message text color. Override to recolor.</summary>
        protected virtual Color GetTextColor()
        {
            return Color.FromArgb(64, 64, 64);
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
