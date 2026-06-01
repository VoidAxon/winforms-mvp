using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface INonModalWindowView : IWindowView
    {
        void SetCounter(int value);
    }
}
