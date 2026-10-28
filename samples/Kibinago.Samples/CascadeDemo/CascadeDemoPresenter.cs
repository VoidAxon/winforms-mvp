using Kibinago.Common;
using Kibinago.Presenters;

namespace Kibinago.Samples.CascadeDemo
{
    /// <summary>
    /// Composes the screen: one Selection per level (per screen, not app-wide), and a child presenter per
    /// list attached with AttachChild. Each child reloads its own list when the level above changes.
    /// </summary>
    public class CascadeDemoPresenter : ControlPresenterBase<ICascadeDemoView>
    {
        protected override void OnInitialize()
        {
            var categories = new Selection<Category>();
            var subCategories = new Selection<SubCategory>();
            var products = new Selection<Product>();
            var catalog = new InMemoryCatalog();

            AttachChild(new CategoryListPresenter(categories, catalog), View.Categories);
            AttachChild(new SubCategoryListPresenter(categories, subCategories, catalog), View.SubCategories);
            AttachChild(new ProductListPresenter(subCategories, products, catalog), View.Products);
        }
    }
}
