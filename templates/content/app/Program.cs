using System;
using System.Windows.Forms;
using WinformsMVP.MVP.Presenters;
using WinformsMVP.Services;
using WinformsMVP.Services.Implementations;

namespace MvpApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
#if NET
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Framework services (messages, dialogs, navigator, ...) are registered by default.
            // Register your business services here; presenters get them through their constructors.
            ServiceLocator.Configure(services =>
            {
                // services.RegisterInstance<IOrderRepository>(new SqlOrderRepository(connectionString));
            });

            // Maps every view interface in this assembly to its Form, so Navigator can create windows.
            ServiceLocator.Current.Resolve<IViewMappingRegister>()
                .RegisterFromAssembly(typeof(Program).Assembly);

            // The main window is created here, so its presenter is connected to it (adopted hosting);
            // every other window is opened from a presenter through Navigator.
            var mainForm = new MainForm();
            new MainPresenter().Connect(mainForm);
            Application.Run(mainForm);
        }
    }
}
