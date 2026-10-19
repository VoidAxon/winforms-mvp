using System.Threading;
using System.Threading.Tasks;
using WinformsMVP.MVP.Presenters;

namespace MvpNamespace
{
    /// <summary>
    /// The control's use cases. The parent (Form or its presenter's view code) hosts it with
    /// <c>new SamplePresenter().Connect(sampleControl)</c>; the presenter is disposed with the control.
    /// Business services go into the constructor.
    /// </summary>
    public sealed class SamplePresenter : ControlPresenterBase<ISampleView>
    {
        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(SampleActions.Refresh, OnRefreshAsync);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
            => OnRefreshAsync(cancellationToken);

        private async Task OnRefreshAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;   // load data here, passing cancellationToken
            View.Summary = "Loaded";
        }
    }
}
