namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// The WinForms-backed framework built-ins. <c>ServiceLocator</c> (in WinformsMVP.Core) finds
    /// this type by name and calls <see cref="Register"/> when it seeds a provider, so the core
    /// assembly needs no compile-time reference to WinForms.
    /// </summary>
    internal static class WinFormsPlatformServices
    {
        internal static void Register(IServiceRegistry registry)
        {
            registry.RegisterInstance<IViewMappingRegister>(new ViewMappingRegister());
            registry.RegisterInstance(new MessageDialogOptions());
            registry.RegisterInstance<IMessageDialogBackend>(new NativeMessageDialogBackend());
            registry.RegisterFactory<IMessageService>(sp => new MessageService(
                sp.ResolveRequired<IMessageDialogBackend>(), sp.ResolveRequired<MessageDialogOptions>()));
            registry.RegisterInstance(new ToastHostOptions());
            registry.RegisterFactory(sp => new WinFormsToastService(
                sp.ResolveRequired<ToastHostOptions>(),
                sp.Resolve<WinformsMVP.Common.ToastRenderer>(),
                sp.Resolve<WinformsMVP.Logging.ILoggerFactory>()));
            registry.RegisterFactory<IWinFormsToastService>(sp => sp.ResolveRequired<WinFormsToastService>());
            registry.RegisterFactory<IToastService>(sp => sp.ResolveRequired<IWinFormsToastService>());
            registry.RegisterInstance(new DialogProviderOptions());
            registry.RegisterFactory(sp => new DialogProvider(sp.ResolveRequired<DialogProviderOptions>()));
            registry.RegisterFactory<IDialogProvider>(sp => sp.ResolveRequired<DialogProvider>());
            registry.RegisterFactory<IWinFormsDialogProvider>(sp => sp.ResolveRequired<DialogProvider>());
            registry.RegisterFactory<IWindowNavigator>(sp =>
                new WindowNavigator(sp.Resolve<IViewMappingRegister>().WithServiceProvider(sp)));
        }
    }
}
