using Kibinago.Services;

namespace Kibinago.Samples.PageNavigationDemo
{
    /// <summary>
    /// The page controls of the demo, registered like windows so the PageHost can create them. An application does
    /// this once at startup (<c>Program.cs</c> here); the sample shell also calls it before it opens the demo, because
    /// its tests open demos without running <c>Program</c>.
    /// </summary>
    public static class PageNavigationDemoViews
    {
        public static void Register(IViewMappingRegister register)
        {
            register.Register<ICustomerListView, CustomerListControl>(allowOverride: true);
            register.Register<ICustomerDetailView, CustomerDetailControl>(allowOverride: true);
            register.Register<IOrderEditView, OrderEditControl>(allowOverride: true);
        }
    }
}
