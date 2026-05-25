namespace WinformsMVP.Common
{
    /// <summary>
    /// Framework-level abstraction of the reason a window is closing.
    /// Maps from WinForms <see cref="System.Windows.Forms.CloseReason"/> to avoid leaking
    /// WinForms types into Presenter code.
    /// </summary>
    /// <remarks>
    /// Presenters subscribed to <see cref="WinformsMVP.Core.Views.IWindowView.Closing"/>
    /// should inspect this value before deciding whether to prompt for unsaved changes:
    /// <list type="bullet">
    ///   <item><description><see cref="Normal"/>: standard close path — perform dirty-data checks here.</description></item>
    ///   <item><description><see cref="SystemShutdown"/> / <see cref="TaskManager"/>: do not prompt; let the process exit.</description></item>
    ///   <item><description><see cref="ParentClosing"/>: usually pass through silently.</description></item>
    /// </list>
    /// </remarks>
    public enum CloseReason
    {
        /// <summary>
        /// Standard close: user clicked the X button, pressed Alt+F4, or the Presenter
        /// triggered <c>CloseRequested</c>. This is the path where dirty-data prompts belong.
        /// </summary>
        Normal,

        /// <summary>
        /// Windows is shutting down. Do not block.
        /// </summary>
        SystemShutdown,

        /// <summary>
        /// Task Manager (or equivalent) is forcing the process to terminate. Do not block.
        /// </summary>
        TaskManager,

        /// <summary>
        /// The owner window is closing, causing this child window to close as well.
        /// </summary>
        ParentClosing,

        /// <summary>
        /// Reason could not be determined.
        /// </summary>
        Unknown,
    }
}
