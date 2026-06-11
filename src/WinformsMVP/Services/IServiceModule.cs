namespace WinformsMVP.Services
{
    /// <summary>
    /// A unit of service registration owned by a UI module. Modules register into the core
    /// <see cref="IServiceRegistry"/>, so modular composition works with no external DI container.
    /// When a real container is used, the same module is applied against its registration surface
    /// by the bridge package instead.
    /// </summary>
    public interface IServiceModule
    {
        void RegisterServices(IServiceRegistry registry);
    }
}
