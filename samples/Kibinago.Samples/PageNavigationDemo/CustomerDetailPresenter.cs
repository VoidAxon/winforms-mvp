using System;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.PageNavigationDemo
{
    public sealed class CustomerDetailParams
    {
        public int CustomerId;

        /// <summary>Called with the order id when the user opens an order.</summary>
        public Action<int> OpenOrder;
    }

    public sealed class CustomerDetailPresenter : ControlPresenterBase<ICustomerDetailView, CustomerDetailParams>
    {
        private readonly DemoCustomerStore _store;
        private int _customerId;

        public CustomerDetailPresenter(DemoCustomerStore store) { _store = store; }

        protected override void OnInitialize(CustomerDetailParams parameters)
        {
            _customerId = parameters.CustomerId;
            SubscribeViewEvent<int>(h => View.OrderOpened += h, h => View.OrderOpened -= h, id => parameters.OpenOrder(id));
            EventHandler<DemoOrder> saved = (s, order) => { if (order.CustomerId == _customerId) ShowOrders(); };
            _store.OrderSaved += saved;
            Disposable.Create(() => _store.OrderSaved -= saved).DisposeWith(Disposables);

            var customer = _store.Customer(_customerId);
            View.ShowCustomer(customer.Name, customer.Phone);
            ShowOrders();
        }

        private void ShowOrders() => View.ShowOrders(_store.OrdersOf(_customerId));
    }
}
