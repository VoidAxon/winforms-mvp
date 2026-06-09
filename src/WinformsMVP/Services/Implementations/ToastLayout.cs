using System.Collections.Generic;
using System.Drawing;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// One toast's footprint as seen by the layout calculator: where it wants to live and how big
    /// it is. Deliberately free of any window/Win32 reference so layout can be unit-tested.
    /// </summary>
    internal struct ToastBox
    {
        public ToastPosition Position;
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Pure stacking math for toast notifications: given each live toast's corner and size, work
    /// out the top-left point it should occupy so toasts in the same corner stack without
    /// overlapping. No dependency on <c>Screen</c>, <c>SetWindowPos</c>, or any window — the
    /// caller supplies the working area and applies the results.
    /// </summary>
    internal static class ToastLayout
    {
        /// <summary>
        /// Computes the top-left point for each box, index-aligned to <paramref name="boxes"/>.
        /// </summary>
        /// <param name="boxes">Live toasts in oldest-first order. Within each corner the newest
        /// (last) hugs the screen edge and older ones march toward the center.</param>
        /// <param name="area">The working area to lay out within (e.g. the screen minus the taskbar).</param>
        /// <param name="margin">Gap in pixels between the stack and the screen edge.</param>
        /// <param name="gap">Vertical gap in pixels between stacked toasts.</param>
        public static Point[] Arrange(IList<ToastBox> boxes, Rectangle area, int margin, int gap)
        {
            var result = new Point[boxes.Count];

            // Distance already consumed from the edge, tracked per corner.
            var consumed = new Dictionary<ToastPosition, int>();

            // Walk newest-first so the most recent toast in each corner sits closest to the edge.
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                var box = boxes[i];

                int used;
                consumed.TryGetValue(box.Position, out used);

                bool isRight = box.Position == ToastPosition.TopRight || box.Position == ToastPosition.BottomRight;
                bool isBottom = box.Position == ToastPosition.BottomLeft || box.Position == ToastPosition.BottomRight;

                int x = isRight ? area.Right - box.Width - margin : area.Left + margin;
                int y = isBottom
                    ? area.Bottom - margin - used - box.Height
                    : area.Top + margin + used;

                result[i] = new Point(x, y);
                consumed[box.Position] = used + box.Height + gap;
            }

            return result;
        }

        /// <summary>
        /// Places a single anchored toast near <paramref name="anchor"/> like a tooltip: it extends
        /// down-and-right from the anchor by default, flips to the other side of the anchor if that
        /// would overflow, and is finally clamped so the whole toast stays inside
        /// <paramref name="area"/>. The clamp makes the result safe for any anchor — even one off
        /// the screen or negative.
        /// </summary>
        /// <param name="anchor">Desired screen point to anchor to (e.g. a cursor or control location).</param>
        /// <param name="size">The toast size in pixels.</param>
        /// <param name="area">The working area the toast must stay fully within.</param>
        /// <param name="margin">Minimum gap in pixels from the area edges.</param>
        public static Point Anchor(Point anchor, Size size, Rectangle area, int margin)
        {
            // Default: the toast extends right from the anchor; flip left if it would overflow.
            int x = anchor.X;
            if (x + size.Width > area.Right - margin)
            {
                x = anchor.X - size.Width;
            }

            // Default: the toast extends down from the anchor; flip up if it would overflow.
            int y = anchor.Y;
            if (y + size.Height > area.Bottom - margin)
            {
                y = anchor.Y - size.Height;
            }

            // Final safety clamp: guarantee full visibility regardless of the anchor given.
            x = Clamp(x, area.Left + margin, area.Right - margin - size.Width);
            y = Clamp(y, area.Top + margin, area.Bottom - margin - size.Height);

            return new Point(x, y);
        }

        private static int Clamp(int value, int min, int max)
        {
            // If the toast is larger than the area, min wins so the top-left stays visible.
            if (max < min)
            {
                max = min;
            }

            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
