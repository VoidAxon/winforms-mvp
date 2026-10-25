using System;
using Kibinago;
using Kibinago.Presenters;
using System.Drawing;
using System.Windows.Forms;
using Kibinago.Samples.ToDoDemo;
using Kibinago.Samples.CheckBoxDemo;
using Kibinago.Samples.BulkBindingDemo;
using Kibinago.Samples.ValueBindingDemo;
using Kibinago.Samples.NavigatorDemo;
using Kibinago.Samples.MVPComparisonDemo;
using Kibinago.Samples.LegacyPopupDemo;
using Kibinago.Samples.MessageBoxDemo;
using Kibinago.Samples.EmailDemo;
using Kibinago.Samples.EmailDemo.Services;
using Kibinago.Samples.ComplexInteractionDemo_ServiceBased;
using Kibinago.Samples.ComplexInteractionDemo_EventBased;
using Kibinago.Samples.CascadeDemo;
using Kibinago.Samples.ToastDemo;
using Kibinago.Samples.AnchoredMessageDemo;
using Kibinago.Samples.DebounceThrottleDemo;
using Kibinago.Samples.AsyncDemo;
using Kibinago.Samples.CustomizationDemo;
using Kibinago.Samples.ValidationDemo;
using Kibinago.Services;
using Kibinago.Services.Implementations;

namespace Kibinago.Samples
{
    /// <summary>
    /// Sample launcher for WinForms MVP demos. Demos are grouped into categories and laid out in a
    /// two-column grid so the whole catalog fits on screen without a tall single column.
    /// </summary>
    public class SampleLauncherForm : Form
    {
        private const int ContentWidth = 720;
        private const int ColumnGutter = 8;

        private readonly ToolTip _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300 };

        public SampleLauncherForm()
        {
            InitializeComponent();
        }

        /// <summary>A single launchable demo entry.</summary>
        private sealed class DemoItem
        {
            public readonly string Title;
            public readonly string Description;
            public readonly Color Color;
            public readonly Action Launch;

            public DemoItem(string title, string description, Color color, Action launch)
            {
                Title = title;
                Description = description;
                Color = color;
                Launch = launch;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "WinForms MVP - Sample Launcher";
            this.ClientSize = new Size(ContentWidth + 56, 785);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            // Accent header bar
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.FromArgb(0, 120, 215)
            };
            var titleLabel = new Label
            {
                Text = "WinForms MVP Framework",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(24, 12),
                AutoSize = true
            };
            var subtitleLabel = new Label
            {
                Text = "Select a demo to explore the framework",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(220, 235, 250),
                Location = new Point(26, 44),
                AutoSize = true
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            // Scrollable content area holding the category sections
            var content = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(24, 16, 24, 16),
                BackColor = Color.White
            };

            // Add content first, then header, so the docked header stays on top.
            this.Controls.Add(content);
            this.Controls.Add(header);

            AddSection(content, "ViewAction",
                new DemoItem("ToDo List Demo",
                    "Action-driven updates • CanExecute • State management",
                    Color.FromArgb(0, 120, 215), LaunchToDoDemo),
                new DemoItem("CheckBox / RadioButton Demo",
                    "CheckedChanged events • Settings UI • MVP pattern",
                    Color.FromArgb(16, 137, 62), LaunchCheckBoxDemo),
                new DemoItem("Bulk Binding Demo (Survey)",
                    "AddRange • AddByTag • Many RadioButtons",
                    Color.FromArgb(139, 69, 19), LaunchBulkBindingDemo),
                new DemoItem("Value Binding Demo",
                    "Add(ctrl, selector) • CheckBox/NumericUpDown/DateTimePicker/MonthCalendar values as payload",
                    Color.FromArgb(120, 80, 0), LaunchValueBindingDemo),
                new DemoItem("Explicit Event Pattern",
                    "ActionBinder => null • Bind() • View.ActionRequest • availability via view members",
                    Color.FromArgb(70, 90, 120), LaunchExplicitEventDemo),
                new DemoItem("Explicit Pattern with Parameters",
                    "Value bindings carry payloads through ActionRequest",
                    Color.FromArgb(90, 70, 130), LaunchExplicitParametersDemo));

            AddSection(content, "Async & Validation",
                new DemoItem("Async Operations Demo",
                    "async actions • auto-disable while running • cancellation • progress",
                    Color.FromArgb(0, 99, 177), LaunchAsyncDemo),
                new DemoItem("Validation Demo",
                    "DataAnnotations • ErrorProvider • errors after leaving a field / on submit",
                    Color.FromArgb(196, 43, 28), LaunchValidationDemo));

