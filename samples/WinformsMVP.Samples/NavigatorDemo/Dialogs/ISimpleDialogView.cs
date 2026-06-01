using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface ISimpleDialogView : IWindowView
    {
        void SetMessage(string message);
    }
}
