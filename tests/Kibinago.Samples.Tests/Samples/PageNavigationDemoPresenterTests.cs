using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kibinago.Presenters;
using Kibinago.Samples.PageNavigationDemo;
using Kibinago.Testing;
using Kibinago.ViewActions;
using Kibinago.Views;
using Xunit;

namespace Kibinago.Samples.Tests.Samples
{
    public class PageNavigationDemoPresenterTests
    {
        private sealed class MockListView : ICustomerListView
        {
            public IViewActionBinder ActionBinder => null;
            public IReadOnlyList<CustomerRow> Rows;
            public void ShowCustomers(IReadOnlyList<CustomerRow> rows) => Rows = rows;
            public event EventHandler<int> CustomerOpened;
            public void Open(int id) => CustomerOpened?.Invoke(this, id);
        }

        private sealed class MockDetailView : ICustomerDetailView
        {
            public IViewActionBinder ActionBinder => null;
            public string Name;
            public IReadOnlyList<DemoOrder> Orders;
            public void ShowCustomer(string name, string phone) => Name = name;
            public void ShowOrders(IReadOnlyList<DemoOrder> orders) => Orders = orders;
            public event EventHandler<int> OrderOpened;
            public void Open(int id) => OrderOpened?.Invoke(this, id);
        }

        private sealed class MockOrderView : IOrderEditView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public string Product { get; set; }
            public int Quantity { get; set; }
            public string Note { get; set; }
            public bool Dirty;
            public bool IsDirty { set => Dirty = value; }
            public event EventHandler Edited;
            public void Edit(int quantity) { Quantity = quantity; Edited?.Invoke(this, EventArgs.Empty); }
        }

        private sealed class MockHostView : IPageNavigationDemoView
        {
            public CapturingActionBinder Binder { get; } = new CapturingActionBinder();
            public IViewActionBinder ActionBinder => Binder;
            public readonly List<string> Crumbs = new List<string>();
            public readonly List<IControlView> Pages = new List<IControlView>();
            public readonly List<IControlView> Removed = new List<IControlView>();
            public int Created;
            public string Status;

            private T Add<T>(T page, string title) where T : IControlView
            {
                Created++;
                Pages.Add(page);
                Crumbs.Add(title);
                return page;
            }

            public ICustomerListView PushCustomerList() => Add(new MockListView(), "顧客一覧");
            public ICustomerDetailView PushCustomerDetail(string title) => Add(new MockDetailView(), title);
            public IOrderEditView PushOrderEdit(string title) => Add(new MockOrderView(), title);

            public void RemovePage(IControlView page)
            {
                int index = Pages.IndexOf(page);
                Pages.RemoveAt(index);
                Crumbs.RemoveAt(index);
                Removed.Add(page);
            }

            public void ShowStatus(string text) => Status = text;

            public MockListView List => (MockListView)Pages[0];
            public MockDetailView Detail => (MockDetailView)Pages[1];
            public MockOrderView Order => (MockOrderView)Pages[2];
        }

        private readonly FakeServices _fakes = TestServices.CreateFakes();
        private readonly MockHostView _view = new MockHostView();
        private readonly DemoCustomerStore _store = new DemoCustomerStore();

        private PresenterTestHost Start() => PresenterTestHost.Start(_fakes.Provider, new PageNavigationDemoPresenter(_store), _view);

        private void OpenOrder101()
        {
            _view.List.Open(1);
            _view.Detail.Open(101);
        }

        [Fact]
        public void StartsAtTheCustomerList_AndDrillsDown()
        {
            using (Start())
            {
                Assert.Equal(new[] { "顧客一覧" }, _view.Crumbs);
                Assert.Equal(3, _view.List.Rows.Count);

                OpenOrder101();

                Assert.Equal(new[] { "顧客一覧", "山田 太郎", "受注 101" }, _view.Crumbs);
                Assert.Equal("山田 太郎", _view.Detail.Name);
                Assert.Equal(2, _view.Detail.Orders.Count);
                Assert.Equal("コピー用紙 A4", _view.Order.Product);
            }
        }

