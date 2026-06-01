using WinformsMVP.Core.Views;
using WinformsMVP.MVP.ViewActions;

namespace WinformsMVP.Samples.NavigatorDemo
{
    public interface IInputDialogView : IWindowView
    {
        void SetPrompt(string prompt);
        string GetInput();
    }
}
