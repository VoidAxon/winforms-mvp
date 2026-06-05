using System.Windows.Forms;

namespace WinformsMVP.MVP.Views
{
    /// <summary>
    /// Interface for views that represent top-level windows (Forms / dialogs).
    /// </summary>
    /// <remarks>
    /// Closing is NOT a View concern. The framework drives it through the Presenter:
    /// override <c>CanClose(CloseReason)</c> to veto (Pull), call <c>RequestClose(...)</c> to
    /// close actively (Push). A Form implementing this interface writes zero closing code.
    /// </remarks>
    public interface IWindowView : IActionableView, IWin32Window
    {
        bool IsDisposed { get; }
        void Activate();
    }
}
