using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IAnchoredMessageService"/>: the anchor-free overloads resolve the
    /// <b>interaction point</b> at call time (see <see cref="ResolveAnchor"/>) and forward to the
    /// <see cref="Point"/> overloads, which delegate to <see cref="AnchoredToast"/> /
    /// <see cref="AnchoredMessageBox"/>. WinForms dialog types (<c>MessageBoxButtons</c>,
    /// <c>MessageBoxIcon</c>, <c>DialogResult</c>) stay inside this class.
    /// </summary>
    public class AnchoredMessageService : IAnchoredMessageService
    {
        public void ShowToast(string text, ToastType type, ToastOptions options = null)
            => ShowToast(text, type, ResolveAnchor(), options);

        public void ShowToast(string text, ToastType type, Point anchor, ToastOptions options = null)
            => AnchoredToast.Show(text, type, anchor, options);

        public void ShowInfo(string text, string caption = "")
            => ShowInfo(text, ResolveAnchor(), caption);

        public void ShowInfo(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowInfo(text, anchor, caption);

        public void ShowWarning(string text, string caption = "")
            => ShowWarning(text, ResolveAnchor(), caption);

        public void ShowWarning(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowWarning(text, anchor, caption);

        public void ShowError(string text, string caption = "")
            => ShowError(text, ResolveAnchor(), caption);

        public void ShowError(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowError(text, anchor, caption);

        public bool ConfirmYesNo(string text, string caption = "")
            => ConfirmYesNo(text, ResolveAnchor(), caption);

        public bool ConfirmYesNo(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ConfirmYesNo(text, anchor, caption);

        public bool ConfirmOkCancel(string text, string caption = "")
            => ConfirmOkCancel(text, ResolveAnchor(), caption);

        public bool ConfirmOkCancel(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ConfirmOkCancel(text, anchor, caption);

        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
            => ConfirmYesNoCancel(text, ResolveAnchor(), caption);

        public ConfirmResult ConfirmYesNoCancel(string text, Point anchor, string caption = "")
        {
            var result = AnchoredMessageBox.Show(
                text, caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, anchor);
            switch (result)
            {
                case DialogResult.Yes: return ConfirmResult.Yes;
                case DialogResult.No: return ConfirmResult.No;
                default: return ConfirmResult.Cancel;
            }
        }

        /// <summary>
        /// Resolves the interaction point for the anchor-free overloads, following the same
        /// convention Windows uses for keyboard-invoked context menus (WM_CONTEXTMENU): mouse
        /// input anchors at the exact cursor point; keyboard input anchors at the focused
        /// control; with no usable focus information it falls back to the center of the active
        /// window, then of the screen (the default MessageBox placement).
        /// </summary>
        /// <remarks>
        /// Mouse vs keyboard is inferred geometrically: a mouse click is always delivered to the
        /// window under the cursor and the handler runs synchronously during it, so the cursor
        /// being inside the active window's bounds means mouse (or a harmless equivalent: the
        /// toast lands under the parked pointer, in plain sight); the cursor being outside means
        /// the trigger cannot have been a mouse click. The inference is self-correcting for the
        /// common case because clicking a focusable control also makes it the focused control —
        /// both branches then point at the same place.
        /// </remarks>
        private static Point ResolveAnchor()
        {
            var cursor = Cursor.Position;
            var active = Form.ActiveForm;
            if (active == null)
            {
                return ResolveAnchorCore(cursor, null, null, Screen.FromPoint(cursor).WorkingArea);
            }

            var focused = InnermostActiveControl(active);
            Rectangle? focusedBounds = focused != null
                ? focused.RectangleToScreen(focused.ClientRectangle)
                : (Rectangle?)null;
            return ResolveAnchorCore(cursor, active.Bounds, focusedBounds, Screen.FromPoint(cursor).WorkingArea);
        }

        /// <summary>
        /// Pure interaction-point decision (unit-tested; all inputs injected):
        /// cursor inside the active window → the cursor; otherwise the focused control's
        /// bottom-left; otherwise the window center; with no active window, the screen center.
        /// </summary>
        internal static Point ResolveAnchorCore(
            Point cursor,
            Rectangle? activeWindowBounds,
            Rectangle? focusedControlBounds,
            Rectangle screenWorkingArea)
        {
            if (activeWindowBounds == null)
            {
                return Center(screenWorkingArea);
            }

            if (activeWindowBounds.Value.Contains(cursor))
            {
                return cursor;
            }

            if (focusedControlBounds != null)
            {
                var r = focusedControlBounds.Value;
                return new Point(r.Left, r.Bottom);
            }

            return Center(activeWindowBounds.Value);
        }

        /// <summary>ActiveControl can be a container (Panel/UserControl); walk down to the leaf
        /// control that really holds the focus.</summary>
        private static Control InnermostActiveControl(ContainerControl container)
        {
            Control current = container.ActiveControl;
            while (current is ContainerControl cc && cc.ActiveControl != null)
            {
                current = cc.ActiveControl;
            }
            return current;
        }

        private static Point Center(Rectangle area)
            => new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
    }
}
