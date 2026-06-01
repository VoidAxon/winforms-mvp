using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.DependencyInjection;
using WinformsMVP.Services;

namespace MultiProjectDemo.OrderModule
{
    public class OrderModuleRegistrar : IModuleRegistrar
    {
        public void RegisterViews(IViewMappingRegister registry)
        {
            registry.Register<IOrderListView, OrderListForm>();
        }

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
            services.AddTransient<OrderListPresenter>();
        }
    }
}
