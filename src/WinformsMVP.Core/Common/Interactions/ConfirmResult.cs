namespace WinformsMVP.Common
{
    /// <summary>
    /// Represents the result of a three-state Yes/No/Cancel confirmation dialog.
    /// Use this in place of System.Windows.Forms.DialogResult to keep Presenters free of WinForms dependencies.
    /// </summary>
    public enum ConfirmResult
    {
        Yes,
        No,
        Cancel
    }
}
