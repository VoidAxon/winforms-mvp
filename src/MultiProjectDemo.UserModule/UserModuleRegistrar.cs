using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.DependencyInjection;
using WinformsMVP.Services;

namespace MultiProjectDemo.UserModule
{
    /// <summary>
    /// The single entry point through which the User module declares everything it owns:
    /// its View → Form mappings, its repository, and its Presenters.
    /// The Shell project just instantiates this class; it does not need to know
    /// what types live inside the module.
    /// </summary>
    public class UserModuleRegistrar : IModuleRegistrar
    {
        public void RegisterViews(IViewMappingRegister registry)
        {
            registry.Register<IUserListView, UserListForm>();
            registry.Register<IUserEditView, UserEditForm>();
        }

        public void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            services.AddTransient<UserListPresenter>();
            services.AddTransient<UserEditPresenter>();
        }
    }
}
