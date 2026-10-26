namespace Kibinago.Services.Implementations
{
    /// <summary>
    /// The WinForms-backed framework built-ins. <c>ServiceLocator</c> (in Kibinago.Core) finds
    /// this type by name and calls <see cref="Register"/> when it seeds a provider, so the core
    /// assembly needs no compile-time reference to WinForms.
    /// </summary>
    internal static class WinFormsPlatformServices
    {
        internal static void Register(IServiceRegistry registry)
        {
            registry.RegisterInstance<IViewMappingRegister>(new ViewMappingRegister());
            registry.RegisterSingleton<IWindowNavigator>(sp =>
                new WindowNavigator(sp.Resolve<IViewMappingRegister>().WithServiceProvider(sp)));

            // Messages, toasts, dialogs, change tracking, events, logging: the builder's defaults,
            // the same list AddKibinago and MvpApplication.Configure start from.
            // Only ServiceLocator calls this hook, always with its own DefaultServiceProvider.
            var provider = registry as DefaultServiceProvider
                ?? throw new System.ArgumentException("Expected the framework's DefaultServiceProvider.", nameof(registry));
            new Kibinago.Configuration.KibinagoBuilder().Apply(provider);
        }
    }
}
