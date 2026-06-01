using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    public interface IViewAttacher<in TView> where TView : IViewBase
    {
        void AttachView(TView view);
    }
}
