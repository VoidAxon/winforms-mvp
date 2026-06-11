using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A non-blocking toast notification that appears temporarily in a screen corner.
    /// </summary>
    /// <remarks>
    /// Implemented as a <see cref="NativeWindow"/> wrapping a layered Win32 popup instead of a
    /// <see cref="Form"/>. Like a native MessageBox, the toast is therefore invisible to
    /// <see cref="Application.OpenForms"/>: host code that enumerates that collection is never
    /// disturbed by a toast appearing or auto-closing mid-iteration.
    /// <para>
    /// Rendering uses one of two paths, chosen by the resolved renderer's
    /// <see cref="ToastRenderer.CornerRadius"/>:
    /// <list type="bullet">
    /// <item><description><b>Square</b> (radius 0): the content is painted onto the opaque window DC
    /// (<c>WM_PAINT</c>) and the whole window is faded with a constant alpha via
    /// <c>SetLayeredWindowAttributes</c>. ClearType text stays crisp on the opaque surface.</description></item>
    /// <item><description><b>Rounded</b> (radius &gt; 0): the content is drawn into an off-screen 32-bit
    /// ARGB bitmap and pushed with <c>UpdateLayeredWindow</c> (per-pixel alpha), which gives smooth,
    /// anti-aliased corners a window region cannot. ClearType cannot composite under per-pixel alpha,
    /// so those renderers use grayscale anti-aliased text.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This type owns a <em>single</em> toast: its window, painting, and fade-out. Collecting,
    /// stacking, and capping multiple toasts is <see cref="ToastManager"/>'s job. Per-toast
    /// appearance (position, size, font, duration) comes from <see cref="ToastOptions"/>, falling
    /// back to <see cref="ToastDefaults"/>; stacking policy (margin, gap, cap) lives on
    /// <see cref="ToastDefaults"/> and is read by the manager.
    /// </para>
    /// </remarks>
    internal sealed class ToastNotification : NativeWindow
    {
        private readonly string _message;
        private readonly ToastType _type;
        private readonly int _width;
        private readonly int _height;
        private readonly Font _font; // owned by the caller / ToastDefaults — never disposed here
        private readonly ToastPosition _position;
        private readonly int _duration;
        private readonly ToastRenderer _renderer; // resolved once at construction; never null
        private readonly bool _showCloseButton;
        private readonly bool _layered; // true when rounded → per-pixel-alpha (UpdateLayeredWindow) path

        private Bitmap _bitmap; // off-screen content for the layered path; null on the opaque path
        private Timer _closeTimer;
        private Timer _fadeTimer;
        private double _opacity;

        public ToastNotification(string message, ToastType type, ToastOptions options)
        {
            options = options ?? new ToastOptions();

            _message = message ?? string.Empty;
            _type = type;

            var size = options.Size ?? ToastDefaults.Size;
            _width = size.Width;
            _font = options.Font ?? ToastDefaults.Font;
            _position = options.Position ?? ToastDefaults.Position;
            _duration = options.Duration ?? ToastDefaults.Duration;
            _opacity = ToastDefaults.Opacity;
            _renderer = ToastRendererResolver.Resolve(
                options.Renderer, options.Style, ToastDefaults.Renderer, ToastDefaults.Style);
            _showCloseButton = options.ShowCloseButton ?? ToastDefaults.ShowCloseButton;
            _layered = _renderer.CornerRadius > 0; // rounded corners need per-pixel alpha

            // Height: fixed from Size, or sized to content (width stays fixed) when AutoHeight is on.
            bool autoHeight = options.AutoHeight ?? ToastDefaults.AutoHeight;
            _height = autoHeight ? MeasureAutoHeight(options) : size.Height;
        }

        /// <summary>Asks the renderer how tall the content wants to be, clamped to the min/max bounds.</summary>
        private int MeasureAutoHeight(ToastOptions options)
        {
            int min = options.MinHeight ?? ToastDefaults.MinHeight;
            int max = options.MaxHeight ?? ToastDefaults.MaxHeight;
            if (max < min) max = min;

            int height;
            using (var bitmap = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bitmap))
            {
                var ctx = new ToastMeasureContext(g, _message, _type, _font, _width, _showCloseButton);
                height = _renderer.MeasureHeight(ctx);
            }

            if (height < min) height = min;
            if (height > max) height = max;
            return height;
        }

        /// <summary>Screen corner this toast wants to appear in. Read by <see cref="ToastManager"/>.</summary>
        public ToastPosition Position { get { return _position; } }

        /// <summary>Toast width in pixels. Read by <see cref="ToastManager"/>.</summary>
        public int Width { get { return _width; } }

        /// <summary>Toast height in pixels. Read by <see cref="ToastManager"/>.</summary>
        public int Height { get { return _height; } }

        /// <summary>Moves the popup to the given top-left point. Called by <see cref="ToastManager"/>.</summary>
        public void MoveTo(int x, int y)
        {
            if (Handle == IntPtr.Zero)
            {
                return;
            }

            SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Shows the toast as part of the stacked, corner-positioned set. Its position is governed
        /// by <see cref="ToastManager"/> and may shift as other toasts come and go.
        /// </summary>
        public void Show()
        {
            var area = Screen.PrimaryScreen.WorkingArea;

            // Provisional spot hugging the target edge; the manager fixes the final position once
            // this toast joins the stack.
            CreatePopupAt(EdgeX(area), EdgeY(area));

            // Hand off to the manager: it adds this toast to the stack, evicts any overflow, and
            // positions everyone (this toast's final spot included).
            ToastManager.Add(this);

            StartCloseTimer();
        }

        /// <summary>
        /// Shows the toast anchored at a screen point, tooltip-style: positioned near
        /// <paramref name="anchor"/> and nudged so the whole toast stays on screen. It is a
        /// standalone singleton — not stacked, and it replaces any previous anchored toast.
        /// </summary>
        public void ShowAnchored(Point anchor)
        {
            // Clamp to the screen the anchor sits on (nearest screen if it is off-screen), so a
            // toast anchored on a non-primary monitor stays there instead of being pulled back to
            // the primary one. Mirrors AnchoredMessageBox.ClampToScreen.
            var area = Screen.FromPoint(anchor).WorkingArea;
            Point topLeft = ToastLayout.Anchor(anchor, new Size(_width, _height), area, ToastDefaults.Margin);

            CreatePopupAt(topLeft.X, topLeft.Y);

            // Register as the single anchored toast (closes any previous one). No stacking/reflow.
            ToastManager.RegisterAnchored(this);

            StartCloseTimer();
        }

        /// <summary>Creates the layered popup at the given top-left and shows it without stealing focus.</summary>
        private void CreatePopupAt(int x, int y)
        {
            var cp = new CreateParams
            {
                Caption = string.Empty,
                // Null class name makes NativeWindow register a default window class whose
                // WndProc routes back to this instance.
                ClassName = null,
                X = x,
                Y = y,
                Width = _width,
                Height = _height,
                Parent = IntPtr.Zero,
                Style = unchecked((int)WS_POPUP),
                ExStyle = WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            };

            CreateHandle(cp);

            if (_layered)
            {
                // Rounded: per-pixel alpha. Push the bitmap before showing to avoid a square flash.
                RenderContent();
                PushLayered();
            }
            else
            {
                // Square: opaque content (painted on WM_PAINT) with a constant window alpha.
                ApplyOpacity();
            }

            ShowWindow(Handle, SW_SHOWNOACTIVATE);
        }

        /// <summary>Starts the timer that begins the fade-out after the visible duration.</summary>
        private void StartCloseTimer()
        {
            _closeTimer = new Timer { Interval = _duration };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                StartFadeOut();
            };
            _closeTimer.Start();
        }

        private bool IsRight
        {
            get { return _position == ToastPosition.TopRight || _position == ToastPosition.BottomRight; }
        }

        private bool IsBottom
        {
            get { return _position == ToastPosition.BottomLeft || _position == ToastPosition.BottomRight; }
        }

        private int EdgeX(Rectangle area)
        {
            int margin = ToastDefaults.Margin;
            return IsRight ? area.Right - _width - margin : area.Left + margin;
        }

        private int EdgeY(Rectangle area)
        {
            int margin = ToastDefaults.Margin;
            return IsBottom ? area.Bottom - _height - margin : area.Top + margin;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_PAINT:
                    if (!_layered)
                    {
                        Paint(); // opaque path paints the window DC directly (crisp ClearType)
                        return;
                    }
                    break; // layered path: content comes from UpdateLayeredWindow; let DefWindowProc validate
                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1; // we own the whole surface; skip the background erase / flicker
                    return;
                case WM_LBUTTONUP:
                    CloseNow(); // click anywhere (incl. the close glyph) dismisses it
                    return;
            }

            base.WndProc(ref m);
        }

        // --- Opaque (square) path ---------------------------------------------------------------

        /// <summary>Paints the renderer's content straight onto the opaque window DC.</summary>
        private void Paint()
        {
            PAINTSTRUCT ps;
            IntPtr hdc = BeginPaint(Handle, out ps);
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    _renderer.Render(new ToastRenderContext(g, new Rectangle(0, 0, _width, _height), _message, _type, _font, _renderer.CornerRadius, _showCloseButton));
                }
            }
            finally
            {
                EndPaint(Handle, ref ps);
            }
        }

        /// <summary>Applies the current fade opacity to the whole window (opaque path only).</summary>
        private void ApplyOpacity()
        {
            if (Handle == IntPtr.Zero)
            {
                return;
            }

            int alpha = (int)(_opacity * 255);
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            SetLayeredWindowAttributes(Handle, 0, (byte)alpha, LWA_ALPHA);
        }

        // --- Layered (rounded) path -------------------------------------------------------------

        /// <summary>
        /// Renders the toast content once into an off-screen 32-bit ARGB bitmap. Rounded renderers
        /// leave the area outside their shape transparent, so the composited corners are smooth.
        /// </summary>
        private void RenderContent()
        {
            _bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_bitmap))
            {
                _renderer.Render(new ToastRenderContext(g, new Rectangle(0, 0, _width, _height), _message, _type, _font, _renderer.CornerRadius, _showCloseButton));
            }
        }

        /// <summary>
        /// Composites <see cref="_bitmap"/> onto the layered window with per-pixel alpha, scaled by
        /// the current fade <see cref="_opacity"/>. Called at show time and on every fade tick (the
        /// bitmap is unchanged across fade ticks — only the constant alpha varies).
        /// </summary>
        private void PushLayered()
        {
            if (Handle == IntPtr.Zero || _bitmap == null)
            {
                return;
            }

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                // GetHbitmap(transparent) yields a premultiplied ARGB DIB, as UpdateLayeredWindow expects.
                hBitmap = _bitmap.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                var size = new SIZE { cx = _width, cy = _height };
                var source = new POINT { x = 0, y = 0 };
                int alpha = (int)(_opacity * 255);
                if (alpha < 0) alpha = 0;
                if (alpha > 255) alpha = 255;
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = (byte)alpha,
                    AlphaFormat = AC_SRC_ALPHA,
                };

                // pptDst = IntPtr.Zero keeps the window's current position (set by CreateHandle / MoveTo).
                UpdateLayeredWindow(Handle, screenDc, IntPtr.Zero, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // --- Fade & teardown --------------------------------------------------------------------

        private void StartFadeOut()
        {
            // Fade timer - gradually reduces opacity for a smooth fade.
            _fadeTimer = new Timer { Interval = 50 };
            _fadeTimer.Tick += (s, e) =>
            {
                _opacity -= 0.1;
                if (_opacity <= 0)
                {
                    _fadeTimer.Stop();
                    CloseNow();
                }
                else if (_layered)
                {
                    PushLayered();
                }
                else
                {
                    ApplyOpacity();
                }
            };
            _fadeTimer.Start();
        }

        /// <summary>
        /// Tears down this toast: stops its timers, removes it from the stack (the manager slides
        /// the rest back toward the edge), disposes the content bitmap, and destroys the window.
        /// Also called by the manager when evicting the oldest toast to honor the visible-count cap.
        /// </summary>
        public void CloseNow()
        {
            if (_closeTimer != null)
            {
                _closeTimer.Stop();
                _closeTimer.Dispose();
                _closeTimer = null;
            }

            if (_fadeTimer != null)
            {
                _fadeTimer.Stop();
                _fadeTimer.Dispose();
                _fadeTimer = null;
            }

            ToastManager.Remove(this);

            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }

            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        #region Win32 interop

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_POPUP = 0x80000000;

        private const int SW_SHOWNOACTIVATE = 4;
        private const uint LWA_ALPHA = 0x00000002;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const int WM_PAINT = 0x000F;
        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_LBUTTONUP = 0x0202;

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int ULW_ALPHA = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        #endregion
    }
}
