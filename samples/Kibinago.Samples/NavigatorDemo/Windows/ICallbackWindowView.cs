using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public interface ICallbackWindowView : IWindowView
    {
        void SetMessage(string message);
        string GetText();
    }
}
