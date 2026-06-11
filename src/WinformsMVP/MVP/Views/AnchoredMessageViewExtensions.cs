using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Cursor-anchored feedback as view behavior: <c>View.ShowToast(...)</c>,
    /// <c>View.ConfirmYesNo(...)</c>. From the presenter's point of view this is "tell the view
    /// to give feedback where the user just clicked" — no coordinates, no controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These extensions resolve <see cref="IAnchoredMessageService"/> from the GLOBAL
    /// <see cref="ServiceLocator.Current"/> (a static method cannot see a per-presenter provider
    /// injected via <c>SetServiceProvider</c>). Tests that assert anchored feedback register a
    /// mock service via <c>ServiceLocator.Configure</c> inside the <c>"ServiceLocator"</c> test
    /// collection and call <c>ServiceLocator.Reset()</c> afterwards.
    /// </para>
    /// <para>
    /// The anchor is the cursor position at call time — call synchronously inside the
    /// event/action handler. After an <c>await</c> use <c>Messages.ShowToast</c> instead.
    /// </para>
    /// <para>
    /// The <paramref name="view"/> receiver is not used by the current implementation; it exists
    /// so the call site reads as view behavior and to allow a future implementation to use the
    /// view (owner window, screen context) without changing call sites.
    /// </para>
    /// </remarks>
    public static class AnchoredMessageViewExtensions
    {
        private static IAnchoredMessageService Service()
            => ServiceLocator.Current.ResolveRequired<IAnchoredMessageService>();

        /// <summary>Shows a toast anchored at the cursor (full-parameter form).</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, ToastOptions options)
            => Service().ShowToast(text, type, options);

        /// <summary>Shows a toast anchored at the cursor with default options.</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type)
            => Service().ShowToast(text, type, null);

        /// <summary>Shows an anchored message box (full-parameter form).</summary>
        public static ConfirmResult ShowMessage(this IViewBase view, string text, string caption, MessageButtons buttons, MessageIcon icon)
            => Service().ShowMessage(text, caption, buttons, icon);

        /// <summary>Shows an anchored information message.</summary>
        public static void ShowInfo(this IViewBase view, string text, string caption = "")
            => Service().ShowInfo(text, caption);

        /// <summary>Shows an anchored warning message.</summary>
        public static void ShowWarning(this IViewBase view, string text, string caption = "")
            => Service().ShowWarning(text, caption);

        /// <summary>Shows an anchored error message.</summary>
        public static void ShowError(this IViewBase view, string text, string caption = "")
            => Service().ShowError(text, caption);

        /// <summary>Anchored Yes/No confirmation. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNo(text, caption);

        /// <summary>Anchored OK/Cancel confirmation. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmOkCancel(text, caption);

        /// <summary>Anchored Yes/No/Cancel confirmation. Returns the raw result.</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNoCancel(text, caption);
    }
}
