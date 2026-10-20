using System;
using System.Windows.Forms;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// The default <see cref="IMessageDialogBackend"/>: the native Windows MessageBox, placed at
    /// <see cref="MessageDialogRequest.Location"/> (clamped on screen) when one is given.
    /// </summary>
    public sealed class NativeMessageDialogBackend : IMessageDialogBackend
    {
        /// <inheritdoc />
        public MessageDialogResult Show(MessageDialogRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var buttons = ToWinForms(request.Buttons);
            var icon = ToWinForms(request.Icon);
            DialogResult result;
            if (request.Location.HasValue)
                result = AnchoredMessageBox.ShowNative(request.Owner, request.Text, request.Caption, buttons, icon, request.Location.Value);
            else if (request.Owner != null)
                result = MessageBox.Show(request.Owner, request.Text, request.Caption, buttons, icon);
            else
                result = MessageBox.Show(request.Text, request.Caption, buttons, icon);
            return FromWinForms(result);
        }

        internal static MessageBoxButtons ToWinForms(MessageDialogButtons buttons)
        {
            switch (buttons)
            {
                case MessageDialogButtons.OkCancel: return MessageBoxButtons.OKCancel;
                case MessageDialogButtons.YesNo: return MessageBoxButtons.YesNo;
                case MessageDialogButtons.YesNoCancel: return MessageBoxButtons.YesNoCancel;
                case MessageDialogButtons.RetryCancel: return MessageBoxButtons.RetryCancel;
                case MessageDialogButtons.AbortRetryIgnore: return MessageBoxButtons.AbortRetryIgnore;
                case MessageDialogButtons.CancelTryContinue:
#if NETFRAMEWORK
                    throw new NotSupportedException("MessageDialogButtons.CancelTryContinue needs .NET Core 3.0 or later.");
#else
                    return MessageBoxButtons.CancelTryContinue;
#endif
                default: return MessageBoxButtons.OK;
            }
        }

        internal static MessageDialogButtons FromWinForms(MessageBoxButtons buttons)
        {
            switch (buttons)
            {
                case MessageBoxButtons.OKCancel: return MessageDialogButtons.OkCancel;
                case MessageBoxButtons.YesNo: return MessageDialogButtons.YesNo;
                case MessageBoxButtons.YesNoCancel: return MessageDialogButtons.YesNoCancel;
                case MessageBoxButtons.RetryCancel: return MessageDialogButtons.RetryCancel;
                case MessageBoxButtons.AbortRetryIgnore: return MessageDialogButtons.AbortRetryIgnore;
#if !NETFRAMEWORK
                case MessageBoxButtons.CancelTryContinue: return MessageDialogButtons.CancelTryContinue;
#endif
                default: return MessageDialogButtons.Ok;
            }
        }

        internal static MessageBoxIcon ToWinForms(MessageDialogIcon icon)
        {
            switch (icon)
            {
                case MessageDialogIcon.Information: return MessageBoxIcon.Information;
                case MessageDialogIcon.Warning: return MessageBoxIcon.Warning;
                case MessageDialogIcon.Error: return MessageBoxIcon.Error;
                case MessageDialogIcon.Question: return MessageBoxIcon.Question;
                default: return MessageBoxIcon.None;
            }
        }

        // MessageBoxIcon aliases share values: Asterisk = Information, Exclamation = Warning,
        // Hand = Stop = Error.
        internal static MessageDialogIcon FromWinForms(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Information: return MessageDialogIcon.Information;
                case MessageBoxIcon.Warning: return MessageDialogIcon.Warning;
                case MessageBoxIcon.Error: return MessageDialogIcon.Error;
                case MessageBoxIcon.Question: return MessageDialogIcon.Question;
                default: return MessageDialogIcon.None;
            }
        }

        internal static MessageDialogResult FromWinForms(DialogResult result)
        {
            switch (result)
            {
                case DialogResult.OK: return MessageDialogResult.Ok;
                case DialogResult.Cancel: return MessageDialogResult.Cancel;
                case DialogResult.Yes: return MessageDialogResult.Yes;
                case DialogResult.No: return MessageDialogResult.No;
                case DialogResult.Retry: return MessageDialogResult.Retry;
                case DialogResult.Abort: return MessageDialogResult.Abort;
                case DialogResult.Ignore: return MessageDialogResult.Ignore;
#if !NETFRAMEWORK
                case DialogResult.TryAgain: return MessageDialogResult.TryAgain;
                case DialogResult.Continue: return MessageDialogResult.Continue;
#endif
                default: return MessageDialogResult.None;
            }
        }

        internal static DialogResult ToWinForms(MessageDialogResult result)
        {
            switch (result)
            {
                case MessageDialogResult.Ok: return DialogResult.OK;
                case MessageDialogResult.Cancel: return DialogResult.Cancel;
                case MessageDialogResult.Yes: return DialogResult.Yes;
                case MessageDialogResult.No: return DialogResult.No;
                case MessageDialogResult.Retry: return DialogResult.Retry;
                case MessageDialogResult.Abort: return DialogResult.Abort;
                case MessageDialogResult.Ignore: return DialogResult.Ignore;
#if !NETFRAMEWORK
                case MessageDialogResult.TryAgain: return DialogResult.TryAgain;
                case MessageDialogResult.Continue: return DialogResult.Continue;
#endif
                default: return DialogResult.None;
            }
        }
    }
}
