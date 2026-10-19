namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Marker for views that represent top-level windows (Forms / dialogs). Carries no members of
    /// its own and no WinForms types — closing is the Presenter's concern (override
    /// <c>CanClose(CloseReason)</c> / call <c>RequestClose(...)</c>), and lifecycle/activation are
    /// handled on the concrete Form by the framework. An <see cref="IWindowView"/> may be passed as
    /// the <c>owner</c> of a modal dialog opened through <c>IWindowNavigator</c>; the navigator
    /// resolves it to the underlying window at runtime.
    /// </summary>
    public interface IWindowView : IActionableView
    {
    }
}
