using Microsoft.Extensions.DependencyInjection;
using WinformsMVP.Modules;

namespace WinformsMVP.DependencyInjection
{
    /// <summary>
    /// Contract for a UI module that registers both its Views and the services its
    /// Presenters depend on. Extends <see cref="IViewModuleRegistrar"/> from the main
    /// framework with the DI-container half — implementations live in this package
    /// because they depend on <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Typical implementation:</b>
    /// <code>
    /// public class UserModuleRegistrar : IModuleRegistrar
    /// {
    ///     public void RegisterViews(IViewMappingRegister registry)
    ///         =&gt; registry.RegisterFromAssembly(typeof(UserModuleRegistrar).Assembly);
    ///
    ///     public void RegisterServices(IServiceCollection services)
    ///     {
    ///         services.AddTransient&lt;UserListPresenter&gt;();
    ///         services.AddSingleton&lt;IUserRepository, UserRepository&gt;();
    ///     }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// Each module owns the registration of its own Views, Presenters, and supporting
    /// services. The Shell project orchestrates the modules but does not need to know
    /// what they contain.
    /// </para>
    /// </remarks>
    public interface IModuleRegistrar : IViewModuleRegistrar
    {
        /// <summary>
        /// Registers the module's services (Presenters, repositories, etc.) with the
        /// host application's DI container.
        /// </summary>
        void RegisterServices(IServiceCollection services);
    }
}
