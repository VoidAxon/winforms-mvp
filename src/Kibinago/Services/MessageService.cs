using System;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Kibinago.Common;

namespace Kibinago.Services
{
    /// <summary>
    /// Default implementation of <see cref="IMessageService"/>: turns each call into a
    /// <see cref="MessageDialogRequest"/> (filling in <see cref="MessageDialogOptions.DefaultCaption"/>)
    /// and lets the <see cref="IMessageDialogBackend"/> show it. To change how message boxes look,
    /// replace the backend (<c>mvp.Messages.UseDialogs(...)</c>), not this service.
    /// </summary>
    /// <remarks>
    /// Called from a worker thread (code inside <c>Task.Run</c>), the dialog is shown on the UI thread
    /// (through an open form, with <c>Invoke</c>), so it is modal to the application's windows as usual,
    /// and the worker waits for the answer — the backend always runs on the UI thread. Do not block the
    /// UI thread on such a worker (<c>task.Wait()</c> / <c>.Result</c>): it would wait for a dialog that
    /// cannot open (await it instead). With no form open, the dialog is shown on the calling thread.
    /// </remarks>
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

        /// <summary>
        /// Finds an open form whose thread is the UI thread. Replaceable so tests are not affected by
        /// forms other tests have open (<see cref="Application.OpenForms"/> is process-wide).
        /// </summary>
        internal Func<Form> OpenFormLocator { get; set; } = OpenForms.FindOne;

        private MessageDialogResult Show(string text, string caption, MessageDialogButtons buttons, MessageDialogIcon icon)
        {
            var request = new MessageDialogRequest
            {
                Text = text,
                Caption = string.IsNullOrEmpty(caption) ? _options.DefaultCaption : caption,
                Buttons = buttons,
                Icon = icon,
            };

            // On a UI thread (this thread runs a message loop — in an app with several UI threads, the
            // caller's), or with no UI thread to go to: show here. From a worker thread: show on the UI
            // thread, so the dialog is modal to the application's windows, and wait for the answer.
            if (Application.MessageLoop) return _backend.Show(request);
            var host = OpenFormLocator();
            if (host == null || !host.InvokeRequired) return _backend.Show(request);

            // The backend's own failure is carried out of Invoke, so whatever Invoke itself throws means
            // the call never reached the UI thread (the form closed meanwhile): show here instead.
            var result = MessageDialogResult.None;
            Exception backendFailure = null;
            bool reached = false;
            try
            {
                host.Invoke(new Action(() =>
                {
                    reached = true;
                    try { result = _backend.Show(request); }
                    catch (Exception ex) { backendFailure = ex; }
                }));
            }
            catch (Exception ex) when (!reached && (ex is ObjectDisposedException || ex is InvalidOperationException))
            {
                return _backend.Show(request);
            }
            if (backendFailure != null) ExceptionDispatchInfo.Capture(backendFailure).Throw();
            return result;
        }
    }
}
