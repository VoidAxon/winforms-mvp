namespace WinformsMVP.Common
{
    /// <summary>
    /// Framework-level button set for message dialogs, so Presenter-facing APIs never expose
    /// the WinForms <c>MessageBoxButtons</c> type. Mapped to the platform type inside the
    /// service implementation only.
    /// </summary>
    public enum MessageButtons
    {
        Ok,
        OkCancel,
        YesNo,
        YesNoCancel
    }
}
