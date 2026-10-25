using System.Drawing;
using System.Windows.Forms;

namespace Kibinago.Services
{
    /// <summary>
    /// Which buttons a message box offers. <see cref="CancelTryContinue"/> needs .NET Core 3.0+;
    /// the native backend rejects it on .NET Framework.
    /// </summary>
    public enum MessageDialogButtons
    {
        /// <summary>OK.</summary>
        Ok,
        /// <summary>OK and Cancel.</summary>
        OkCancel,
        /// <summary>Yes and No.</summary>
        YesNo,
        /// <summary>Yes, No and Cancel.</summary>
        YesNoCancel,
        /// <summary>Retry and Cancel.</summary>
        RetryCancel,
        /// <summary>Abort, Retry and Ignore.</summary>
        AbortRetryIgnore,
        /// <summary>Cancel, Try Again and Continue (.NET Core 3.0+ only).</summary>
        CancelTryContinue
    }

    /// <summary>The icon (and system sound) of a message box.</summary>
    public enum MessageDialogIcon
    {
        /// <summary>No icon.</summary>
        None,
        /// <summary>The information icon.</summary>
        Information,
        /// <summary>The warning icon.</summary>
        Warning,
        /// <summary>The error icon.</summary>
        Error,
        /// <summary>The question-mark icon.</summary>
        Question
    }

    /// <summary>The button the user chose; <see cref="None"/> when the box closed without one.</summary>
    public enum MessageDialogResult
    {
        /// <summary>No button was chosen.</summary>
        None,
        /// <summary>OK.</summary>
        Ok,
        /// <summary>Cancel.</summary>
        Cancel,
        /// <summary>Yes.</summary>
        Yes,
        /// <summary>No.</summary>
        No,
        /// <summary>Retry.</summary>
        Retry,
        /// <summary>Abort.</summary>
        Abort,
        /// <summary>Ignore.</summary>
        Ignore,
        /// <summary>Try Again.</summary>
        TryAgain,
        /// <summary>Continue.</summary>
        Continue
    }

    /// <summary>
    /// One message box for an <see cref="IMessageDialogBackend"/> to show. Built by
    /// <see cref="Implementations.MessageService"/> (centered) and
    /// <see cref="Implementations.AnchoredMessageBox"/> (positioned).
    /// </summary>
    public sealed class MessageDialogRequest
    {
        /// <summary>The message.</summary>
        public string Text { get; set; }

        /// <summary>The title. Already resolved: an empty caller caption is replaced by
        /// <see cref="MessageDialogOptions.DefaultCaption"/>.</summary>
        public string Caption { get; set; }

        /// <summary>The buttons to offer.</summary>
        public MessageDialogButtons Buttons { get; set; }

        /// <summary>The icon to show.</summary>
        public MessageDialogIcon Icon { get; set; }

        /// <summary>Owner window for modality, or <c>null</c>.</summary>
        public IWin32Window Owner { get; set; }

        /// <summary>Screen point to place the box at (clamped on screen), or <c>null</c> to center it.
        /// A backend that cannot position its dialog may ignore it.</summary>
        public Point? Location { get; set; }
    }
}
