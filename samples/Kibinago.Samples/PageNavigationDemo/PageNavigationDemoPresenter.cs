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
            _pages = new PageStack(this, () => View.Pages);
        }

        protected override void RegisterViewActions()
            => Dispatcher.Register(PageNavigationDemoActions.Breadcrumb, (int level) => _pages.PopToCountAsync(level + 1));

        protected override void OnInitialize()
            => _pages.Push(new CustomerListPresenter(_store), new CustomerListParams { Open = OpenCustomer }, "顧客一覧");

        Task<bool> IPageCloseGuard.CanCloseAsync() => _pages.CanCloseAllAsync();

        // While a jump is deciding (its question is open) a push answers false and creates nothing.
        private void OpenCustomer(int customerId)
            => _pages.Push(new CustomerDetailPresenter(_store),
                           new CustomerDetailParams { CustomerId = customerId, OpenOrder = OpenOrder },
                           _store.Customer(customerId).Name);

        private void OpenOrder(int orderId)
            => _pages.Push(new OrderEditPresenter(_store),
                           new OrderEditParams { OrderId = orderId, Saved = OnOrderSaved }, "受注 " + orderId);

        private void OnOrderSaved(DemoOrder order)
            => View.ShowStatus($"受注 {order.Id} を保存しました ({order.Product} × {order.Quantity})。");
    }
}
