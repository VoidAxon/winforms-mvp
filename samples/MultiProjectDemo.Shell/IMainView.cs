using WinformsMVP.MVP.Views;

namespace MultiProjectDemo.Shell
{
    public interface IMainView : IWindowView
    {
        void RequestExit();
    }
}
