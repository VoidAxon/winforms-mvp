namespace Kibinago.Common
{
    /// <summary>
    /// Represents the result of a three-state Yes/No/Cancel confirmation dialog.
    /// Use this in place of System.Windows.Forms.DialogResult to keep Presenters free of WinForms dependencies.
    /// </summary>
    public enum ConfirmResult
    {
        /// <summary>The user answered Yes.</summary>
        Yes,
        /// <summary>The user answered No.</summary>
        No,
        /// <summary>The user cancelled (Cancel button, Esc or the dialog's X).</summary>
        Cancel
    }
}
