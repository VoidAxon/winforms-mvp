using WinformsMVP.MVP.ViewActions;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Samples.AnchoredMessageDemo
{
    public static class AnchoredMessageDemoActions
    {
        private static readonly ViewActionFactory Factory =
            ViewAction.Factory.WithQualifier("AnchoredMessageDemo");

        public static readonly ViewAction Save = Factory.Create("Save");
        public static readonly ViewAction Delete = Factory.Create("Delete");
        public static readonly ViewAction GridTouch = Factory.Create("GridTouch");
        public static readonly ViewAction MenuNotify = Factory.Create("MenuNotify");
        public static readonly ViewAction ToggleNotify = Factory.Create("ToggleNotify");
    }

    /// <summary>
    /// View for the anchored-message demo, showing the division of labor for user feedback:
    /// most feedback goes through <c>Messages</c> (corner toast / centered dialogs — always
    /// correct, regardless of how the action was triggered); when the feedback position carries
    /// meaning, the view exposes a small <b>semantic method</b> and its implementation picks the
    /// anchor (using the <c>AnchoredToast</c> / <c>AnchoredMessageBox</c> view-layer utilities).
    /// The presenter never sees a coordinate.
    /// </summary>
    public interface IAnchoredMessageDemoView : IWindowView
    {
        /// <summary>The item name being edited (data property — no TextBox exposed).</summary>
        string ItemName { get; }

        /// <summary>Whether the notifications option is checked.</summary>
        bool NotificationsEnabled { get; }

        void ShowHint(string message);

        /// <summary>
        /// Asks the user to confirm the deletion. The view decides where the dialog appears
        /// (anchored at the Delete button) — correct for mouse, mnemonic, and keyboard alike,
        /// because the anchor is explicit instead of inferred.
        /// </summary>
        bool ConfirmDelete();

        /// <summary>
        /// Position-meaningful feedback: a toast anchored at the grid's current row.
        /// </summary>
        void ShowRowTouched();
    }
}
