using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface ISingletonWindowView : IWindowView
    {
        void SetInstanceId(int id);
        void SetMessage(string message);
    }
}
