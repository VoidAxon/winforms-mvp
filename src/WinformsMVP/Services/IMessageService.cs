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

        // Positioned (cursor-anchored) feedback is available to Presenters via IViewBase extension
        // methods: View.ShowToast(text, type) and View.ConfirmYesNo(text, caption). These resolve
        // IAnchoredMessageService from ServiceLocator.Current and read Cursor.Position at call time.
        // Call synchronously inside the action handler so the cursor equals the click point.
        // View code can also call AnchoredMessageBox / AnchoredToast directly (raw coordinates).

        // Toast notifications (non-blocking temporary messages)
        void ShowToast(string text, ToastType type, int duration = 3000);
        void ShowToast(string text, ToastType type, ToastOptions options);
    }
}
