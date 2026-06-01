using WinformsMVP.Core.Views;

namespace MultiProjectDemo.Shell
{
    public interface IMainView : IWindowView
    {
        void RequestExit();
    }
}
