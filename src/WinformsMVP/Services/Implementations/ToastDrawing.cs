using System.Drawing;
using System.Drawing.Drawing2D;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>Shared GDI+ helpers for the built-in toast renderers.</summary>
    internal static class ToastDrawing
    {
        /// <summary>
        /// Builds a rounded-rectangle path for <paramref name="bounds"/> with the given corner
        /// <paramref name="radius"/>. A radius of <c>0</c> (or larger than half the smaller side)
        /// is clamped to a sensible value.
        /// </summary>
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int max = System.Math.Min(bounds.Width, bounds.Height) / 2;
            if (radius < 1) radius = 1;
            if (radius > max) radius = max;
            int d = radius * 2;

            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
