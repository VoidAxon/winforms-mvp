using System.Drawing;
using WinformsMVP.Common;
using WinformsMVP.Services;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Anchored feedback as view behavior: <c>View.ShowToast(...)</c>,
    /// <c>View.ConfirmYesNo(...)</c>. Each method mirrors <see cref="IAnchoredMessageService"/>
    /// in two forms — without a <see cref="Point"/> (anchored at the cursor at call time) and
    /// with a <see cref="Point"/> (caller-supplied screen anchor).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Intended callers per form: a <b>Presenter</b> uses the cursor forms only ("tell the view
    /// to give feedback where the user just clicked" — no coordinates, no controls). The
    /// <see cref="Point"/> forms are for <b>View-layer code</b> (a Form calling
    /// <c>this.ShowToast(...)</c> with a control's bounds, a hit-test result, ...), which owns
    /// screen coordinates. Passing a <see cref="Point"/> from a Presenter violates the MVP
    /// layering by convention, even though the type itself is plain <c>System.Drawing</c> data.
    /// </para>
    /// <para>
    /// These extensions resolve <see cref="IAnchoredMessageService"/> from the GLOBAL
    /// <see cref="ServiceLocator.Current"/> (a static method cannot see a per-presenter provider
    /// injected via <c>SetServiceProvider</c>). Tests that assert anchored feedback register a
    /// mock service via <c>ServiceLocator.Configure</c> inside the <c>"ServiceLocator"</c> test
    /// collection and call <c>ServiceLocator.Reset()</c> afterwards.
    /// </para>
    /// <para>
    /// For the cursor forms the anchor is the cursor position at call time — call synchronously
    /// inside the event/action handler. After an <c>await</c> use <c>Messages.ShowToast</c>
    /// instead.
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

        /// <summary>Shows a toast anchored at the cursor.</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, ToastOptions options = null)
            => Service().ShowToast(text, type, options);

        /// <summary>Shows a toast anchored at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowToast(this IViewBase view, string text, ToastType type, Point anchor, ToastOptions options = null)
            => Service().ShowToast(text, type, anchor, options);

        /// <summary>Shows an anchored information message at the cursor.</summary>
        public static void ShowInfo(this IViewBase view, string text, string caption = "")
            => Service().ShowInfo(text, caption);

        /// <summary>Shows an anchored information message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowInfo(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowInfo(text, anchor, caption);

        /// <summary>Shows an anchored warning message at the cursor.</summary>
        public static void ShowWarning(this IViewBase view, string text, string caption = "")
            => Service().ShowWarning(text, caption);

        /// <summary>Shows an anchored warning message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowWarning(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowWarning(text, anchor, caption);

        /// <summary>Shows an anchored error message at the cursor.</summary>
        public static void ShowError(this IViewBase view, string text, string caption = "")
            => Service().ShowError(text, caption);

        /// <summary>Shows an anchored error message at <paramref name="anchor"/> (View-layer callers).</summary>
        public static void ShowError(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ShowError(text, anchor, caption);

        /// <summary>Anchored Yes/No confirmation at the cursor. True when the user chose Yes.</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNo(text, caption);

        /// <summary>Anchored Yes/No confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static bool ConfirmYesNo(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmYesNo(text, anchor, caption);

        /// <summary>Anchored OK/Cancel confirmation at the cursor. True when the user chose OK.</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmOkCancel(text, caption);

        /// <summary>Anchored OK/Cancel confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static bool ConfirmOkCancel(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmOkCancel(text, anchor, caption);

        /// <summary>Anchored Yes/No/Cancel confirmation at the cursor. Returns the raw result.</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IViewBase view, string text, string caption = "")
            => Service().ConfirmYesNoCancel(text, caption);

        /// <summary>Anchored Yes/No/Cancel confirmation at <paramref name="anchor"/> (View-layer callers).</summary>
        public static ConfirmResult ConfirmYesNoCancel(this IViewBase view, string text, Point anchor, string caption = "")
            => Service().ConfirmYesNoCancel(text, anchor, caption);
    }
}
