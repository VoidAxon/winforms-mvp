using System.Threading;
using System.Threading.Tasks;
using Kibinago.Presenters;

namespace MvpNamespace
{
    /// <summary>
    /// The control's use cases; business services go into the constructor. In an MVP window the parent
    /// view interface exposes <see cref="ISampleView"/> and the parent presenter attaches this one in its
    /// OnInitialize: <c>AttachChild(new SamplePresenter(), View.Sample)</c> (disposed with the parent).
    /// A host that is not an MVP screen uses <c>new SamplePresenter().Connect(control)</c> instead.
    /// </summary>
    public sealed class SamplePresenter : ControlPresenterBase<ISampleView>
    {
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
