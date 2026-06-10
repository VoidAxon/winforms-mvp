using System;
using System.Drawing;
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
            _height = size.Height;
            _font = options.Font ?? ToastDefaults.Font;
            _position = options.Position ?? ToastDefaults.Position;
            _duration = options.Duration ?? ToastDefaults.Duration;
            _opacity = ToastDefaults.Opacity;
            _renderer = ToastRendererResolver.Resolve(
                options.Renderer, options.Style, ToastDefaults.Renderer, ToastDefaults.Style);
            _showCloseButton = options.ShowCloseButton ?? ToastDefaults.ShowCloseButton;
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
            var area = Screen.PrimaryScreen.WorkingArea;
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
            ApplyCornerRadius();
            ApplyOpacity();
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
                    Paint();
                    return; // BeginPaint/EndPaint already validated the update region
                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1; // we paint the whole surface ourselves; skip flicker
                    return;
                case WM_LBUTTONUP:
                    CloseNow(); // click anywhere (incl. the close glyph) dismisses it
                    return;
            }

            base.WndProc(ref m);
        }

        private void Paint()
        {
            PAINTSTRUCT ps;
            IntPtr hdc = BeginPaint(Handle, out ps);
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    Render(g);
                }
            }
            finally
            {
                EndPaint(Handle, ref ps);
            }
        }

        private void Render(Graphics g)
        {
            _renderer.Render(new ToastRenderContext(g, new Rectangle(0, 0, _width, _height), _message, _type, _font, _renderer.CornerRadius, _showCloseButton));
        }

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
                else
                {
                    ApplyOpacity();
                }
            };
            _fadeTimer.Start();
        }

        /// <summary>
        /// Rounds the popup to the resolved renderer's <see cref="ToastRenderer.CornerRadius"/> by
        /// applying a window region. A radius of <c>0</c> leaves the window square. The OS takes
        /// ownership of the region on <c>SetWindowRgn(..., true)</c>, so it must not be deleted here.
        /// </summary>
        private void ApplyCornerRadius()
        {
            int radius = _renderer.CornerRadius;
            if (radius <= 0 || Handle == IntPtr.Zero)
            {
                return;
            }

            // CreateRoundRectRgn treats the right/bottom as exclusive, so use width/height + 1.
            IntPtr region = CreateRoundRectRgn(0, 0, _width + 1, _height + 1, radius * 2, radius * 2);
            SetWindowRgn(Handle, region, true);
        }

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

        /// <summary>
        /// Tears down this toast: stops its timers, removes it from the stack (the manager slides
        /// the rest back toward the edge), and destroys the window. Also called by the manager
        /// when evicting the oldest toast to honor the visible-count cap.
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

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        #endregion
    }
}
