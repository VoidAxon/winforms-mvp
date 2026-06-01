using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface ICallbackWindowView : IWindowView
    {
        void SetMessage(string message);
        string GetText();
    }
}
