using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Kibinago.Presenters;
using Kibinago.Samples.AnchoredMessageDemo;
using Kibinago.Samples.AsyncDemo;
using Kibinago.Samples.BindingDemo;
using Kibinago.Samples.BulkBindingDemo;
using Kibinago.Samples.BusyDemo;
using Kibinago.Samples.CascadeDemo;
using Kibinago.Samples.ChangeTrackingDemo;
using Kibinago.Samples.CheckBoxDemo;
using Kibinago.Samples.CompositionDemo;
using Kibinago.Samples.ComplexInteractionDemo_EventBased;
using Kibinago.Samples.ComplexInteractionDemo_ServiceBased;
using Kibinago.Samples.CustomizationDemo;
using Kibinago.Samples.DebounceThrottleDemo;
using Kibinago.Samples.EmailDemo;
using Kibinago.Samples.EmailDemo.Services;
using Kibinago.Samples.FirstErrorDemo;
using Kibinago.Samples.LegacyPopupDemo;
using Kibinago.Samples.ListDemo;
using Kibinago.Samples.MasterDetailDemo;
using Kibinago.Samples.MessageBoxDemo;
using Kibinago.Samples.MVPComparisonDemo;
using Kibinago.Samples.NavigatorDemo;
using Kibinago.Samples.ToastDemo;
using Kibinago.Samples.ToDoDemo;
using Kibinago.Samples.ValidationDemo;
using Kibinago.Samples.ValueBindingDemo;
using Kibinago.Samples.WindowClosingDemo;
using Kibinago.Services;

namespace Kibinago.Samples.Shell
{
    /// <summary>
    /// The view side of the catalog: the control of each embedded demo and the window of each demo that
    /// opens from its tab, by demo id (<see cref="DemoIds"/>).
    /// </summary>
    public static class DemoViews
    {
        // The control of each demo that runs in its tab.
        private static readonly Dictionary<string, Func<Control>> Embedded = new Dictionary<string, Func<Control>>
        {
            [DemoIds.ToDo] = () => new ToDoDemoControl(),
            [DemoIds.Busy] = () => new BusyDemoControl(),
            [DemoIds.Sort] = () => new SortDemoControl(),
            [DemoIds.Move] = () => new MoveDemoControl(),
            [DemoIds.Contacts] = () => new ContactsDemoControl(),
            [DemoIds.Reservation] = () => new ReservationDemoControl(),
            [DemoIds.FirstError] = () => new IntakeControl(),
            [DemoIds.ProfileEdit] = () => new ProfileEditControl(),
            [DemoIds.Outpatient] = () => new OutpatientControl(),
        };

        // The window of each demo that opens from its tab (owner = this main window).
        private static readonly Dictionary<string, Action<ShellForm>> Windows = new Dictionary<string, Action<ShellForm>>
        {
            [DemoIds.MvpComparison] = owner => new MVPComparisonLauncher().Show(owner),
            [DemoIds.CheckBox] = owner => ShowConnected(owner, new SettingsDemoForm(), v => new SettingsDemoPresenter().Connect(v)),
            [DemoIds.BulkBinding] = owner => ShowConnected(owner, new SurveyDemoForm(), v => new SurveyDemoPresenter().Connect(v)),
            [DemoIds.ValueBinding] = owner => ShowConnected(owner, new ValueBindingForm(), v => new ValueBindingPresenter().Connect(v)),
            [DemoIds.ExplicitEvent] = owner => ShowConnected(owner, new ExplicitEventDemoForm(),
                v => new ExplicitEventDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()).Connect(v)),
            [DemoIds.ExplicitParameters] = owner => ShowConnected(owner, new MixedParametersDemoForm(),
                v => new MixedParametersDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()).Connect(v)),
            [DemoIds.Async] = owner => ShowConnected(owner, new AsyncDemoForm(), v => new AsyncDemoPresenter().Connect(v)),
            [DemoIds.DebounceThrottle] = owner => new DebounceThrottleDemoForm().Show(owner),
            [DemoIds.Navigator] = owner => ShowConnected(owner, new NavigatorDemoForm(), v => new NavigatorDemoPresenter().Connect(v)),
            [DemoIds.WindowClosing] = owner => ShowConnected(owner, new WindowClosingDemoForm(), v => new WindowClosingDemoPresenter().Connect(v)),
            [DemoIds.LegacyPopup] = owner => ShowConnected(owner, new LegacyPopupDemoForm(), v => new LegacyPopupDemoPresenter().Connect(v)),
            [DemoIds.MessageBox] = owner => new MessageBoxDemoForm().Show(owner),
            [DemoIds.Toast] = owner => new ToastDemoForm().Show(owner),
            [DemoIds.AnchoredMessage] = owner => ShowConnected(owner, new AnchoredMessageDemoForm(), v => new AnchoredMessageDemoPresenter().Connect(v)),
            [DemoIds.Validation] = owner => ShowConnected(owner, new ValidationDemoForm(), v => new ValidationDemoPresenter().Connect(v)),
            [DemoIds.MasterDetail] = owner => ShowConnected(owner, new MasterDetailForm(), v => new MasterDetailPresenter().Connect(v)),
            [DemoIds.Cascade] = owner => CascadeDemoProgram.Run(),
            [DemoIds.ServiceBased] = owner => ComplexInteractionDemoServiceBasedProgram.Run(),
            [DemoIds.EventBased] = owner => ComplexInteractionDemoEventBasedProgram.Run(),
            [DemoIds.Customization] = owner =>
            {
                // The running application is never reconfigured: the customized look runs as its own process.
                if (owner.CustomProfile)
                    ShowConnected(owner, new CustomizationDemoForm(), v => new CustomizationDemoPresenter().Connect(v));
                else
                    Process.Start(Application.ExecutablePath, Program.CustomProfileArguments);
            },
            [DemoIds.Email] = owner => ShowConnected(owner, new MainEmailForm(), v => new MainEmailPresenter(new InMemoryEmailRepository()).Connect(v)),
        };

        /// <summary>Whether there is a control (embedded) or a window for a demo — checked by the tests.</summary>
        public static bool Has(string demoId, DemoKind kind) =>
            kind == DemoKind.Embedded ? Embedded.ContainsKey(demoId) : Windows.ContainsKey(demoId);

        /// <summary>Creates the control of an embedded demo.</summary>
        public static Control Create(string demoId) => Embedded[demoId]();

        /// <summary>Opens the window of a demo, owned by the main window.</summary>
        public static void Open(string demoId, ShellForm owner) => Windows[demoId](owner);

        // A demo Form whose presenter is connected here (adopted hosting), shown as owned by the main window.
        private static void ShowConnected<TForm>(ShellForm owner, TForm form, Action<TForm> connect) where TForm : Form
        {
            connect(form);
            form.Show(owner);
        }
    }
}
