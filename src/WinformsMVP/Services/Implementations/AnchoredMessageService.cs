using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IAnchoredMessageService"/>: the cursor-anchored overloads read
    /// <see cref="Cursor.Position"/> at call time and forward to the <see cref="Point"/>
    /// overloads, which delegate to <see cref="AnchoredToast"/> / <see cref="AnchoredMessageBox"/>.
    /// WinForms dialog types (<c>MessageBoxButtons</c>, <c>MessageBoxIcon</c>,
    /// <c>DialogResult</c>) stay inside this class.
    /// </summary>
    public class AnchoredMessageService : IAnchoredMessageService
    {
        public void ShowToast(string text, ToastType type, ToastOptions options = null)
            => ShowToast(text, type, Cursor.Position, options);

        public void ShowToast(string text, ToastType type, Point anchor, ToastOptions options = null)
            => AnchoredToast.Show(text, type, anchor, options);

        public void ShowInfo(string text, string caption = "")
            => ShowInfo(text, Cursor.Position, caption);

        public void ShowInfo(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowInfo(text, anchor, caption);

        public void ShowWarning(string text, string caption = "")
            => ShowWarning(text, Cursor.Position, caption);

        public void ShowWarning(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowWarning(text, anchor, caption);

        public void ShowError(string text, string caption = "")
            => ShowError(text, Cursor.Position, caption);

        public void ShowError(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ShowError(text, anchor, caption);

        public bool ConfirmYesNo(string text, string caption = "")
            => ConfirmYesNo(text, Cursor.Position, caption);

        public bool ConfirmYesNo(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ConfirmYesNo(text, anchor, caption);

        public bool ConfirmOkCancel(string text, string caption = "")
            => ConfirmOkCancel(text, Cursor.Position, caption);

        public bool ConfirmOkCancel(string text, Point anchor, string caption = "")
            => AnchoredMessageBox.ConfirmOkCancel(text, anchor, caption);

        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
            => ConfirmYesNoCancel(text, Cursor.Position, caption);

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
    }
}
