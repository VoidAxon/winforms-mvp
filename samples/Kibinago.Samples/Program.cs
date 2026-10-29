using System;
using System.Windows.Forms;
using Kibinago.Logging;
using Kibinago.Presenters;
using Kibinago.Samples.ActionLogDemo;
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

            // An action handler's unhandled exception propagates like any WinForms event handler's
            // (the framework logs it first): show it instead of the default crash dialog.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                ServiceLocator.Current.Resolve<IMessageService>().ShowError(e.Exception.Message);

            // --profile custom: the Customization demo's configuration (company dialogs, branded toasts).
            // A running application is configured once, here; the shell never reconfigures it.
            var custom = string.Join(" ", args).Trim() == CustomProfileArguments;

            // The operation log: every action any presenter runs is reported to it (操作ログ demo).
            var actionLog = new SampleActionLog();

            // One startup call: framework customization, then services.
            MvpApplication.Configure(framework: mvp =>
            {
                mvp.Busy.UseOverlay();   // ShowBusy also covers the view with an overlay (after mvp.Busy.Delay)
                mvp.Logging.Use(new DebugLoggerFactory());   // presenters' Logger writes to the debugger's output
                mvp.Actions.Observe(actionLog);
                if (custom)
                    CustomizationDemoSetup.Configure(mvp);
            },
            services: reg => reg.RegisterInstance(actionLog));   // the demo's presenter reads it

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