        [Fact]
        public async Task EditedOrder_BreadcrumbToTheList_No_Stays()
        {
            using (Start())
            {
                OpenOrder101();
                _view.Order.Edit(99);
                _fakes.Messages.EnqueueYesNo(false);

                await _view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 0);

                Assert.Equal(3, _view.Pages.Count);
                Assert.Empty(_view.Removed);
                Assert.Single(_fakes.Messages.Calls, c => c.IsConfirmation);
            }
        }

        [Fact]
        public async Task EditedOrder_BreadcrumbToTheList_Yes_BackAtTheList()
        {
            using (Start())
            {
                OpenOrder101();
                var order = _view.Order;
                var detail = _view.Detail;
                _view.Order.Edit(99);
                _fakes.Messages.EnqueueYesNo(true);

                await _view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 0);

                Assert.Equal(new[] { "顧客一覧" }, _view.Crumbs);
                Assert.Equal(new IControlView[] { order, detail }, _view.Removed);
                Assert.Equal(10, _store.Order(101).Quantity);   // discarded, not saved
            }
        }

        [Fact]
        public async Task UneditedOrder_BreadcrumbAsksNothing()
        {
            using (Start())
            {
                OpenOrder101();

                await _view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 1);

                Assert.Equal(2, _view.Pages.Count);
                Assert.Empty(_fakes.Messages.Calls);
            }
        }

        [Fact]
        public async Task SavingTheOrder_RefreshesTheListAndDetail_AndReportsThroughTheCallback()
        {
            using (Start())
            {
                OpenOrder101();
                _view.Order.Edit(99);

                await _view.Order.Binder.DispatchAsync(PageNavigationDemoActions.SaveOrder);

                Assert.Equal(99, _store.Order(101).Quantity);
                Assert.Equal(99 + 50, _view.List.Rows.Single(r => r.Id == 1).TotalQuantity);
                Assert.Equal(99, _view.Detail.Orders.Single(o => o.Id == 101).Quantity);
                Assert.Contains("受注 101 を保存しました", _view.Status);          // the Saved callback
                Assert.False(_view.Order.Dirty);

                await _view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 0);
                Assert.Equal(new[] { "顧客一覧" }, _view.Crumbs);
                Assert.DoesNotContain(_fakes.Messages.Calls, c => c.IsConfirmation);   // saved: nothing to ask
            }
        }

        [Fact]
        public async Task OpeningACustomerWhileAJumpIsDeciding_CreatesNoView()
        {
            using (Start())
            {
                OpenOrder101();
                _view.Order.Edit(99);
                int createdBefore = _view.Created;
                var list = _view.List;
                _fakes.Messages.YesNoHandler = call =>
                {
                    list.Open(2);          // a click that arrives while the dialog is open
                    return false;
                };

                await _view.Binder.DispatchAsync(PageNavigationDemoActions.Breadcrumb, 0);

                Assert.Equal(createdBefore, _view.Created);
                Assert.Equal(3, _view.Pages.Count);
            }
        }

        [Fact]
        public async Task TheDemoAsAWhole_AsksItsPagesBeforeItIsDiscarded()
        {
            var presenter = new PageNavigationDemoPresenter(_store);
            using (PresenterTestHost.Start(_fakes.Provider, presenter, _view))
            {
                OpenOrder101();
                _view.Order.Edit(99);
                _fakes.Messages.EnqueueYesNo(false);

                Assert.False(await ((IPageCloseGuard)presenter).CanCloseAsync());
                Assert.Equal(3, _view.Pages.Count);   // asked, nothing removed
            }
        }

        [Fact]
        public void DisposingTheDemo_DisposesThePages_WithoutRemovingViews()
        {
            var host = Start();
            OpenOrder101();

            host.Dispose();

            Assert.Empty(_view.Removed);
        }
    }
}
