using System.Windows.Forms;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Marker for views that represent top-level windows (Forms / dialogs). Adds nothing of its
    /// own beyond <see cref="IWin32Window"/> — a window has a Win32 handle and can therefore own a
    /// modal dialog (it can be passed as the <c>owner</c> of <c>IWindowNavigator.ShowWindowAsModal</c>).
    /// </summary>
    /// <remarks>
    /// Closing is NOT a View concern. The framework drives it through the Presenter:
    /// override <c>CanClose(CloseReason)</c> to veto (Pull), call <c>RequestClose(...)</c> to
    /// close actively (Push). A Form implementing this interface writes zero closing code, and no
    /// lifecycle/activation plumbing either — those are handled on the concrete Form by the framework.
    /// </remarks>
    public interface IWindowView : IActionableView, IWin32Window
    {
    }
}