            AddSection(content, "Navigation & Windows",
                new DemoItem("WindowNavigator Demo",
                    "Modal • Non-Modal • Parameters • Results",
                    Color.FromArgb(75, 0, 130), LaunchNavigatorDemo),
                new DemoItem("MessageBox Positioning Demo",
                    "Native MessageBox • Windows API Hook • Positioning",
                    Color.FromArgb(255, 140, 0), LaunchMessageBoxDemo),
                new DemoItem("Toast Notification Demo",
                    "Layered popup • Invisible to OpenForms (like MessageBox)",
                    Color.FromArgb(0, 150, 199), LaunchToastDemo),
                new DemoItem("Framework Customization",
                    "One startup call • Company dialogs • Branded toasts • Change policy",
                    Color.FromArgb(0, 120, 110), LaunchCustomizationDemo),
                new DemoItem("Anchored Message Demo",
                    "Corner toast by default • Anchored via semantic view methods • AnchoredToast/AnchoredMessageBox",
                    Color.FromArgb(0, 128, 128), LaunchAnchoredMessageDemo));

            AddSection(content, "Architecture",
                new DemoItem("MVP Pattern Comparison",
                    "Passive View vs Supervising Controller",
                    Color.FromArgb(220, 20, 60), LaunchMVPComparisonDemo),
                new DemoItem("Legacy Popup",
                    "Open a non-MVP form from an MVP screen • Semantic view method • InteractionResult",
                    Color.FromArgb(128, 0, 128), LaunchLegacyPopupDemo),
                new DemoItem("Debounce / Throttle",
                    "Rate-limit rapid events • UI-thread marshaling • Error handling",
                    Color.FromArgb(0, 128, 128), LaunchDebounceThrottleDemo));

            AddSection(content, "Cross-Presenter Communication",
                new DemoItem("Order Mgmt (Service-Based)",
                    "Shared Service Layer • Zero Presenter Coupling",
                    Color.FromArgb(34, 139, 34), LaunchServiceBasedDemo),
                new DemoItem("Order Mgmt (EventAggregator)",
                    "Event Aggregator Pub-Sub • Decoupled Messaging",
                    Color.FromArgb(184, 134, 11), LaunchEventBasedDemo),
                new DemoItem("Cascade (N-level selection)",
                    "N-level cascading selection • Selection • Child presenter wiring",
                    Color.FromArgb(0, 100, 160), LaunchCascadeDemo));

            AddSection(content, "Complete Application",
                new DemoItem("Email Demo (Complete App)",
                    "All Features • ChangeTracker • Navigator • Validation",
                    Color.FromArgb(0, 150, 136), LaunchEmailDemo));
        }

