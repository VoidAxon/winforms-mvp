using WinformsMVP.Common;
using WinformsMVP.Services.Implementations;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Per-call toast styling for view-side code. <see cref="ToastOptions"/> carries GDI+ types
    /// (Font, renderer), so it is not part of the platform-neutral <see cref="IMessageService"/>.
    /// </summary>
    public static class MessageServiceToastExtensions
    {
        /// <summary>
        /// Shows a toast with explicit <paramref name="options"/> when the service is the built-in
        /// <see cref="MessageService"/>; any other implementation receives the duration only.
        /// </summary>
        public static void ShowToast(this IMessageService messages, string text, ToastType type, ToastOptions options)
        {
            if (messages == null) throw new System.ArgumentNullException(nameof(messages));
            if (messages is MessageService builtIn)
                builtIn.ShowToast(text, type, options);
            else
                messages.ShowToast(text, type, options?.Duration ?? ToastDefaults.Duration);
        }
    }
}
