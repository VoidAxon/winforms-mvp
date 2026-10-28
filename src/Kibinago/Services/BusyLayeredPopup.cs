using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kibinago.Services
{
    /// <summary>
    /// A borderless, non-activating, layered top-level window owned by a form, painted with per-pixel alpha.
    /// A <see cref="NativeWindow"/>, not a Form: it never appears in <see cref="Application.OpenForms"/> and is
    /// not a control of any view. Its surface is a 32-bit premultiplied DIB that GDI+ draws into directly and
    /// that is reused while the size stays the same. A click on it neither activates it nor reaches the view.
    /// </summary>
    internal sealed class BusyLayeredPopup : NativeWindow, IDisposable
    {
        private Surface _surface;
        private bool _shown;
        private bool _disposed;

        /// <summary>Creates the window, hidden, owned by <paramref name="owner"/> (a top-level window handle).</summary>
        public BusyLayeredPopup(IntPtr owner)
        {
            CreateHandle(new CreateParams
            {
                Caption = "Busy",
                Style = unchecked((int)Native.WS_POPUP),
                ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE,
                Parent = owner,   // owner: kept above it, hidden while it is minimized, never above other apps
            });
        }

        public bool IsAlive => !_disposed && Handle != IntPtr.Zero;

        /// <summary>A cleared surface of <paramref name="size"/> to paint on (the previous one is reused when the size is the same).</summary>
        public Graphics Begin(Size size, float dpi)
        {
            if (_surface == null || _surface.Size != size)
            {
                var replacement = new Surface(size);   // built before the old one goes: a failure keeps the old one usable
                _surface?.Dispose();
                _surface = replacement;
            }
            _surface.SetDpi(dpi);
            var g = _surface.Graphics;
            g.ResetTransform();
            g.ResetClip();
            g.CompositingMode = CompositingMode.SourceOver;
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            return g;
        }

        /// <summary>Puts the painted surface on screen at <paramref name="location"/> and shows the window.</summary>
        public void Present(Point location)
        {
            if (!IsAlive || _surface == null) return;
            _surface.Graphics.Flush();
            _surface.Present(Handle, location);
            if (_shown) return;
            Native.ShowWindow(Handle, Native.SW_SHOWNOACTIVATE);
            _shown = true;
        }

        public void Hide()
        {
            if (!_shown || !IsAlive) return;
            Native.ShowWindow(Handle, Native.SW_HIDE);
            _shown = false;
        }

        /// <summary>Puts this window above <paramref name="below"/>'s (both owned by the same form).</summary>
        public void PlaceAbove(BusyLayeredPopup below)
        {
            if (!IsAlive || below == null || !below.IsAlive) return;
            Native.SetWindowPos(below.Handle, Handle, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)Native.MA_NOACTIVATEANDEAT;   // a click does nothing and activates nothing
                    return;
                case Native.WM_SETCURSOR:
                    Cursor.Current = Cursors.WaitCursor;
                    m.Result = (IntPtr)1;
                    return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (Handle != IntPtr.Zero) DestroyHandle();   // already gone when its owner was destroyed
            }
            finally
            {
                _surface?.Dispose();
                _surface = null;
            }
        }

        private sealed class Surface : IDisposable
        {
            private IntPtr _dc;
            private IntPtr _dib;
            private IntPtr _previous;
            private Bitmap _bitmap;

            public Surface(Size size)
            {
                Size = size;
                try
                {
                    var info = new Native.BITMAPINFOHEADER
                    {
                        biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)),
                        biWidth = size.Width,
                        biHeight = -size.Height,   // top-down rows, matching the Bitmap below
                        biPlanes = 1,
                        biBitCount = 32,
                    };
                    _dc = Native.CreateCompatibleDC(IntPtr.Zero);
                    if (_dc == IntPtr.Zero) throw new Win32Exception();
                    _dib = Native.CreateDIBSection(_dc, ref info, 0, out var bits, IntPtr.Zero, 0);
                    if (_dib == IntPtr.Zero || bits == IntPtr.Zero) throw new Win32Exception();
                    _previous = Native.SelectObject(_dc, _dib);
                    _bitmap = new Bitmap(size.Width, size.Height, size.Width * 4, PixelFormat.Format32bppPArgb, bits);
                    Graphics = Graphics.FromImage(_bitmap);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public Size Size { get; }
            public Graphics Graphics { get; private set; }

            // A Graphics keeps the resolution its bitmap had when it was created: recreate it after a change.
            public void SetDpi(float dpi)
            {
                if (dpi <= 0 || Math.Abs(_bitmap.HorizontalResolution - dpi) <= 0.01f) return;
                _bitmap.SetResolution(dpi, dpi);
                var replacement = Graphics.FromImage(_bitmap);
                Graphics.Dispose();
                Graphics = replacement;
            }

            public void Present(IntPtr window, Point location)
            {
                var screen = Native.GetDC(IntPtr.Zero);
                try
                {
                    var position = new Native.POINT { X = location.X, Y = location.Y };
                    var size = new Native.SIZE { cx = Size.Width, cy = Size.Height };
                    var source = new Native.POINT();
                    var blend = new Native.BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };   // AC_SRC_ALPHA
                    Native.UpdateLayeredWindow(window, screen, ref position, ref size, _dc, ref source, 0, ref blend, Native.ULW_ALPHA);
                }
                finally
                {
                    if (screen != IntPtr.Zero) Native.ReleaseDC(IntPtr.Zero, screen);
                }
            }

            public void Dispose()
            {
                Graphics?.Dispose();
                Graphics = null;
                _bitmap?.Dispose();
                _bitmap = null;
                if (_dc != IntPtr.Zero && _previous != IntPtr.Zero) Native.SelectObject(_dc, _previous);
                _previous = IntPtr.Zero;
                if (_dib != IntPtr.Zero) Native.DeleteObject(_dib);
                _dib = IntPtr.Zero;
                if (_dc != IntPtr.Zero) Native.DeleteDC(_dc);
                _dc = IntPtr.Zero;
            }
        }

        internal static class Native
        {
            public const uint WS_POPUP = 0x80000000;
            public const int WS_EX_LAYERED = 0x00080000, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
            public const int WM_MOUSEACTIVATE = 0x0021, WM_SETCURSOR = 0x0020, MA_NOACTIVATEANDEAT = 4;
            public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, ULW_ALPHA = 2;
            public const int SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;

            [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
            [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

            [StructLayout(LayoutKind.Sequential)]
            public struct BITMAPINFOHEADER
            {
                public int biSize, biWidth, biHeight;
                public short biPlanes, biBitCount;
                public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
            }

            [DllImport("user32.dll")]
            public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
                IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

            [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
            [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
            [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
            [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
            [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, int flags);
            [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
            [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
            [DllImport("gdi32.dll", SetLastError = true)] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
            [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
            [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
            [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        }
    }
}
