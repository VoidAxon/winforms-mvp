namespace Kibinago.Services
{
    /// <summary>
    /// Shows one message box and returns the user's choice. This is the single place that decides
    /// what message boxes look like: <see cref="MessageService"/> (presenter
    /// <c>Messages</c>) and <see cref="AnchoredMessageBox"/> both go through it.
    /// Replace it (<c>mvp.Messages.UseDialogs(...)</c>) to show company-styled dialogs; the default,
    /// <see cref="NativeMessageDialogBackend"/>, uses the native MessageBox.
    /// </summary>
    /// <remarks>Called on the UI thread; the call blocks until the dialog closes.</remarks>
    public interface IMessageDialogBackend
    {
        /// <summary>Shows <paramref name="request"/> modally and returns the chosen button.</summary>
        MessageDialogResult Show(MessageDialogRequest request);
    }
}
