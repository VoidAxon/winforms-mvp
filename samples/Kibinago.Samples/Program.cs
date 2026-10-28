using System;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.CustomizationDemo;
using Kibinago.Samples.Shell;
using Kibinago.Services;

namespace Kibinago.Samples
{
    static class Program
    {
        /// <summary>Command-line arguments that start the sample with the customized configuration.</summary>
        public const string CustomProfileArguments = "--profile custom";

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // --profile custom: the Customization demo's configuration (company dialogs, branded toasts).
            // A running application is configured once, here; the shell never reconfigures it.
            var custom = string.Join(" ", args).Trim() == CustomProfileArguments;

            // One startup call: framework customization, then services.
            MvpApplication.Configure(framework: mvp =>
            {
                mvp.Busy.UseOverlay();   // ShowBusy also covers the view with an overlay (after mvp.Busy.Delay)
                if (custom)
                    CustomizationDemoSetup.Configure(mvp);
            });

            // Maps every view interface in this assembly to its Form, so Navigator can create windows.
            ServiceLocator.Current.Resolve<IViewMappingRegister>()
                .RegisterFromAssembly(typeof(Program).Assembly);

            // The main window: sidebar of categories, tabs of demos.
            var shell = new ShellForm { CustomProfile = custom };
            new ShellPresenter(SampleCatalog.Create(), custom ? SampleCatalog.CustomizeCategoryId : null).Connect(shell);
            MvpApplication.Run(shell);   // disposes the framework services when the main window closes
        }
    }
}
