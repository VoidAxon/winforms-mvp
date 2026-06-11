namespace WinformsMVP.Common
{
    /// <summary>
    /// Framework-level icon kind for message dialogs, so Presenter-facing APIs never expose
    /// the WinForms <c>MessageBoxIcon</c> type. Mapped to the platform type inside the
    /// service implementation only.
    /// </summary>
    public enum MessageIcon
    {
        None,
        Information,
        Warning,
        Error,
        Question
    }
}
