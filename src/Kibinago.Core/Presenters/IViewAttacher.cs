using Kibinago.Views;

namespace Kibinago.Presenters
{
    public interface IViewAttacher<in TView> where TView : IViewBase
    {
        void AttachView(TView view);
    }
}
