using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.Common.Interactions;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default <see cref="IAnchoredMessageService"/>: reads <see cref="Cursor.Position"/> at
    /// call time and delegates to <see cref="AnchoredToast"/> / <see cref="AnchoredMessageBox"/>.
    /// All WinForms types (<c>MessageBoxButtons</c>, <c>MessageBoxIcon</c>, <c>DialogResult</c>,
    /// screen coordinates) stay inside this class — the interface speaks only framework types.
    /// </summary>
    public class AnchoredMessageService : IAnchoredMessageService
    {
        public void ShowToast(string text, ToastType type, ToastOptions options)
        {
            AnchoredToast.Show(text, type, Cursor.Position, options);
        }

        public ConfirmResult ShowMessage(string text, string caption, MessageButtons buttons, MessageIcon icon)
        {
            var result = AnchoredMessageBox.Show(text, caption, Map(buttons), Map(icon), Cursor.Position);
            return Map(result);
        }

        private static MessageBoxButtons Map(MessageButtons buttons)
        {
            switch (buttons)
            {
                case MessageButtons.OkCancel: return MessageBoxButtons.OKCancel;
                case MessageButtons.YesNo: return MessageBoxButtons.YesNo;
                case MessageButtons.YesNoCancel: return MessageBoxButtons.YesNoCancel;
                default: return MessageBoxButtons.OK;
            }
        }

        private static MessageBoxIcon Map(MessageIcon icon)
        {
            switch (icon)
            {
                case MessageIcon.Information: return MessageBoxIcon.Information;
                case MessageIcon.Warning: return MessageBoxIcon.Warning;
                case MessageIcon.Error: return MessageBoxIcon.Error;
                case MessageIcon.Question: return MessageBoxIcon.Question;
                default: return MessageBoxIcon.None;
            }
        }

        // OK and Yes are both affirmative; anything else is a cancel/dismiss.
        private static ConfirmResult Map(DialogResult result)
        {
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    return ConfirmResult.Yes;
                case DialogResult.No:
                    return ConfirmResult.No;
                default:
                    return ConfirmResult.Cancel;
            }
        }
    }
}
