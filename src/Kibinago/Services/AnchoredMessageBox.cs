using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kibinago.Services
{
    /// <summary>
    /// Shows a native Windows MessageBox at a specific screen location, nudged so the whole dialog
    /// stays on screen even if the point sits near (or past) an edge. The MessageBox version of
    /// <see cref="AnchoredToast"/>: a View-layer utility for code that knows a pixel location.
    /// </summary>
    /// <remarks>
    /// This is a low-level View-layer utility intended for Forms / UserControls (a control's screen
    /// rectangle, the cursor, a hit-test result) and legacy migration. <b>Do NOT call it from a
    /// Presenter</b> — it returns <see cref="DialogResult"/>, depends on <c>System.Windows.Forms</c>,
    /// and deals in screen coordinates, all of which violate the MVP rule that Presenters stay out
    /// of the UI/positioning layer. Presenters use <see cref="IMessageService"/> for centered
    /// dialogs instead; when the dialog's position carries meaning, the Presenter calls a small
    /// semantic view method (e.g. <c>bool View.ConfirmDelete()</c>) whose Form implementation
    /// picks the anchor and calls this utility — the business result flows back as the return value.
    /// <para>
    /// The dialog is shown by the application's <see cref="IMessageDialogBackend"/> (resolved from
    /// <see cref="ServiceLocator.Current"/>), so replacing the backend restyles anchored boxes too;
    /// an empty caption becomes <see cref="MessageDialogOptions.DefaultCaption"/>. The default
    /// backend positions a native MessageBox with a CBT hook, so — like any MessageBox — it never
    /// appears in <see cref="Application.OpenForms"/>. The requested location is clamped to the
    /// working area of the screen it lands on, so any point (even off-screen or negative) yields a
    /// fully-visible dialog.
    /// </para>
    /// </remarks>
    public static class AnchoredMessageBox
    {
        #region Windows API Declarations

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        #endregion


        #region Public API

        /// <summary>
        /// Shows a message box at the specified screen location.
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="caption">The caption (title) of the message box</param>
        /// <param name="buttons">The buttons to display</param>
        /// <param name="icon">The icon to display</param>
        /// <param name="location">The screen location to anchor to. Any value is safe — it is
        /// clamped so the whole dialog stays visible.</param>
        /// <returns>The DialogResult from the message box</returns>
        public static DialogResult Show(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            return ShowThroughBackend(null, text, caption, buttons, icon, location);
        }

        /// <summary>
        /// Shows a message box at the specified screen location with an owner window.
        /// </summary>
        /// <param name="owner">The owner window (for modality)</param>
        /// <param name="text">The text to display</param>
        /// <param name="caption">The caption (title) of the message box</param>
        /// <param name="buttons">The buttons to display</param>
        /// <param name="icon">The icon to display</param>
        /// <param name="location">The screen location to anchor to. Any value is safe — it is
        /// clamped so the whole dialog stays visible.</param>
        /// <returns>The DialogResult from the message box</returns>
        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            return ShowThroughBackend(owner, text, caption, buttons, icon, location);
        }

        #endregion

        #region Internal Implementation

        private static DialogResult ShowThroughBackend(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            var services = ServiceLocator.Current;
            var backend = services.Resolve<IMessageDialogBackend>() ?? new NativeMessageDialogBackend();
            var defaultCaption = services.Resolve<MessageDialogOptions>()?.DefaultCaption;

            var answer = backend.Show(new MessageDialogRequest
            {
                Text = text,
                Caption = string.IsNullOrEmpty(caption) ? defaultCaption ?? caption : caption,
                Buttons = NativeMessageDialogBackend.FromWinForms(buttons),
                Icon = NativeMessageDialogBackend.FromWinForms(icon),
                Owner = owner,
                Location = location,
            });
            return NativeMessageDialogBackend.ToWinForms(answer);
        }

        /// <summary>Native MessageBox placed at <paramref name="location"/> (the default backend's positioned path).</summary>
        internal static DialogResult ShowNative(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            // One hook per call: a box shown while another is open (a timer, a handler in its modal loop)
            // or on another UI thread has its own, and each call removes only its own.
            using (new HookSession(location))
            {
                return owner != null
                    ? MessageBox.Show(owner, text, caption, buttons, icon)
                    : MessageBox.Show(text, caption, buttons, icon);
            }
        }

        /// <summary>
        /// A CBT hook on the current thread that moves the first message box activated after it to a
        /// location. Holds its own handle and delegate (alive until disposed), so calls never share state.
        /// </summary>
        internal sealed class HookSession : IDisposable
        {
            private readonly Point _location;
            private HookProc _proc;   // referenced while the hook is installed (never collected under it)
            private IntPtr _handle;
            private bool _positioned;

            public HookSession(Point location)
            {
                _location = location;
                _proc = Callback;
                _handle = SetWindowsHookEx(WH_CBT, _proc, IntPtr.Zero, GetCurrentThreadId());
            }

            public bool IsInstalled => _handle != IntPtr.Zero;

            /// <summary>Removes the hook; <c>true</c> when it was installed and is now removed.</summary>
            public bool Uninstall()
            {
                var handle = _handle;
                _handle = IntPtr.Zero;
                var removed = handle != IntPtr.Zero && UnhookWindowsHookEx(handle);
                _proc = null;
                return removed;
            }

            public void Dispose() => Uninstall();

            private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
            {
                if (nCode == HCBT_ACTIVATE && !_positioned)
                {
                    try
                    {
                        // wParam is the window being activated; only a dialog box (#32770, a MessageBox) is
                        // moved, not a tooltip or an IME window.
                        var className = new System.Text.StringBuilder(32);
                        GetClassName(wParam, className, className.Capacity);
                        if (className.ToString() == "#32770")
                        {
                            // Clamped so the whole dialog is visible; native size and Z-order kept.
                            var target = ClampToScreen(_location, wParam);
                            SetWindowPos(wParam, IntPtr.Zero, target.X, target.Y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                            _positioned = true;
                        }
                    }
                    catch
                    {
                        _positioned = true;   // never break the MessageBox; no retry
                    }
                }
                return CallNextHookEx(_handle, nCode, wParam, lParam);
            }
        }

        /// <summary>
        /// Shifts <paramref name="desired"/> so the message box (whose size we read from its handle)
        /// stays fully inside the working area of the screen it lands on. Robust to off-screen or
        /// negative points.
        /// </summary>
        private static Point ClampToScreen(Point desired, IntPtr hWnd)
        {
            RECT rect;
            if (!GetWindowRect(hWnd, out rect))
            {
                return desired;
            }

            Rectangle area = Screen.FromPoint(desired).WorkingArea;

            // If the dialog is larger than the area, the lower bound (Left/Top) wins so its
            // top-left stays visible.
            int maxX = area.Right - rect.Width;
            int maxY = area.Bottom - rect.Height;
            if (maxX < area.Left) maxX = area.Left;
            if (maxY < area.Top) maxY = area.Top;

            int x = desired.X;
            int y = desired.Y;
            if (x < area.Left) x = area.Left; else if (x > maxX) x = maxX;
            if (y < area.Top) y = area.Top; else if (y > maxY) y = maxY;

            return new Point(x, y);
        }

        #endregion

        #region Convenience Methods

        /// <summary>
        /// Shows an information message at the specified location.
        /// </summary>
        public static void ShowInfo(string text, Point location, string caption = "")
        {
            Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information, location);
        }

        /// <summary>
        /// Shows a warning message at the specified location.
        /// </summary>
        public static void ShowWarning(string text, Point location, string caption = "")
        {
            Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning, location);
        }

        /// <summary>
        /// Shows an error message at the specified location.
        /// </summary>
        public static void ShowError(string text, Point location, string caption = "")
        {
            Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error, location);
        }

        /// <summary>
        /// Shows a Yes/No confirmation dialog at the specified location.
        /// </summary>
        /// <returns>True if Yes was clicked, false otherwise</returns>
        public static bool ConfirmYesNo(string text, Point location, string caption = "")
        {
            return Show(text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, location) == DialogResult.Yes;
        }

        /// <summary>
        /// Shows an OK/Cancel confirmation dialog at the specified location.
        /// </summary>
        /// <returns>True if OK was clicked, false otherwise</returns>
        public static bool ConfirmOkCancel(string text, Point location, string caption = "")
        {
            return Show(text, caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, location) == DialogResult.OK;
        }

        #endregion
    }
}
