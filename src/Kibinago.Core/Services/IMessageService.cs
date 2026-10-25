using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// Modal message boxes for presenters. Non-blocking notifications are a separate service,
    /// <see cref="IToastService"/>.
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
        // Presenter one. View code uses AnchoredMessageBox / AnchoredToast directly. A Presenter
        // that needs position-meaningful feedback calls a small semantic view method (e.g.
        // View.ConfirmDelete()) whose Form implementation picks the anchor. Neither belongs on
        // this Presenter-facing API.
    }
}
