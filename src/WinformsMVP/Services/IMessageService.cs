using WinformsMVP.Common;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Provides message box and notification services.
    /// </summary>
    public interface IMessageService
    {
        // Standard message dialogs
        bool ConfirmYesNo(string text, string caption = "");
        bool ConfirmOkCancel(string text, string caption = "");
        ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
        void ShowInfo(string text, string caption = "");
        void ShowWarning(string text, string caption = "");
        void ShowError(string text, string caption = "");

        // Positioned dialogs are a View-layer concern (the View knows screen coordinates), not a
        // Presenter one. For a MessageBox at a specific point, View code uses AnchoredMessageBox;
        // for a positioned toast it uses AnchoredToast. Neither belongs on this Presenter-facing API.

        // Toast notifications (non-blocking temporary messages)
        void ShowToast(string text, ToastType type, int duration = 3000);
        void ShowToast(string text, ToastType type, ToastOptions options);
    }
}
