using System.Windows.Forms;

namespace WinformsMVP.Core.Views
{
    public interface IWindowView : IActionableView, IWin32Window
    {
        bool IsDisposed { get; }
        void Activate();
    }
}
