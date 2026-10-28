using System.Windows.Forms;
using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.CascadeDemo
{
    public sealed partial class CascadeForm : Form
    {
        // held so they are not collected and are disposed with their controls
        private readonly CategoryListPresenter _categoryPresenter;
        private readonly SubCategoryListPresenter _subCategoryPresenter;
        private readonly ProductListPresenter _productPresenter;

        public CascadeForm()
        {
            InitializeComponent();

            // Per-screen selections (not app-wide singletons).
            var categorySelection = new Selection<Category>();
            var subCategorySelection = new Selection<SubCategory>();
            var productSelection = new Selection<Product>();

            var catalog = new InMemoryCatalog();

            _categoryPresenter = new CategoryListPresenter(categorySelection, catalog);
            _subCategoryPresenter = new SubCategoryListPresenter(categorySelection, subCategorySelection, catalog);
            _productPresenter = new ProductListPresenter(subCategorySelection, productSelection, catalog);

            _categoryPresenter.Connect(_categoryControl);
            _subCategoryPresenter.Connect(_subCategoryControl);
            _productPresenter.Connect(_productControl);
        }
    }
}
