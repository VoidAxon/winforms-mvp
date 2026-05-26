using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiProjectDemo.OrderModule;
using MultiProjectDemo.UserModule;
using WinformsMVP.DependencyInjection;
using WinformsMVP.Logging.MicrosoftExtensions;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace MultiProjectDemo.Shell
{
    /// <summary>
    /// Reference Shell that wires multiple UI modules through the WinformsMVP DI
    /// integration. The Shell does not know what types live inside each module —
    /// it just instantiates each module's <c>IModuleRegistrar</c> and lets the
    /// framework do the wiring.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Shared View registry — populated by each module.
            var viewRegistry = new ViewMappingRegister();
            viewRegistry.Register<IMainView, MainForm>();  // Shell's own view.

            // 2. DI container — modules contribute Views and services in one pass.
            var services = new ServiceCollection();
            services.RegisterModules(viewRegistry,
                new UserModuleRegistrar(),
                new OrderModuleRegistrar());

            // 3. Framework's own services (IPresenterFactory, IViewMappingRegister, ...).
            services.AddWinformsMVP(viewRegistry);

            // 4. Shell-owned Presenters.
            services.AddTransient<MainPresenter>();

            // 5. Build the provider and wire PlatformServices.
            var provider = services.BuildServiceProvider();
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug());

            PlatformServices.Default = new DefaultPlatformServices(
                viewMappingRegister: viewRegistry,
                loggerFactory: loggerFactory.AsFrameworkLoggerFactory(),
                serviceProvider: provider);

            // 6. Resolve the root Presenter from DI, show it, and pump messages.
            var mainPresenter = provider.GetRequiredService<MainPresenter>();
            PlatformServices.Default.WindowNavigator.ShowWindow(mainPresenter);

            Application.Run();
        }
    }
}
