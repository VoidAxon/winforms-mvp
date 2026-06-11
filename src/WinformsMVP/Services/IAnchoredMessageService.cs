using WinformsMVP.Common;

namespace WinformsMVP.Services
{
    /// <summary>
    /// Shows feedback anchored at the cursor position: a non-blocking toast or a blocking
    /// message box, both appearing where the user just clicked. The anchor is the cursor
    /// position <b>at call time</b> — call synchronously inside the event/action handler on the
    /// UI thread so it equals the click point. After an <c>await</c> the cursor may have moved; use
    /// <see cref="IMessageService.ShowToast(string, ToastType, int)"/> (corner toast) for
    /// deferred feedback.
    /// </summary>
    /// <remarks>
    /// The interface is deliberately minimal — one full-parameter method per feedback kind —
    /// so implementations (including test mocks) implement exactly two methods. All
    /// convenience overloads (<c>ShowInfo</c>, <c>ConfirmYesNo</c>, ...) are extension methods
    /// in <see cref="AnchoredMessageServiceExtensions"/>. Presenters do not take this service
    /// as a dependency; they call the <c>IViewBase</c> extension methods (e.g.
    /// <c>View.ShowToast(...)</c>), which resolve it from <see cref="ServiceLocator.Current"/>.
    /// </remarks>
    public interface IAnchoredMessageService
    {
        /// <summary>Shows a non-blocking toast anchored at the current cursor position.</summary>
        void ShowToast(string text, ToastType type, ToastOptions options);

        /// <summary>
        /// Shows a blocking message box anchored at the current cursor position and returns the
        /// user's choice. <c>OK</c> maps to <see cref="ConfirmResult.Yes"/>.
        /// </summary>
        ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon);
    }
}
