using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// A non-blocking toast notification that appears temporarily at the bottom-right of the screen.
    /// </summary>
    /// <remarks>
    /// Implemented as a <see cref="NativeWindow"/> wrapping a layered Win32 popup instead of a
    /// <see cref="Form"/>. Like a native MessageBox, the toast is therefore invisible to
    /// <see cref="Application.OpenForms"/>: host code that enumerates that collection is never
    /// disturbed by a toast appearing or auto-closing mid-iteration.
    /// </remarks>
    internal sealed class ToastNotification : NativeWindow
    {
        private const int ToastWidth = 350;
        private const int ToastHeight = 80;
        private const int ScreenMargin = 20;

        // Roots live toasts so the GC cannot collect a NativeWindow whose handle is still
        // alive. A Form was kept alive by Application.OpenForms; a NativeWindow has no such
        // anchor, so we provide one. Only ever touched on the UI thread.
        private static readonly List<ToastNotification> LiveToasts = new List<ToastNotification>();

        private readonly string _message;
        private readonly ToastType _type;
        private readonly int _duration;

        private Timer _closeTimer;
        private Timer _fadeTimer;
        private double _opacity = 0.95;

        public ToastNotification(string message, ToastType type, int duration)
        {
            _message = message ?? string.Empty;
            _type = type;
            _duration = duration;
        }

        /// <summary>
        /// Creates the layered popup, shows it without stealing focus, and starts the auto-close timer.
        /// </summary>
        public void Show()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var cp = new CreateParams
            {
                Caption = string.Empty,
                // Null class name makes NativeWindow register a default window class whose
                // WndProc routes back to this instance.
                ClassName = null,
                X = area.Right - ToastWidth - ScreenMargin,
                Y = area.Bottom - ToastHeight - ScreenMargin,
                Width = ToastWidth,
                Height = ToastHeight,
                Parent = IntPtr.Zero,
                Style = unchecked((int)WS_POPUP),
                ExStyle = WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            };

            CreateHandle(cp);
            ApplyOpacity();
            ShowWindow(Handle, SW_SHOWNOACTIVATE);

            LiveToasts.Add(this);

            // Close timer - starts the fade out after the visible duration.
            _closeTimer = new Timer { Interval = _duration };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                StartFadeOut();
            };
            _closeTimer.Start();
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
            g.Clear(GetBackgroundColor(_type));

            using (var iconFont = new Font("Segoe UI", 18f, FontStyle.Bold))
            using (var messageFont = new Font("Segoe UI", 10f))
            using (var closeFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var leftMiddle = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                // Icon
                g.DrawString(GetIconText(_type), iconFont, Brushes.White, new RectangleF(10, 10, 40, 60), centered);
                // Message
                g.DrawString(_message, messageFont, Brushes.White, new RectangleF(60, 10, 280, 60), leftMiddle);
                // Close glyph
                g.DrawString("✖", closeFont, Brushes.White, new RectangleF(320, 5, 20, 20), centered);
            }
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

        private void CloseNow()
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

            LiveToasts.Remove(this);

            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        private static Color GetBackgroundColor(ToastType type)
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

        private static string GetIconText(ToastType type)
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

        #region Win32 interop

        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_POPUP = 0x80000000;

        private const int SW_SHOWNOACTIVATE = 4;
        private const uint LWA_ALPHA = 0x00000002;

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

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        #endregion
    }
}
