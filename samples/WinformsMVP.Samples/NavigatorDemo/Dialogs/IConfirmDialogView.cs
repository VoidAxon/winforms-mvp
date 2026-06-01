using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface IConfirmDialogView : IWindowView
    {
        void SetTitle(string title);
        void SetMessage(string message);
        void SetDefaultChoice(bool yesIsDefault);  // Rule 13: Domain naming, not UI control naming
    }
}
