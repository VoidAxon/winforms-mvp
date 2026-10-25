using System;
using Kibinago.Common;

namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// Default implementation of <see cref="IMessageService"/>: turns each call into a
    /// <see cref="MessageDialogRequest"/> (filling in <see cref="MessageDialogOptions.DefaultCaption"/>)
    /// and lets the <see cref="IMessageDialogBackend"/> show it. To change how message boxes look,
    /// replace the backend (<c>mvp.Messages.UseDialogs(...)</c>), not this service.
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IMessageDialogBackend _backend;
        private readonly MessageDialogOptions _options;

        /// <param name="backend">Shows the dialogs.</param>
        /// <param name="options">Settings; copied here (later edits have no effect).</param>
        public MessageService(IMessageDialogBackend backend, MessageDialogOptions options)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options.Copy();
        }

        /// <inheritdoc />
        public bool ConfirmOkCancel(string text, string caption = "")
            => Show(text, caption, MessageDialogButtons.OkCancel, MessageDialogIcon.Question) == MessageDialogResult.Ok;

        /// <inheritdoc />
        public bool ConfirmYesNo(string text, string caption = "")
            => Show(text, caption, MessageDialogButtons.YesNo, MessageDialogIcon.Question) == MessageDialogResult.Yes;

        /// <inheritdoc />
        public ConfirmResult ConfirmYesNoCancel(string text, string caption = "")
        {
            switch (Show(text, caption, MessageDialogButtons.YesNoCancel, MessageDialogIcon.Question))
            {
                case MessageDialogResult.Yes: return ConfirmResult.Yes;
                case MessageDialogResult.No: return ConfirmResult.No;
                default: return ConfirmResult.Cancel;
            }
        }

        /// <inheritdoc />
        public void ShowError(string text, string caption = "")
            => Show(text, caption, MessageDialogButtons.Ok, MessageDialogIcon.Error);

        /// <inheritdoc />
        public void ShowInfo(string text, string caption = "")
            => Show(text, caption, MessageDialogButtons.Ok, MessageDialogIcon.Information);

        /// <inheritdoc />
        public void ShowWarning(string text, string caption = "")
            => Show(text, caption, MessageDialogButtons.Ok, MessageDialogIcon.Warning);

        private MessageDialogResult Show(string text, string caption, MessageDialogButtons buttons, MessageDialogIcon icon)
            => _backend.Show(new MessageDialogRequest
            {
                Text = text,
                Caption = string.IsNullOrEmpty(caption) ? _options.DefaultCaption : caption,
                Buttons = buttons,
                Icon = icon,
            });
    }
}
