using System.Threading.Tasks;
using Kibinago.Presenters;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// In-window drill-down with a breadcrumb: 顧客一覧 → 顧客詳細 → 受注編集. The pages live on a
    /// <see cref="PageStack"/>; a crumb click jumps back (the pages it discards are asked first). The pages below
    /// the top stay alive, so the list keeps its selection when you come back.
    /// </summary>
    /// <remarks>
    /// The demo itself is a page-owning panel inside the sample shell: it forwards its own
    /// <see cref="IPageCloseGuard"/> to the stack, so the shell can ask it before switching categories, and the
    /// main window asks every page before it closes.
    /// </remarks>
    public sealed class PageNavigationDemoPresenter : ControlPresenterBase<IPageNavigationDemoView>, IPageCloseGuard
    {
        private readonly DemoCustomerStore _store;
        private readonly PageStack _pages;

        public PageNavigationDemoPresenter() : this(new DemoCustomerStore()) { }

        public PageNavigationDemoPresenter(DemoCustomerStore store)
        {
            _store = store;
            _pages = new PageStack(this);
        }

        protected override void RegisterViewActions()
            => Dispatcher.Register(PageNavigationDemoActions.Breadcrumb, (int level) => _pages.PopToCountAsync(level + 1));

        protected override void OnInitialize()
            => _pages.Push(new CustomerListPresenter(_store), View.PushCustomerList(),
                           new CustomerListParams { Open = OpenCustomer }, View.RemovePage);

        Task<bool> IPageCloseGuard.CanCloseAsync() => _pages.CanCloseAllAsync();

        private void OpenCustomer(int customerId)
        {
            if (_pages.IsBusy) return;   // a jump is deciding: check before the view creates the page
            var customer = _store.Customer(customerId);
            _pages.Push(new CustomerDetailPresenter(_store), View.PushCustomerDetail(customer.Name),
                        new CustomerDetailParams { CustomerId = customerId, OpenOrder = OpenOrder }, View.RemovePage);
        }

        private void OpenOrder(int orderId)
        {
            if (_pages.IsBusy) return;
            _pages.Push(new OrderEditPresenter(_store), View.PushOrderEdit("受注 " + orderId),
                        new OrderEditParams { OrderId = orderId, Saved = OnOrderSaved }, View.RemovePage);
        }

        private void OnOrderSaved(DemoOrder order)
            => View.ShowStatus($"受注 {order.Id} を保存しました ({order.Product} × {order.Quantity})。");
    }
}
