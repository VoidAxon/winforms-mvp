using Kibinago.Views;

namespace Kibinago.Samples.CascadeDemo
{
    /// <summary>The three lists of the cascade, as child views (the parent view exposes view interfaces, never controls).</summary>
    public interface ICascadeDemoView : IViewBase
    {
        ISelectListView<Category> Categories { get; }
        ISelectListView<SubCategory> SubCategories { get; }
        ISelectListView<Product> Products { get; }
    }
}
