using System.Drawing;
using WinformsMVP.Common;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Shows feedback anchored at a screen point: a non-blocking toast or a blocking message box
    /// appearing where the user just clicked. Method names mirror <see cref="IMessageService"/> —
    /// the button/icon combination is expressed by the method, not by parameters; the difference
    /// from <see cref="IMessageService"/> is placement.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every method comes in two forms. The form <b>without</b> a <see cref="Point"/> anchors at
    /// the cursor position <b>at call time</b> — call it synchronously inside the event/action
    /// handler on the UI thread so it equals the click point; after an <c>await</c> the cursor
    /// may have moved (use <see cref="IMessageService.ShowToast(string, ToastType, int)"/>, the
    /// corner toast, for deferred feedback). The form <b>with</b> a <see cref="Point"/> lets the
    /// caller supply its own screen anchor (a control's bounds, a hit-test result, ...).
    /// </para>
    /// <para>
    /// Presenters never pass coordinates: they use the cursor-anchored <c>IViewBase</c>
    /// extensions (<c>View.ShowToast(...)</c>, see <c>AnchoredMessageViewExtensions</c>), which
    /// resolve this service from <see cref="ServiceLocator.Current"/>. The <see cref="Point"/>
    /// overloads are for View-layer code that knows a pixel location and wants to control
    /// placement itself.
    /// </para>
    /// </remarks>
    public interface IAnchoredMessageService
    {
        // Toast notifications (non-blocking)
        void ShowToast(string text, ToastType type, ToastOptions options = null);
        void ShowToast(string text, ToastType type, Point anchor, ToastOptions options = null);

        // Message dialogs (blocking)
        void ShowInfo(string text, string caption = "");
        void ShowInfo(string text, Point anchor, string caption = "");
        void ShowWarning(string text, string caption = "");
        void ShowWarning(string text, Point anchor, string caption = "");
        void ShowError(string text, string caption = "");
        void ShowError(string text, Point anchor, string caption = "");

        // Confirmations (blocking; true = affirmative choice)
        bool ConfirmYesNo(string text, string caption = "");
        bool ConfirmYesNo(string text, Point anchor, string caption = "");
        bool ConfirmOkCancel(string text, string caption = "");
        bool ConfirmOkCancel(string text, Point anchor, string caption = "");
        ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
        ConfirmResult ConfirmYesNoCancel(string text, Point anchor, string caption = "");
    }
}
