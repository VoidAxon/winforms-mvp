using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// Non-blocking, auto-dismissing notifications for presenters (the <c>Toasts</c> property).
    /// Appearance is configured once at startup (<c>mvp.Toasts</c>); view code that needs per-call
    /// styling or an anchor uses the WinForms package's <c>IWinFormsToastService</c>.
    /// </summary>
    /// <remarks>
    /// Safe to call from any thread: implementations show the toast on the UI thread.
    /// </remarks>
    public interface IToastService
    {
        /// <summary>Shows <paramref name="text"/> as a toast.</summary>
        /// <param name="text">The message.</param>
        /// <param name="type">Info / Success / Warning / Error.</param>
        /// <param name="duration">Milliseconds before it fades; <c>null</c> = the configured default.</param>
        void Show(string text, ToastType type, int? duration = null);
    }
}
