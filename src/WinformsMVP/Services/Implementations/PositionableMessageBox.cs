using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Positionable MessageBox with support for custom screen locations.
    /// Uses native Windows MessageBox with CBT hook to control position.
    /// </summary>
    /// <remarks>
    /// This is a low-level WinForms utility intended for View-layer code (Forms, UserControls)
    /// and legacy migration scenarios. <b>Do NOT call this directly from Presenters</b> —
    /// it returns <see cref="DialogResult"/> and depends on <c>System.Windows.Forms</c>,
    /// which violates the MVP rule that Presenters must not reference WinForms APIs.
    /// Presenters should use <see cref="IMessageService"/> instead.
    /// </remarks>
    public static class PositionableMessageBox
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

        #region Hook State

        private static IntPtr _hookHandle = IntPtr.Zero;
        private static Point? _targetLocation = null;
        private static HookProc _hookProc = null;  // Keep reference to prevent GC
        private static bool _positioned = false;  // Track if positioning is done

        #endregion

        #region Public API

        /// <summary>
        /// Shows a message box at the specified screen location.
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="caption">The caption (title) of the message box</param>
        /// <param name="buttons">The buttons to display</param>
        /// <param name="icon">The icon to display</param>
        /// <param name="location">The screen location where the message box should appear</param>
        /// <returns>The DialogResult from the message box</returns>
        public static DialogResult Show(
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            return ShowInternal(null, text, caption, buttons, icon, location);
        }

        /// <summary>
        /// Shows a message box at the specified screen location with an owner window.
        /// </summary>
        /// <param name="owner">The owner window (for modality)</param>
        /// <param name="text">The text to display</param>
        /// <param name="caption">The caption (title) of the message box</param>
        /// <param name="buttons">The buttons to display</param>
        /// <param name="icon">The icon to display</param>
        /// <param name="location">The screen location where the message box should appear</param>
        /// <returns>The DialogResult from the message box</returns>
        public static DialogResult Show(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            return ShowInternal(owner, text, caption, buttons, icon, location);
        }

        #endregion

        #region Internal Implementation

        private static DialogResult ShowInternal(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            Point location)
        {
            // Set target location for the hook
            _targetLocation = location;
            _positioned = false;  // Reset positioned flag

            // Create and install CBT hook
            _hookProc = new HookProc(CBTHookCallback);  // Keep reference
            _hookHandle = SetWindowsHookEx(
                WH_CBT,
                _hookProc,
                IntPtr.Zero,
                GetCurrentThreadId());

            DialogResult result;
            try
            {
                // Show the native MessageBox
                // The hook will intercept it and set the position
                if (owner != null)
                {
                    result = MessageBox.Show(owner, text, caption, buttons, icon);
                }
                else
                {
                    result = MessageBox.Show(text, caption, buttons, icon);
                }
            }
            finally
            {
                // Always unhook in finally block (safer than unhooking in callback)
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }

                _targetLocation = null;
                _hookProc = null;  // Allow GC
                _positioned = false;  // Reset flag
            }

            return result;
        }

        private static IntPtr CBTHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HCBT_ACTIVATE && _targetLocation.HasValue && !_positioned)
            {
                try
                {
                    // wParam is the handle of the window being activated
                    IntPtr hWnd = wParam;

                    // ✅ CRITICAL: Verify this is actually a MessageBox (class name #32770)
                    // This prevents accidentally positioning tooltips, IME windows, etc.
                    var className = new System.Text.StringBuilder(32);
                    GetClassName(hWnd, className, className.Capacity);

                    if (className.ToString() != "#32770")
                    {
                        // Not a MessageBox - ignore and continue hook chain
                        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
                    }

                    // This is a MessageBox - set the position
                    // Note: We keep the size (SWP_NOSIZE) and Z-order (SWP_NOZORDER)
                    SetWindowPos(
                        hWnd,
                        IntPtr.Zero,
                        _targetLocation.Value.X,
                        _targetLocation.Value.Y,
                        0,
                        0,
                        SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                    // Mark as positioned (prevents re-entry)
                    _positioned = true;
                }
                catch
                {
                    // Ignore errors in hook - don't break MessageBox
                    // Mark as positioned to prevent retry
                    _positioned = true;
                }
            }

            // Call the next hook in the chain
            // Note: We don't unhook here - that's done in the finally block for safety
            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
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
