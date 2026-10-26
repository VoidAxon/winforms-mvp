//#if (!Edit)
using System.Threading;
using System.Threading.Tasks;
//#endif
using Kibinago.Common;
using Kibinago.Presenters;
using Kibinago.Services;   // Navigator.For(...) and the other service interfaces

namespace MvpNamespace
{
//#if (Edit)
    /// <summary>
    /// An edit dialog: opened with the model, closes with the edited model. Open it from another presenter:
    /// <code>
    /// var r = Navigator.For(new SamplePresenter()).WithParam(model).ShowAsModal&lt;SampleModel&gt;();   // new SampleModel() to add
    /// if (r.IsOk) { save r.Value; reload the list and select it }
    /// </code>
    /// Closing with unsaved edits (X, Esc, Cancel) asks "Save changes?". Business services go into the constructor.
    /// </summary>
    public sealed class SamplePresenter : WindowPresenterBase<ISampleView, SampleModel>
//#else
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
//#endif
    {
//#if (Edit)
        // Detects unsaved edits: compares what the screen shows now with what it showed when opened / saved.
        private ChangeTracker<SampleModel> _tracker;

//#endif
        protected override void OnViewAttached()
        {
//#if (HasModel)
            // Unsubscribed when the presenter is disposed.
            SubscribeViewEvent(h => View.DataChanged += h, h => View.DataChanged -= h, OnDataChanged);
//#endif
        }

        protected override void RegisterViewActions()
        {
//#if (Edit)
            Dispatcher.Register(SampleActions.Ok, OnOk);
            // Same as X and Esc: RequestClose goes through CanClose, which asks about unsaved edits.
            Dispatcher.Register(SampleActions.Cancel, () => RequestClose(InteractionStatus.Cancel));
//#else
            Dispatcher.Register(SampleActions.Ok, OnOkAsync);
            // Same as X: RequestClose goes through CanClose / CanCloseAsync. Override CanClose to ask about
            // unsaved edits; for an explicit "discard and close", AcceptChanges() before RequestClose.
            Dispatcher.Register(SampleActions.Cancel, () => RequestClose(InteractionStatus.Cancel));
//#endif
        }

//#if (Edit)
        protected override void OnInitialize(SampleModel parameters)
        {
            View.Title = parameters.Title;
            _tracker = TrackChanges(ReadModel());   // what the screen shows now counts as unchanged
        }
//#elseif (HasParam)
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
//#if (HasModel)

        private SampleModel ReadModel() => new SampleModel { Title = View.Title };

        // Errors appear next to a field once the user has left it.
        private void OnDataChanged() => Validation.Validate(ReadModel());
//#endif
//#if (Edit)

        private void OnOk()
        {
            var model = ReadModel();
            if (!IsValid(model)) return;
            _tracker.AcceptChanges(model);           // saved: closing no longer asks
            RequestClose(model, InteractionStatus.Ok);
        }

        // X, Esc, Cancel: ask only when something was edited.
        protected override bool CanClose(CloseReason reason)
        {
            var model = ReadModel();
            if (!_tracker.IsChangedWith(model)) return true;

            switch (Messages.ConfirmYesNoCancel("Save changes?"))
            {
                case ConfirmResult.No: return true;       // discard
                case ConfirmResult.Cancel: return false;  // keep editing
            }
            if (!IsValid(model)) return false;
            RequestClose(model, InteractionStatus.Ok);  // inside the gate: the result of this close
            return true;
        }

        // Shows every error next to its field; the first one (screen order) as a message, then goes there.
        private bool IsValid(SampleModel model)
        {
            if (Validation.Check(model)) return true;
            var first = Validation.FirstError;
            Messages.ShowWarning(first.Message);
            View.FocusError(first.Field);
            return false;
        }
//#else

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
//#endif
    }
}
