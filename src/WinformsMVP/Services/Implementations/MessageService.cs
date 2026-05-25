using System.Drawing;
using System.Windows.Forms;
using WinformsMVP.Common;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Default implementation of IMessageService using WinForms MessageBox and custom dialogs.
    /// Uses configurable defaults from DialogDefaults class.
    /// </summary>
    public class MessageService : IMessageService
    {
        #region Standard Message Dialogs

        public bool ConfirmOkCancel(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            return MessageBox.Show(text, caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        public bool ConfirmYesNo(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            return MessageBox.Show(text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            var result = MessageBox.Show(text, caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            return MapToConfirmResult(result);
        }

        public void ShowError(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowInfo(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void ShowWarning(string text, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion

        #region Positioned Message Dialogs (using Windows API Hook)

        public bool ConfirmOkCancelAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            return PositionableMessageBox.Show(text, caption, MessageBoxButtons.OKCancel, MessageBoxIcon.Question, location) == DialogResult.OK;
        }

        public bool ConfirmYesNoAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            return PositionableMessageBox.Show(text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, location) == DialogResult.Yes;
        }

        public ConfirmResult ConfirmYesNoCancelAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            var result = PositionableMessageBox.Show(text, caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, location);
            return MapToConfirmResult(result);
        }

        public void ShowErrorAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            PositionableMessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error, location);
        }

        public void ShowInfoAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            PositionableMessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information, location);
        }

        public void ShowWarningAt(string text, Point location, string caption = "")
        {
            caption = string.IsNullOrEmpty(caption) ? DialogDefaults.DefaultMessageCaption : caption;
            PositionableMessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning, location);
        }

        #endregion

        #region Toast Notifications

        public void ShowToast(string text, ToastType type, int duration = 3000)
        {
            var toast = new ToastNotification(text, type, duration);
            toast.Show();
        }

        #endregion

        private static ConfirmResult MapToConfirmResult(DialogResult result)
        {
            switch (result)
            {
                case DialogResult.Yes: return ConfirmResult.Yes;
                case DialogResult.No: return ConfirmResult.No;
                default: return ConfirmResult.Cancel;
            }
        }
    }
}
