using System;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.CascadeDemo
{
    // Top level: no parent. Loads its own list; publishes the user's selection to its store.
    public sealed class CategoryListPresenter : ControlPresenterBase<ISelectListView<Category>>
    {
        private readonly ISelectionStore<Category> _store;
        private readonly ICategoryRepository _repo;

        public CategoryListPresenter(ISelectionStore<Category> store, ICategoryRepository repo)
        {
            _store = store;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;   // user selection -> store
        }

        protected override void OnInitialize()
        {
            View.Items = _repo.GetAll();   // initial list load belongs in OnInitialize
        }

        private void OnUserSelected(object sender, EventArgs e) { _store.Select(View.Selected); }

        protected override void Cleanup() { View.SelectionChanged -= OnUserSelected; }
    }

    // Middle level: subscribes to the parent (Category) store, reloads its own list.
    public sealed class SubCategoryListPresenter : ControlPresenterBase<ISelectListView<SubCategory>>
    {
        private readonly ISelectionStore<Category> _parent;
        private readonly ISelectionStore<SubCategory> _self;
        private readonly ISubCategoryRepository _repo;
        private IDisposable _bind;

        public SubCategoryListPresenter(
            ISelectionStore<Category> parent,
            ISelectionStore<SubCategory> self,
            ISubCategoryRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;

            if (_bind != null) _bind.Dispose();   // guard against double-attach
            _bind = Cascade.Bind(_parent, _self, category =>
            {
                try
                {
                    View.Items = category == null
                        ? new SubCategory[0]                   // net40-style empty (sample stays uniform)
                        : _repo.GetByCategory(category.Id);
                }
                catch (Exception ex)
                {
                    View.Items = new SubCategory[0];           // reload self-recovers; Cascade does not roll back
                    Messages.ShowError("Failed to load subcategories: " + ex.Message, "Error");
                }
            });
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }

        protected override void Cleanup()
        {
            View.SelectionChanged -= OnUserSelected;
            if (_bind != null) _bind.Dispose();
        }
    }

    // Leaf level: subscribes to the SubCategory store. Same shape as the middle level.
    public sealed class ProductListPresenter : ControlPresenterBase<ISelectListView<Product>>
    {
        private readonly ISelectionStore<SubCategory> _parent;
        private readonly ISelectionStore<Product> _self;
        private readonly IProductRepository _repo;
        private IDisposable _bind;

        public ProductListPresenter(
            ISelectionStore<SubCategory> parent,
            ISelectionStore<Product> self,
            IProductRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            View.SelectionChanged += OnUserSelected;

            if (_bind != null) _bind.Dispose();
            _bind = Cascade.Bind(_parent, _self, sub =>
            {
                try
                {
                    View.Items = sub == null ? new Product[0] : _repo.GetBySubCategory(sub.Id);
                }
                catch (Exception ex)
                {
                    View.Items = new Product[0];
                    Messages.ShowError("Failed to load products: " + ex.Message, "Error");
                }
            });
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }

        protected override void Cleanup()
        {
            View.SelectionChanged -= OnUserSelected;
            if (_bind != null) _bind.Dispose();
        }
    }
}
