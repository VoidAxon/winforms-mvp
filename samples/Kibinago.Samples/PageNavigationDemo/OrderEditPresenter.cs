using System;
using System.Threading.Tasks;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.PageNavigationDemo
{
    public sealed class OrderEditParams
    {
        public int OrderId;

        /// <summary>Called with the saved order (the result travels back through the parameters).</summary>
        public Action<DemoOrder> Saved;
    }

    /// <summary>
    /// The top page. Unsaved edits make it refuse to be discarded unless the user agrees: the same question for
    /// a breadcrumb jump, the window closing, or the shell switching categories.
    /// </summary>
    public sealed class OrderEditPresenter : ControlPresenterBase<IOrderEditView, OrderEditParams>, IPageCloseGuard
    {
        private readonly DemoCustomerStore _store;
        private DemoOrder _order;
        private ChangeTracker<DemoOrder> _tracker;

        public OrderEditPresenter(DemoCustomerStore store) { _store = store; }

        protected override void RegisterViewActions()
            => Dispatcher.Register(PageNavigationDemoActions.SaveOrder, Save);

        protected override void OnInitialize(OrderEditParams parameters)
        {
            _order = _store.Order(parameters.OrderId);
            View.Product = _order.Product;
            View.Quantity = _order.Quantity;
            View.Note = _order.Note;
            _tracker = TrackChanges(Read());
            SubscribeViewEvent(h => View.Edited += h, h => View.Edited -= h, () => View.IsDirty = _tracker.IsChangedWith(Read()));
        }

        Task<bool> IPageCloseGuard.CanCloseAsync()
            => Task.FromResult(!_tracker.IsChangedWith(Read()) || Messages.ConfirmYesNo("受注の変更を破棄しますか?", "受注編集"));

        private void Save()
        {
            var order = Read();
            _store.Save(order);
            _tracker.AcceptChanges(order);
            View.IsDirty = false;
            Parameters.Saved?.Invoke(order);
        }

        private DemoOrder Read() => new DemoOrder
        {
            Id = _order.Id,
            CustomerId = _order.CustomerId,
            Product = View.Product,
            Quantity = View.Quantity,
            Note = View.Note,
        };
    }
}
