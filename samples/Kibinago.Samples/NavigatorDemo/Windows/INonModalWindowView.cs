using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public interface INonModalWindowView : IWindowView
    {
        void SetCounter(int value);
    }
}
