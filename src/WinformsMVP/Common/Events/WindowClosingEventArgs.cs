using System;

namespace WinformsMVP.Common.Events
{
    /// <summary>
    /// Event arguments for <see cref="WinformsMVP.MVP.Views.IWindowView.Closing"/>.
    /// Presenters subscribe to that event and may set <see cref="Cancel"/> to <c>true</c>
    /// to prevent the window from closing.
    /// </summary>
    /// <remarks>
    /// This is the framework-abstracted analogue of <see cref="System.Windows.Forms.FormClosingEventArgs"/>.
    /// WinForms types are intentionally not exposed; the WinForms <c>CloseReason</c>
    /// is mapped to <see cref="CloseReason"/> by <c>WindowNavigator</c>.
    /// </remarks>
    public class WindowClosingEventArgs : EventArgs
    {
        /// <summary>
        /// Why the window is closing. Use this to decide whether to prompt the user
        /// (e.g. skip prompts on <see cref="CloseReason.SystemShutdown"/>).
        /// </summary>
        public CloseReason Reason { get; }

        private bool _cancel;

        /// <summary>
        /// Set to <c>true</c> to prevent the window from closing. Default is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Write-once veto: once set to <c>true</c> it stays <c>true</c>. The event is multicast,
        /// so this guarantees one subscriber's veto cannot be silently undone by a later subscriber.
        /// Assignments of <c>false</c> are therefore ignored.
        /// </remarks>
        public bool Cancel
        {
            get => _cancel;
            set { if (value) _cancel = true; }
        }

        public WindowClosingEventArgs(CloseReason reason)
        {
            Reason = reason;
        }
    }
}
