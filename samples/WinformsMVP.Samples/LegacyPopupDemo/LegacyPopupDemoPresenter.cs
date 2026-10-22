using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.LegacyPopupDemo
{
    /// <summary>
    /// Opens the legacy customer editor through the view and keeps what it returns. The presenter
    /// sees only business data and an InteractionResult — never the legacy Form.
    /// </summary>
    public sealed class LegacyPopupDemoPresenter : WindowPresenterBase<ILegacyPopupDemoView>
    {
        private CustomerData _customer;

        protected override void OnViewAttached() { }

        protected override void RegisterViewActions()
            => Dispatcher.Register(LegacyPopupDemoActions.EditCustomer, OnEditCustomer);

        protected override void OnInitialize() => View.ShowStatus("Ready. 編集 opens a legacy (non-MVP) form.");

        private void OnEditCustomer()
        {
            var result = View.EditCustomer(_customer);
            if (result.IsError)
            {
                Messages.ShowError(result.ErrorMessage);
                return;
            }
            if (!result.IsOk)
            {
                View.ShowStatus("Edit cancelled");
                return;
            }

            _customer = result.Value;   // validate / save here as the business requires
            View.ShowCustomer(_customer);
            View.ShowStatus($"Customer updated: {_customer.Name}");
        }
    }
}