        /// <summary>Builds one category: a heading, a divider line, and a two-column button grid.</summary>
        private void AddSection(FlowLayoutPanel parent, string name, params DemoItem[] items)
        {
            var section = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0, 0, 0, 14)
            };

            var heading = new Label
            {
                Text = name,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(64, 64, 64),
                AutoSize = false,
                Size = new Size(ContentWidth, 22),
                Margin = new Padding(0, 0, 0, 2),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var divider = new Panel
            {
                Height = 1,
                Width = ContentWidth,
                BackColor = Color.Gainsboro,
                Margin = new Padding(0, 0, 0, 8)
            };

            var grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            foreach (var item in items)
            {
                grid.Controls.Add(CreateDemoButton(item));
            }

            section.Controls.Add(heading);
            section.Controls.Add(divider);
            section.Controls.Add(grid);
            parent.Controls.Add(section);
        }

        private Button CreateDemoButton(DemoItem item)
        {
            int buttonWidth = (ContentWidth - ColumnGutter) / 2 - 8;
            var button = new Button
            {
                Text = item.Title,
                Size = new Size(buttonWidth, 50),
                Margin = new Padding(4),
                Font = new Font("Segoe UI", 10f),
                BackColor = item.Color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => item.Launch();
            _toolTip.SetToolTip(button, item.Description);
            return button;
        }

        private void LaunchToDoDemo()
        {
            var view = new ToDoDemoForm();
            var presenter = new ToDoDemoPresenter();  // Uses CommonServices.Default

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchCheckBoxDemo()
        {
            var view = new SettingsDemoForm();
            var presenter = new SettingsDemoPresenter();

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchBulkBindingDemo()
        {
            var view = new SurveyDemoForm();
            var presenter = new SurveyDemoPresenter();

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchValueBindingDemo()
        {
            var view = new ValueBindingForm();
            var presenter = new ValueBindingPresenter();

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchNavigatorDemo()
        {
            // Automatic assembly scanning - registers all Views in the assembly
            var viewMappingRegister = new ViewMappingRegister();
            int registered = viewMappingRegister.RegisterFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
            System.Diagnostics.Debug.WriteLine($"ViewMappingRegister: Auto-registered {registered} Views from assembly");

            // Configure ServiceLocator with the scanned register so Navigator resolves correctly.
            ServiceLocator.Configure(reg =>
            {
                reg.RegisterInstance<IViewMappingRegister>(viewMappingRegister);
            });

            var view = new NavigatorDemoForm();
            var presenter = new NavigatorDemoPresenter();  // No constructor parameters needed!

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        /// <summary>Shows <paramref name="view"/> after <paramref name="connect"/> wired its presenter.</summary>
        private void ShowDemo(Form view, Action connect)
        {
            connect();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchExplicitEventDemo()
        {
            var view = new ExplicitEventDemoForm();
            ShowDemo(view, () => new ExplicitEventDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()).Connect(view));
        }

        private void LaunchExplicitParametersDemo()
        {
            var view = new MixedParametersDemoForm();
            ShowDemo(view, () => new MixedParametersDemoPresenter(ServiceLocator.Current.Resolve<IMessageService>()).Connect(view));
        }

        private void LaunchAsyncDemo()
        {
            var view = new AsyncDemoForm();
            ShowDemo(view, () => new AsyncDemoPresenter().Connect(view));
        }

        private void LaunchValidationDemo()
        {
            var view = new ValidationDemoForm();
            ShowDemo(view, () => new ValidationDemoPresenter().Connect(view));
        }

        private void LaunchMVPComparisonDemo()
        {
            var launcher = new MVPComparisonLauncher();
            launcher.Show();

            this.Hide();
            launcher.FormClosed += (s, e) => this.Show();
        }

        private void LaunchLegacyPopupDemo()
        {
            var view = new LegacyPopupDemoForm();
            var presenter = new LegacyPopupDemoPresenter();

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchDebounceThrottleDemo()
        {
            // View-driven demo: rate limiting is a UI-timing concern, so there is no presenter.
            var view = new DebounceThrottleDemoForm();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchMessageBoxDemo()
        {
            // View-driven demo: positioning is a View concern, so there is no presenter.
            var view = new MessageBoxDemoForm();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchEmailDemo()
        {
            // Automatic assembly scanning - registers all Views in the assembly
            var viewMappingRegister = new ViewMappingRegister();
            int registered = viewMappingRegister.RegisterFromAssembly(System.Reflection.Assembly.GetExecutingAssembly());
            System.Diagnostics.Debug.WriteLine($"ViewMappingRegister: Auto-registered {registered} Views from assembly");

            // Configure ServiceLocator with the scanned register so Navigator resolves correctly.
            ServiceLocator.Configure(reg =>
            {
                reg.RegisterInstance<IViewMappingRegister>(viewMappingRegister);
            });

            // Create repository and main view
            var repository = new InMemoryEmailRepository();
            var view = new MainEmailForm();
            var presenter = new MainEmailPresenter(repository);

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchCustomizationDemo()
        {
            // An application does this once in Program.cs. The launcher switches the whole
            // configuration for this demo and restores the defaults when it closes. Each Configure
            // disposes the previous services, so a demo still open from before (e.g. the Toast demo,
            // which holds its toast service) stops showing toasts until it is reopened.
            MvpApplication.Configure(framework: CustomizationDemoSetup.Configure);

            var view = new CustomizationDemoForm();
            ShowDemo(view, () => new CustomizationDemoPresenter().Connect(view));
            view.FormClosed += (s, e) => MvpApplication.Configure();
        }

        private void LaunchToastDemo()
        {
            var view = new ToastDemoForm();
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }

        private void LaunchServiceBasedDemo()
        {
            this.Hide();
            ComplexInteractionDemoServiceBasedProgram.Run();
            this.Show();
        }

        private void LaunchEventBasedDemo()
        {
            this.Hide();
            ComplexInteractionDemoEventBasedProgram.Run();
            this.Show();
        }

        private void LaunchCascadeDemo()
        {
            this.Hide();
            CascadeDemoProgram.Run();
            this.Show();
        }

        private void LaunchAnchoredMessageDemo()
        {
            var view = new AnchoredMessageDemoForm();
            var presenter = new AnchoredMessageDemoPresenter();

            presenter.Connect(view);  // attach + initialize + lifetime wiring
            view.Show();

            this.Hide();
            view.FormClosed += (s, e) => this.Show();
        }
    }
}
