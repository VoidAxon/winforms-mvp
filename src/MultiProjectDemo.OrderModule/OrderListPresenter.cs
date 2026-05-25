using WinformsMVP.MVP.Presenters;

namespace MultiProjectDemo.OrderModule
{
    public class OrderListPresenter : WindowPresenterBase<IOrderListView>
    {
        private readonly IOrderRepository _repository;

        public OrderListPresenter(IOrderRepository repository)
        {
            _repository = repository;
        }

        protected override void OnViewAttached() { }

        protected override void OnInitialize()
        {
            ReloadList();
        }

        protected override void RegisterViewActions()
        {
            _dispatcher.Register(OrderListActions.Refresh, ReloadList);
        }

        private void ReloadList()
        {
            View.SetOrders(_repository.GetAll());
        }
    }
}
