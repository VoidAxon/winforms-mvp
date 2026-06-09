using System.Windows.Forms;
using WinformsMVP.Common;
using WinformsMVP.MVP.Presenters;

namespace WinformsMVP.Samples.CascadeDemo
{
    public sealed class CascadeForm : Form
    {
        // held so they are not collected and are disposed with their controls
        private readonly CategoryListPresenter _categoryPresenter;
        private readonly SubCategoryListPresenter _subCategoryPresenter;
        private readonly ProductListPresenter _productPresenter;

        public CascadeForm()
        {
            Text = "Cascade Demo - Category > SubCategory > Product";
            Width = 720;
            Height = 420;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));

            var categoryControl = new SelectListControl<Category>("Category") { Dock = DockStyle.Fill };
            var subCategoryControl = new SelectListControl<SubCategory>("SubCategory") { Dock = DockStyle.Fill };
            var productControl = new SelectListControl<Product>("Product") { Dock = DockStyle.Fill };
            layout.Controls.Add(categoryControl, 0, 0);
            layout.Controls.Add(subCategoryControl, 1, 0);
            layout.Controls.Add(productControl, 2, 0);
            Controls.Add(layout);

            // Per-screen stores (not app-wide singletons).
            var categoryStore = new SelectionStore<Category>();
            var subCategoryStore = new SelectionStore<SubCategory>();
            var productStore = new SelectionStore<Product>();

            var catalog = new InMemoryCatalog();

            _categoryPresenter = new CategoryListPresenter(categoryStore, catalog);
            _subCategoryPresenter = new SubCategoryListPresenter(categoryStore, subCategoryStore, catalog);
            _productPresenter = new ProductListPresenter(subCategoryStore, productStore, catalog);

            _categoryPresenter.Connect(categoryControl);
            _subCategoryPresenter.Connect(subCategoryControl);
            _productPresenter.Connect(productControl);
        }
    }
}
