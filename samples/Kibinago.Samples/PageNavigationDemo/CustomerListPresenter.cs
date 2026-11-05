using System;
using System.Linq;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.PageNavigationDemo
{
    public sealed class CustomerListParams
    {
        /// <summary>Called with the customer id when the user opens a customer.</summary>
        public Action<int> Open;
    }

    /// <summary>The first page. It stays alive below the others and refreshes when an order is saved.</summary>
    public sealed class CustomerListPresenter : ControlPresenterBase<ICustomerListView, CustomerListParams>
    {
        private readonly DemoCustomerStore _store;

        public CustomerListPresenter(DemoCustomerStore store) { _store = store; }

        protected override void OnInitialize(CustomerListParams parameters)
        {
            SubscribeViewEvent<int>(h => View.CustomerOpened += h, h => View.CustomerOpened -= h, id => parameters.Open(id));
            EventHandler<DemoOrder> saved = (s, order) => ShowRows();
            _store.OrderSaved += saved;
            Disposable.Create(() => _store.OrderSaved -= saved).DisposeWith(Disposables);
            ShowRows();
        }

        private void ShowRows()
            => View.ShowCustomers(_store.Customers
                .Select(c => new CustomerRow { Id = c.Id, Name = c.Name, Phone = c.Phone, TotalQuantity = _store.TotalQuantity(c.Id) })
                .ToList());
    }
}
