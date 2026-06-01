using WinformsMVP.MVP.Presenters;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.MVP.Presenters
{
    public abstract class PresenterBase<TView, TParameters> : PresenterBase<TView> where TView : IViewBase
    {
        public abstract void Initialize(TParameters parameters);
    }
}
