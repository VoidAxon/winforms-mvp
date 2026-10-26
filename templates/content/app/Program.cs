using System;
using System.Windows.Forms;
using Kibinago;
using Kibinago.Presenters;
using Kibinago.Services;

namespace MyMvpApp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
//#if (Framework == "net10.0-windows")
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
//#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // An exception nobody handled ends up here — including one thrown by a ViewAction handler
            // (the framework logs it, then lets it propagate like any WinForms event handler's).
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                MessageBox.Show(e.Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // One startup call: framework customization, then your business services.
            // Every framework service (messages, toasts, dialogs, navigator, ...) has a default.
            MvpApplication.Configure(
                framework: mvp =>
                {
                    // Type "mvp." to see what can be replaced, e.g.:
                    // mvp.Messages.DefaultCaption = "MyMvpApp";
                    // mvp.Toasts.Duration = 4000;
                },
                services: services =>
                {
                    // Only what is resolved from the container (ServiceLocator.Current), e.g. a replacement
                    // for a framework service. Business services need no registration: see below.
                });

            // Maps every view interface in this assembly to its Form, so Navigator can create windows.
            ServiceLocator.Current.Resolve<IViewMappingRegister>()
                .RegisterFromAssembly(typeof(Program).Assembly);

            // Business services (repositories, ...): create them once here and pass them to the main
            // presenter's constructor; it passes them on to the presenters of the windows it opens.
            // var orders = new SqlOrderRepository(connectionString);

            // The main window is created here, so its presenter is connected to it (adopted hosting);
            // every other window is opened from a presenter through Navigator.
            var mainForm = new MainForm();
            new MainPresenter(/* orders */).Connect(mainForm);
            MvpApplication.Run(mainForm);   // disposes the framework services when the main form closes
        }
    }
}
