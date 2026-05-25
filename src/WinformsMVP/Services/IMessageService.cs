using System.Drawing;
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

        // Positioned message dialogs (shown at specific screen location)
        bool ConfirmYesNoAt(string text, Point location, string caption = "");
        bool ConfirmOkCancelAt(string text, Point location, string caption = "");
        ConfirmResult ConfirmYesNoCancelAt(string text, Point location, string caption = "");
        void ShowInfoAt(string text, Point location, string caption = "");
        void ShowWarningAt(string text, Point location, string caption = "");
        void ShowErrorAt(string text, Point location, string caption = "");

        // Toast notifications (non-blocking temporary messages)
        void ShowToast(string text, ToastType type, int duration = 3000);
    }
}
