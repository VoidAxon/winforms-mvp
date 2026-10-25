using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public interface ISingletonWindowView : IWindowView
    {
        void SetInstanceId(int id);
        void SetMessage(string message);
    }
}
