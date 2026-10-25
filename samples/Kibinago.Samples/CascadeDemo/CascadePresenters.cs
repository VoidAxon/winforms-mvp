using System;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.CascadeDemo
{
    // Top level: no parent. Loads its own list; publishes the user's selection to its Selection.
    public sealed class CategoryListPresenter : ControlPresenterBase<ISelectListView<Category>>
    {
        private readonly ISelection<Category> _selection;
        private readonly ICategoryRepository _repo;

        public CategoryListPresenter(ISelection<Category> selection, ICategoryRepository repo)
        {
            _selection = selection;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            EventHandler handler = OnUserSelected;
            View.SelectionChanged += handler;
            Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);
        }

        protected override void OnInitialize()
        {
            View.Items = _repo.GetAll();   // initial list load belongs in OnInitialize
        }

        private void OnUserSelected(object sender, EventArgs e) { _selection.Select(View.Selected); }
    }

    // Middle level: subscribes to the parent (Category) selection, reloads its own list.
    public sealed class SubCategoryListPresenter : ControlPresenterBase<ISelectListView<SubCategory>>
    {
        private readonly ISelection<Category> _parent;
        private readonly ISelection<SubCategory> _self;
        private readonly ISubCategoryRepository _repo;

        public SubCategoryListPresenter(
            ISelection<Category> parent,
            ISelection<SubCategory> self,
            ISubCategoryRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            EventHandler handler = OnUserSelected;
            View.SelectionChanged += handler;
            Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);

            Cascade.Bind(_parent, _self, category =>
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
            }).DisposeWith(Disposables);
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }
    }

    // Leaf level: subscribes to the SubCategory selection. Same shape as the middle level.
    public sealed class ProductListPresenter : ControlPresenterBase<ISelectListView<Product>>
    {
        private readonly ISelection<SubCategory> _parent;
        private readonly ISelection<Product> _self;
        private readonly IProductRepository _repo;

        public ProductListPresenter(
            ISelection<SubCategory> parent,
            ISelection<Product> self,
            IProductRepository repo)
        {
            _parent = parent;
            _self = self;
            _repo = repo;
        }

        protected override void OnViewAttached()
        {
            EventHandler handler = OnUserSelected;
            View.SelectionChanged += handler;
            Disposable.Create(() => View.SelectionChanged -= handler).DisposeWith(Disposables);

            Cascade.Bind(_parent, _self, sub =>
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
            }).DisposeWith(Disposables);
        }

        private void OnUserSelected(object sender, EventArgs e) { _self.Select(View.Selected); }
    }
}
