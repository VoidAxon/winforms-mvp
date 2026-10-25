using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public interface IInputDialogView : IWindowView
    {
        void SetPrompt(string prompt);
        string GetInput();
    }
}
