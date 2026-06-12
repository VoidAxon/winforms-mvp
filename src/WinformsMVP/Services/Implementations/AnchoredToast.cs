using System.Drawing;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Shows a single toast notification anchored to a screen point, tooltip-style: it appears next
    /// to the point you give and is automatically nudged back on screen if the point sits near (or
    /// past) an edge, so the whole toast is always visible.
    /// </summary>
    /// <remarks>
    /// This is a low-level View-layer utility for Forms / UserControls that know a pixel location
    /// (a control's screen rectangle, the cursor, a hit-test result). <b>Do NOT call it from a
    /// Presenter</b> — Presenters must not deal in screen coordinates. Presenters that want a toast
    /// use <see cref="IMessageService.ShowToast(string, ToastType, ToastOptions)"/>, which shows
    /// stacked toasts in a screen corner instead; when the toast's position carries meaning, the
    /// Presenter calls a small semantic view method (e.g. <c>View.ShowRowTouched()</c>) whose Form
    /// implementation picks the anchor and calls this utility.
    /// <para>
    /// Unlike the corner toasts, an anchored toast is a standalone singleton: only one exists at a
    /// time, and showing a new one closes the previous. It does not stack with — or disturb — the
    /// corner toasts. Appearance comes from <paramref name="options"/> (falling back to
    /// <see cref="ToastDefaults"/>); the <see cref="ToastOptions.Position"/> corner is ignored
    /// here because the anchor point determines placement.
    /// </para>
    /// </remarks>
    public static class AnchoredToast
    {
        /// <summary>
        /// Shows an anchored toast at <paramref name="anchor"/> (a screen-coordinate point).
        /// </summary>
        /// <param name="text">The message to display.</param>
        /// <param name="type">The toast kind (drives color and icon).</param>
        /// <param name="anchor">Screen point to anchor to. Any value is safe — an off-screen or
        /// negative point is clamped so the toast stays fully visible.</param>
        /// <param name="options">Optional per-toast appearance overrides. <c>Position</c> is ignored.</param>
        public static void Show(string text, ToastType type, Point anchor, ToastOptions options = null)
        {
            var toast = new ToastNotification(text, type, options);
            toast.ShowAnchored(anchor);
        }

        /// <summary>Shows an anchored information toast at <paramref name="anchor"/>.</summary>
        public static void ShowInfo(string text, Point anchor, ToastOptions options = null)
        {
            Show(text, ToastType.Info, anchor, options);
        }

        /// <summary>Shows an anchored success toast at <paramref name="anchor"/>.</summary>
        public static void ShowSuccess(string text, Point anchor, ToastOptions options = null)
        {
            Show(text, ToastType.Success, anchor, options);
        }

        /// <summary>Shows an anchored warning toast at <paramref name="anchor"/>.</summary>
        public static void ShowWarning(string text, Point anchor, ToastOptions options = null)
        {
            Show(text, ToastType.Warning, anchor, options);
        }

        /// <summary>Shows an anchored error toast at <paramref name="anchor"/>.</summary>
        public static void ShowError(string text, Point anchor, ToastOptions options = null)
        {
            Show(text, ToastType.Error, anchor, options);
        }
    }
}
