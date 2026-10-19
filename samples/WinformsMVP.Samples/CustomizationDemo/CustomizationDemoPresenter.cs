using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.CustomizationDemo
{
    /// <summary>
    /// Plain presenter code — no customization lives here. The dialogs, toasts and change
    /// detection it triggers look and behave as configured in <see cref="CustomizationDemoSetup"/>.
    /// </summary>
    public class CustomizationDemoPresenter : WindowPresenterBase<ICustomizationDemoView>
    {
        public class Customer
        {
            public string Name { get; set; }
            public System.DateTime LastViewed { get; set; }   // ignored by the configured comparer
        }

        private ChangeTracker<Customer> _tracker;

        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
        {
            Dispatcher.Register(CustomizationDemoActions.Info, () => Messages.ShowInfo("This is the company-styled dialog."));
            Dispatcher.Register(CustomizationDemoActions.Confirm, OnConfirm);
            Dispatcher.Register(CustomizationDemoActions.Toast, () => Toasts.Show("Branded toast, 4.5 s by default", ToastType.Success));
            Dispatcher.Register(CustomizationDemoActions.CheckChanges, OnCheckChanges);
        }

        protected override void OnInitialize()
        {
            var customer = new Customer { Name = "Contoso", LastViewed = System.DateTime.Now };
            // Uses the policy registered for Customer at startup (LastViewed is not an edit).
            _tracker = TrackChanges(customer);
            View.CustomerName = customer.Name;
        }

        private void OnConfirm()
        {
            View.ShowStatus(Messages.ConfirmYesNo("Proceed?") ? "You chose Yes." : "You chose No.");
        }

        private void OnCheckChanges()
        {
            _tracker.CurrentValue.Name = View.CustomerName;
            _tracker.CurrentValue.LastViewed = System.DateTime.Now;
            View.ShowStatus(_tracker.IsChanged
                ? "Changed (the name differs)."
                : "Not changed (only LastViewed moved, which the policy ignores).");
        }
    }
}
