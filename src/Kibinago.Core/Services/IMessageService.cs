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
        /// <summary>Asks a Yes / No question.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        /// <returns><c>true</c> when the user answers Yes.</returns>
        bool ConfirmYesNo(string text, string caption = "");
        /// <summary>Asks for OK / Cancel.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        /// <returns><c>true</c> when the user chooses OK.</returns>
        bool ConfirmOkCancel(string text, string caption = "");
        /// <summary>Asks a Yes / No / Cancel question, e.g. "Save changes?" before closing.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        /// <returns>The user's answer.</returns>
        ConfirmResult ConfirmYesNoCancel(string text, string caption = "");
        /// <summary>Shows an information message.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        void ShowInfo(string text, string caption = "");
        /// <summary>Shows a warning message.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        void ShowWarning(string text, string caption = "");
        /// <summary>Shows an error message.</summary>
        /// <param name="text">The message.</param>
        /// <param name="caption">The title; empty uses the configured default caption.</param>
        void ShowError(string text, string caption = "");

        // Positioned dialogs are a View-layer concern (the View knows screen coordinates), not a
        // Presenter one. View code uses AnchoredMessageBox / AnchoredToast directly. A Presenter
        // that needs position-meaningful feedback calls a small semantic view method (e.g.
        // View.ConfirmDelete()) whose Form implementation picks the anchor. Neither belongs on
        // this Presenter-facing API.
    }
}
