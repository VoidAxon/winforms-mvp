using System.Threading;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;   // Navigator.For(...) and the other service interfaces

namespace MvpNamespace
{
    /// <summary>
    /// The window's use cases. Open it from another presenter with
//#if (HasParam)
    /// <c>Navigator.For(new SamplePresenter()).WithParam(value).ShowAsModal()</c>.
//#else
    /// <c>Navigator.For(new SamplePresenter()).ShowAsModal()</c>.
//#endif
    /// Business services go into the constructor.
    /// </summary>
//#if (HasParam)
    public sealed class SamplePresenter : WindowPresenterBase<ISampleView, PARAM_TYPE>
//#else
    public sealed class SamplePresenter : WindowPresenterBase<ISampleView>
//#endif
    {
        protected override void OnViewAttached()
        {
//#if (Validation)
            // Unsubscribed when the presenter is disposed.
            SubscribeViewEvent(h => View.DataChanged += h, h => View.DataChanged -= h, OnDataChanged);
//#endif
        }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(SampleActions.Ok, OnOkAsync);
            // Same as X: RequestClose goes through CanClose / CanCloseAsync. Override CanClose to ask about
            // unsaved edits; for an explicit "discard and close", AcceptChanges() before RequestClose.
            Dispatcher.Register(SampleActions.Cancel, () => RequestClose(InteractionStatus.Cancel));
        }

//#if (HasParam)
        protected override void OnInitialize(PARAM_TYPE parameters)
        {
            // Show what the window was opened with. Load data asynchronously in OnInitializeAsync.
        }
//#else
        protected override void OnInitialize()
        {
            // Fill the view. Load data asynchronously in OnInitializeAsync.
        }
//#endif
//#if (Validation)

        private SampleModel ReadModel() => new SampleModel { Title = View.Title };

        // Errors appear next to a field once the user has left it.
        private void OnDataChanged() => Validation.Validate(ReadModel());
//#endif

        // Disabled while it runs; double clicks are ignored; cancelled if the window closes.
        private async Task OnOkAsync(CancellationToken cancellationToken)
        {
//#if (Validation)
            var model = ReadModel();
            if (!Validation.Check(model))   // shows every error next to its field
            {
                // The first error in screen order (the model's declaration order): tell, then go there.
                var first = Validation.FirstError;
                Messages.ShowWarning(first.Message);
                View.FocusError(first.Field);
                return;
            }

//#endif
            await Task.CompletedTask;               // save here, passing cancellationToken
            // The close goes through CanClose: after saving, mark the state clean (tracker.AcceptChanges())
            // so it does not ask "save changes?".
            RequestClose(InteractionStatus.Ok);
        }
    }
}
