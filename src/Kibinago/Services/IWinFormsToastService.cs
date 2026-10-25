using System.Drawing;
using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// The WinForms toast service: <see cref="IToastService"/> plus the per-call appearance and
    /// anchored variants that take GDI+ types, for view code. The default <see cref="IToastService"/>
    /// registration forwards to whichever instance is registered under this interface, so
    /// presenter and view toasts share one stack. Replace this interface (<c>mvp.Toasts.Use(...)</c>) to replace both.
    /// </summary>
    public interface IWinFormsToastService : IToastService
    {
        /// <summary>
        /// Shows a corner toast with per-call overrides. <paramref name="options"/> (font included) is
        /// copied before this returns. Safe to call from any thread.
        /// </summary>
        void Show(string text, ToastType type, ToastOptions options);

        /// <summary>
        /// Shows a toast near the screen point <paramref name="anchor"/>, tooltip-style, clamped to
        /// that screen. Only one anchored toast exists at a time: a new one replaces the previous.
        /// It is not part of the corner stack, and <see cref="ToastOptions.Position"/> is ignored.
        /// </summary>
        void ShowAnchored(string text, ToastType type, Point anchor, ToastOptions options = null);
    }
}
