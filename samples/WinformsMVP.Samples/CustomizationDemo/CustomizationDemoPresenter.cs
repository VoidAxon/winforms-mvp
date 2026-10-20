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
            public System.DateTime LastViewed { get; set; }   // ignored on this screen
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
            // The tracker only detects changes; it keeps its own baseline copy. On this screen,
            // LastViewed is not an edit.
            _tracker = TrackChanges(customer).Ignore(c => c.LastViewed);
            View.CustomerName = customer.Name;
        }

        private void OnConfirm()
        {
            View.ShowStatus(Messages.ConfirmYesNo("Proceed?") ? "You chose Yes." : "You chose No.");
        }

        private void OnCheckChanges()
        {
            var edited = new Customer { Name = View.CustomerName, LastViewed = System.DateTime.Now };
            View.ShowStatus(_tracker.IsChangedWith(edited)
                ? "Changed (the name differs)."
                : "Not changed (only LastViewed moved, which this screen ignores).");
        }
    }
}
