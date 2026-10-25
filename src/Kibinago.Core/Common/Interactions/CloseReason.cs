namespace Kibinago.Common
{
    /// <summary>
    /// Framework-level abstraction of the reason a window is closing.
    /// Maps from WinForms <c>System.Windows.Forms.CloseReason</c> to avoid leaking
    /// WinForms types into Presenter code.
    /// </summary>
    /// <remarks>
    /// Presenters that override <c>CanClose(CloseReason)</c> should inspect this value before
    /// deciding whether to veto the close:
    /// <list type="bullet">
    ///   <item><description><see cref="Normal"/>: standard close path — perform dirty-data checks here.</description></item>
    ///   <item><description><see cref="SystemShutdown"/> / <see cref="TaskManager"/>: never reach the presenter;
    ///     the framework allows them without asking.</description></item>
    ///   <item><description><see cref="ParentClosing"/> / <see cref="Unknown"/>: reach the presenter like
    ///     <see cref="Normal"/> — prompt for unsaved changes there too, or the owner's close loses them.</description></item>
    /// </list>
    /// </remarks>
    public enum CloseReason
    {
        /// <summary>
        /// Standard close: user clicked the X button, pressed Alt+F4, or the Presenter
        /// called <c>RequestClose</c>. This is the path where dirty-data prompts belong.
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
