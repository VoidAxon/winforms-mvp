using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Anchored feedback as view behavior: <c>View.ShowToast(...)</c>,
    /// <c>View.ConfirmYesNo(...)</c>. Each method mirrors <see cref="IAnchoredMessageService"/>
    /// in two forms — without a <see cref="Point"/> (anchored at the interaction point: the
    /// click point for mouse input, the focused control for keyboard input) and with a
    /// <see cref="Point"/> (caller-supplied screen anchor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One simple rule: <b>Presenters use the cursor forms</b> ("feedback where the user just
    /// clicked" — no coordinates involved); <b>the Point forms are for View-layer code</b> that
    /// picks its own anchor (a Form calling <c>this.ShowToast(...)</c> with a control's bounds,
    /// a hit-test result, ...). For the rare case where feedback depends on a business outcome
    /// but must appear at a view-specific spot, give the view one small semantic method (e.g.
    /// <c>ShowNameError(message)</c>) and let its implementation pick the anchor.
    /// </para>
    /// <para>
    /// These extensions resolve <see cref="IAnchoredMessageService"/> from the GLOBAL
    /// <see cref="ServiceLocator.Current"/> (a static method cannot see a per-presenter provider
    /// injected via <c>SetServiceProvider</c>). Tests that assert anchored feedback register a
    /// mock service via <c>ServiceLocator.Configure</c> inside the <c>"ServiceLocator"</c> test
    /// collection and call <c>ServiceLocator.Reset()</c> afterwards.
    /// </para>
    /// <para>
    /// The anchor-free forms resolve the interaction point at call time (mouse → click point,
    /// keyboard → focused control, fallback → window/screen center — the Windows context-menu
    /// convention). Call them synchronously inside the event/action handler; after an
    /// <c>await</c> use <c>Messages.ShowToast</c> instead.
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

        /// <summary>Shows a toast anchored at the interaction point.</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, ToastOptions options = null)
            => Service().ShowToast(text, type, options);

        /// <summary>Shows a toast anchored at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, Point anchor, ToastOptions options = null)
            => Service().ShowToast(text, type, anchor, options);

        /// <summary>Shows an anchored information message at the interaction point.</summary>
        public static void ShowInfo(this IViewBase view, string text, string caption = "")
            => Service().ShowInfo(text, caption);

        /// <summary>Shows an anchored information message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowInfo(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowInfo(text, anchor, caption);

        /// <summary>Shows an anchored warning message at the interaction point.</summary>
        public static void ShowWarning(this IViewBase view, string text, string caption = "")
            => Service().ShowWarning(text, caption);

        /// <summary>Shows an anchored warning message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowWarning(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowWarning(text, anchor, caption);

        /// <summary>Shows an anchored error message at the interaction point.</summary>
        public static void ShowError(this IViewBase view, string text, string caption = "")
            => Service().ShowError(text, caption);

        /// <summary>Shows an anchored error message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowError(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowError(text, anchor, caption);

        /// <summary>Anchored Yes/No confirmation at the interaction point. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNo(text, caption);

        /// <summary>Anchored Yes/No confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmYesNo(text, anchor, caption);

        /// <summary>Anchored OK/Cancel confirmation at the interaction point. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmOkCancel(text, caption);

        /// <summary>Anchored OK/Cancel confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmOkCancel(text, anchor, caption);

        /// <summary>Anchored Yes/No/Cancel confirmation at the interaction point. Returns the raw result.</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNoCancel(text, caption);

        /// <summary>Anchored Yes/No/Cancel confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmYesNoCancel(text, anchor, caption);
    }
}
