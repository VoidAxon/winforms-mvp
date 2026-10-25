using Kibinago.Views;
using Kibinago.ViewActions;

namespace Kibinago.Samples.NavigatorDemo
{
    public interface ISimpleDialogView : IWindowView
    {
        void SetMessage(string message);
    }
}
