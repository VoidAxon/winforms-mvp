using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiProjectDemo.OrderModule;
using MultiProjectDemo.UserModule;
using MultiProjectDemo.Shell.Logging;
using WinformsMVP.DependencyInjection;
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

            // 3. Framework's own services (IPresenterFactory, IViewMappingRegister,
            //    IMessageService, IWindowNavigator, ...).
            services.AddWinformsMVP(viewRegistry);

            // 4. Override the framework logger with a real M.E.Logging-backed factory.
            var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
            services.AddSingleton<WinformsMVP.Logging.ILoggerFactory>(
                loggerFactory.AsFrameworkLoggerFactory());

            // 5. Shell-owned Presenters.
            services.AddTransient<MainPresenter>();

            // 6. Build the provider and hand it to ServiceLocator so presenter
            //    convenience accessors (Messages, Dialogs, Navigator, ...) resolve
            //    through M.E.DI.
            var provider = services.BuildServiceProvider();
            provider.UseWinformsMVP();

            // 7. Resolve the root Presenter from DI, show it, and pump messages.
            var mainPresenter = provider.GetRequiredService<MainPresenter>();
            provider.GetRequiredService<IWindowNavigator>().ShowWindow(mainPresenter);

            Application.Run();
        }
    }
}
